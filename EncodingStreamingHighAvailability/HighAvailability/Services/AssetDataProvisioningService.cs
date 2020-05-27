namespace HighAvailability.Services
{
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Specialized;
    using HighAvailability.Helpers;
    using HighAvailability.Interfaces;
    using HighAvailability.Models;
    using Microsoft.Azure.Management.Media;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Implements encoded asset provisioning to multiple Azure Media Services instances
    /// </summary>
    public class AssetDataProvisioningService : IProvisioningService
    {
        /// <summary>
        /// Factory to get Azure Media Service instance client
        /// </summary>
        private readonly IMediaServiceInstanceFactory mediaServiceInstanceFactory;

        /// <summary>
        /// Configuration container
        /// </summary>
        private readonly IConfigService configService;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mediaServiceInstanceFactory">Factory to get Azure Media Service instance client</param>
        /// <param name="configService">Configuration container</param>
        public AssetDataProvisioningService(IMediaServiceInstanceFactory mediaServiceInstanceFactory, IConfigService configService)
        {
            this.mediaServiceInstanceFactory = mediaServiceInstanceFactory ?? throw new ArgumentNullException(nameof(mediaServiceInstanceFactory));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        /// <summary>
        /// Provisions encoded assets from Azure Media Services source instance to all other Azure Media Services instances.
        /// </summary>
        /// <param name="provisioningRequestModel">Model to provision</param>
        /// <param name="provisioningCompletedEventModel">Provision completed event model to store provisioning data</param>
        /// <param name="logger">Logger to log data</param>
        public async Task ProvisionAsync(ProvisioningRequestModel provisioningRequest, ProvisioningCompletedEventModel provisioningCompletedEventModel, ILogger logger)
        {
            logger.LogInformation($"AssetDataProvisioningService::ProvisionAsync started: provisioningRequest={LogHelper.FormatObjectForLog(provisioningRequest)}");

            // Make sure that account name that asset is provisioned exists in current configuration
            if (!this.configService.MediaServiceInstanceConfiguration.ContainsKey(provisioningRequest.EncodedAssetMediaServiceAccountName))
            {
                throw new Exception($"AssetDataProvisioningService::ProvisionAsync does not have configuration for account={provisioningRequest.EncodedAssetMediaServiceAccountName}");
            }

            // Get source configuration that asset is provisioned as part of encoding job
            var sourceClientConfiguration = this.configService.MediaServiceInstanceConfiguration[provisioningRequest.EncodedAssetMediaServiceAccountName];
            provisioningCompletedEventModel.AddMediaServiceAccountName(provisioningRequest.EncodedAssetMediaServiceAccountName);

            // Get Azure Media Services instance client associated with provisioned asset
            var sourceClient = await this.mediaServiceInstanceFactory.GetMediaServiceInstanceAsync(provisioningRequest.EncodedAssetMediaServiceAccountName).ConfigureAwait(false);

            // Create a list of Azure Media Services instances that asset needs to be provisioned. It should be all instances listed in configuration, except source instance
            var targetInstances = this.configService.MediaServiceInstanceConfiguration.Keys.Where(
                                    i => !i.Equals(provisioningRequest.EncodedAssetMediaServiceAccountName, StringComparison.InvariantCultureIgnoreCase));

            // Iterate through the list of all Azure Media Services instance names that asset needs to be provisioned to
            foreach (var target in targetInstances)
            {
                // Get target configuration
                var targetClientConfiguration = this.configService.MediaServiceInstanceConfiguration[target];

                // Get client associated with target instance
                var targetClient = await this.mediaServiceInstanceFactory.GetMediaServiceInstanceAsync(target).ConfigureAwait(false);
                
                // Copy data from source instance to target instance
                var asset = await this.CopyAssetAsync(sourceClient, sourceClientConfiguration, targetClient, targetClientConfiguration, provisioningRequest, logger).ConfigureAwait(false);
                
                // Record fact that asset was provisioned to target instance
                provisioningCompletedEventModel.AddMediaServiceAccountName(target);
            }

            logger.LogInformation($"AssetDataProvisioningService::ProvisionAsync completed: provisioningRequest={LogHelper.FormatObjectForLog(provisioningRequest)}");
        }

        /// <summary>
        /// Copies asset data from source Azure Media Services instance to target using Azure Blob Copy
        /// </summary>
        /// <param name="sourceClient">Source Azure Media Services instance client</param>
        /// <param name="sourceConfig">Source Azure Media Services instance configuration</param>
        /// <param name="targetClient">Target Azure Media Services instance client</param>
        /// <param name="targetConfig">Target Azure Media Services instance configuration</param>
        /// <param name="provisioningRequest">Provisioning request data</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Target asset</returns>
        private async Task<Asset> CopyAssetAsync(IAzureMediaServicesClient sourceClient, MediaServiceConfigurationModel sourceConfig,
                                                  IAzureMediaServicesClient targetClient, MediaServiceConfigurationModel targetConfig,
                                                  ProvisioningRequestModel provisioningRequest, ILogger logger)
        {
            logger.LogInformation($"AssetDataProvisioningService::CopyAssetAsync started: provisioningRequest={LogHelper.FormatObjectForLog(provisioningRequest)} sourceInstanceName={sourceConfig.AccountName} targetInstanceName={targetConfig.AccountName}");

            // Need to ensure that target asset exits
            var targetAsset = await targetClient.Assets.GetAsync(targetConfig.ResourceGroup, targetConfig.AccountName, provisioningRequest.EncodedAssetName).ConfigureAwait(false);

            // if there is no target asset, need to provision one
            if (targetAsset == null)
            {
                // create new target asset
                targetAsset = await targetClient.Assets.CreateOrUpdateAsync(targetConfig.ResourceGroup, targetConfig.AccountName, provisioningRequest.EncodedAssetName, new Asset()).ConfigureAwait(false);
                // need to reload asset to get Container value populated, otherwise Container is null after asset creation
                targetAsset = await targetClient.Assets.GetAsync(targetConfig.ResourceGroup, targetConfig.AccountName, provisioningRequest.EncodedAssetName).ConfigureAwait(false);
            }

            // Get SAS token associated with source asset. SAS token is requried to initiate StartCopyFromUri
            var sourceAssetContainerSas = await sourceClient.Assets.ListContainerSasAsync(
               sourceConfig.ResourceGroup,
               sourceConfig.AccountName,
               provisioningRequest.EncodedAssetName,
               permissions: AssetContainerPermission.Read,
               expiryTime: DateTime.UtcNow.AddHours(1).ToUniversalTime()).ConfigureAwait(false);

            var sourceContainerSasUrl = new Uri(sourceAssetContainerSas.AssetContainerSasUrls.FirstOrDefault());

            var sourceBlobClient = new BlobContainerClient(sourceContainerSasUrl);

            var copyTasks = new List<Task>();

            // Get a list of all blobs to copy
            await foreach (var blobItem in sourceBlobClient.GetBlobsAsync())
            {
                // All blobs can be copies in parallel
                copyTasks.Add(Task.Run(() =>
                {
                    // Get target blob
                    var targetBlob = new BlobBaseClient(this.configService.MediaServiceInstanceStorageAccountConnectionStrings[targetConfig.AccountName], targetAsset.Container, blobItem.Name);
                    // Get source blob
                    var sourceBlob = sourceBlobClient.GetBlobClient(blobItem.Name);
                    // Start copy operation, see more data about it https://docs.microsoft.com/en-us/dotnet/api/azure.storage.blobs.specialized.blobbaseclient.startcopyfromuriasync?view=azure-dotnet
                    var copyOperation = targetBlob.StartCopyFromUri(sourceBlob.Uri);
                    // Wait for copy to complete, since this is running on seprate thread, no need to do async
                    var copyResult = copyOperation.WaitForCompletionAsync().GetAwaiter().GetResult();
                    // Check copy operation status
                    if (copyResult.GetRawResponse().Status != 200)
                    {
                        throw new Exception($"Copy operation failed, sourceAccount={sourceConfig.AccountName} targetAccount={targetConfig.AccountName} assetName={provisioningRequest.EncodedAssetName} blobName={blobItem.Name} httpStatus={copyResult.GetRawResponse().Status}");
                    }
                }));
            }

            // Wait for all copy tasks to finish
            await Task.WhenAll(copyTasks).ConfigureAwait(false);
            logger.LogInformation($"AssetDataProvisioningService::CopyAssetAsync completed: provisioningRequest={LogHelper.FormatObjectForLog(provisioningRequest)} sourceInstanceName={sourceConfig.AccountName} targetInstanceName={targetConfig.AccountName} numberOfFiles={copyTasks.Count}");

            return targetAsset;
        }
    }
}
