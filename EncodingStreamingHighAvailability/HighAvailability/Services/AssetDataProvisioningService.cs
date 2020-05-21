namespace HighAvailability.Services
{
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Specialized;
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using Microsoft.Azure.Management.Media;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class AssetDataProvisioningService : IProvisioningService
    {
        private readonly IConfigService configService;

        public AssetDataProvisioningService(IConfigService configService)
        {
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        public async Task ProvisionAsync(StreamProvisioningRequestModel streamProvisioningRequest, ILogger logger)
        {
            logger.LogInformation($"AssetDataProvisioningService::ProvisionAsync started: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)}");

            if (!this.configService.MediaServiceInstanceConfiguration.ContainsKey(streamProvisioningRequest.EncodedAssetMediaServiceAccountName))
            {
                throw new Exception($"AssetDataProvisioningService::ProvisionAsync does not have configuration for account={streamProvisioningRequest.EncodedAssetMediaServiceAccountName}");
            }

            var sourceClientConfiguration = this.configService.MediaServiceInstanceConfiguration[streamProvisioningRequest.EncodedAssetMediaServiceAccountName];

            using (var sourceClient = await MediaServicesHelper.CreateMediaServicesClientAsync(sourceClientConfiguration).ConfigureAwait(false))
            {
                var targetInstances = this.configService.MediaServiceInstanceConfiguration.Keys.Where(i => !i.Equals(streamProvisioningRequest.EncodedAssetMediaServiceAccountName, StringComparison.InvariantCultureIgnoreCase));

                foreach (var target in targetInstances)
                {
                    var targetClientConfiguration = this.configService.MediaServiceInstanceConfiguration[target];
                    using (var targetClient = await MediaServicesHelper.CreateMediaServicesClientAsync(targetClientConfiguration).ConfigureAwait(false))
                    {
                        var asset = await this.CopyAssetAsync(sourceClient, sourceClientConfiguration, targetClient, targetClientConfiguration, streamProvisioningRequest, logger).ConfigureAwait(false);
                    }
                }
            }

            logger.LogInformation($"AssetDataProvisioningService::ProvisionAsync completed: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)}");
        }

        private async Task<Asset> CopyAssetAsync(IAzureMediaServicesClient sourceClient, MediaServiceConfigurationModel sourceConfig,
                                                  IAzureMediaServicesClient targetClient, MediaServiceConfigurationModel targetConfig,
                                                  StreamProvisioningRequestModel streamProvisioningRequest, ILogger logger)
        {
            logger.LogInformation($"StreamProvisioningService::CopyAssetAsync started: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)} sourceInstanceName={sourceConfig.AccountName} targetInstanceName={targetConfig.AccountName}");

            var targetAsset = await targetClient.Assets.GetAsync(targetConfig.ResourceGroup, targetConfig.AccountName, streamProvisioningRequest.EncodedAssetName).ConfigureAwait(false);

            if (targetAsset == null)
            {
                targetAsset = await targetClient.Assets.CreateOrUpdateAsync(targetConfig.ResourceGroup, targetConfig.AccountName, streamProvisioningRequest.EncodedAssetName, new Asset()).ConfigureAwait(false);
                // TBD to verify 
                // need to reload asset to get Container value populated, otherwise Container is null after asset creation
                targetAsset = await targetClient.Assets.GetAsync(targetConfig.ResourceGroup, targetConfig.AccountName, streamProvisioningRequest.EncodedAssetName).ConfigureAwait(false);
            }

            var sourceAssetContainerSas = await sourceClient.Assets.ListContainerSasAsync(
               sourceConfig.ResourceGroup,
               sourceConfig.AccountName,
               streamProvisioningRequest.EncodedAssetName,
               permissions: AssetContainerPermission.Read,
               expiryTime: DateTime.UtcNow.AddHours(1).ToUniversalTime()).ConfigureAwait(false);

            var sourceContainerSasUrl = new Uri(sourceAssetContainerSas.AssetContainerSasUrls.FirstOrDefault());

            var sourceBlobClient = new BlobContainerClient(sourceContainerSasUrl);

            var copyTasks = new List<Task>();

            await foreach (var blobItem in sourceBlobClient.GetBlobsAsync())
            {
                copyTasks.Add(Task.Run(() =>
                {
                    var targetBlob = new BlobBaseClient(this.configService.MediaServiceInstanceStorageAccountConnectionStrings[targetConfig.AccountName], targetAsset.Container, blobItem.Name);
                    var sourceBlob = sourceBlobClient.GetBlobClient(blobItem.Name);
                    var copyOperation = targetBlob.StartCopyFromUri(sourceBlob.Uri);
                    var copyResult = copyOperation.WaitForCompletionAsync().GetAwaiter().GetResult();
                    if (copyResult.GetRawResponse().Status != 200)
                    {
                        throw new Exception($"Copy operation failed, sourceAccount={sourceConfig.AccountName} targetAccount={targetConfig.AccountName} assetName={streamProvisioningRequest.EncodedAssetName} blobName={blobItem.Name} httpStatus={copyResult.GetRawResponse().Status}");
                    }
                }));
            }

            await Task.WhenAll(copyTasks).ConfigureAwait(false);
            logger.LogInformation($"StreamProvisioningService::CopyAssetAsync completed: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)} sourceInstanceName={sourceConfig.AccountName} targetInstanceName={targetConfig.AccountName} numberOfFiles={copyTasks.Count}");

            return targetAsset;
        }
    }
}
