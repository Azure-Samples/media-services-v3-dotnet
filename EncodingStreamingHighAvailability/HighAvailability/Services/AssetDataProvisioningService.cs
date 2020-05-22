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

        public async Task ProvisionAsync(ProvisioningRequestModel provisioningRequest, ProvisioningCompletedEventModel provisioningCompletedEventModel, ILogger logger)
        {
            logger.LogInformation($"AssetDataProvisioningService::ProvisionAsync started: provisioningRequest={LogHelper.FormatObjectForLog(provisioningRequest)}");

            if (!this.configService.MediaServiceInstanceConfiguration.ContainsKey(provisioningRequest.EncodedAssetMediaServiceAccountName))
            {
                throw new Exception($"AssetDataProvisioningService::ProvisionAsync does not have configuration for account={provisioningRequest.EncodedAssetMediaServiceAccountName}");
            }

            var sourceClientConfiguration = this.configService.MediaServiceInstanceConfiguration[provisioningRequest.EncodedAssetMediaServiceAccountName];
            provisioningCompletedEventModel.AddMediaServiceAccountName(provisioningRequest.EncodedAssetMediaServiceAccountName);

            using (var sourceClient = await MediaServicesHelper.CreateMediaServicesClientAsync(sourceClientConfiguration).ConfigureAwait(false))
            {
                var targetInstances = this.configService.MediaServiceInstanceConfiguration.Keys.Where(i => !i.Equals(provisioningRequest.EncodedAssetMediaServiceAccountName, StringComparison.InvariantCultureIgnoreCase));

                foreach (var target in targetInstances)
                {
                    var targetClientConfiguration = this.configService.MediaServiceInstanceConfiguration[target];
                    using (var targetClient = await MediaServicesHelper.CreateMediaServicesClientAsync(targetClientConfiguration).ConfigureAwait(false))
                    {
                        var asset = await this.CopyAssetAsync(sourceClient, sourceClientConfiguration, targetClient, targetClientConfiguration, provisioningRequest, logger).ConfigureAwait(false);
                        provisioningCompletedEventModel.AddMediaServiceAccountName(target);
                    }
                }
            }

            logger.LogInformation($"AssetDataProvisioningService::ProvisionAsync completed: provisioningRequest={LogHelper.FormatObjectForLog(provisioningRequest)}");
        }

        private async Task<Asset> CopyAssetAsync(IAzureMediaServicesClient sourceClient, MediaServiceConfigurationModel sourceConfig,
                                                  IAzureMediaServicesClient targetClient, MediaServiceConfigurationModel targetConfig,
                                                  ProvisioningRequestModel provisioningRequest, ILogger logger)
        {
            logger.LogInformation($"AssetDataProvisioningService::CopyAssetAsync started: provisioningRequest={LogHelper.FormatObjectForLog(provisioningRequest)} sourceInstanceName={sourceConfig.AccountName} targetInstanceName={targetConfig.AccountName}");

            var targetAsset = await targetClient.Assets.GetAsync(targetConfig.ResourceGroup, targetConfig.AccountName, provisioningRequest.EncodedAssetName).ConfigureAwait(false);

            if (targetAsset == null)
            {
                targetAsset = await targetClient.Assets.CreateOrUpdateAsync(targetConfig.ResourceGroup, targetConfig.AccountName, provisioningRequest.EncodedAssetName, new Asset()).ConfigureAwait(false);
                // TBD to verify 
                // need to reload asset to get Container value populated, otherwise Container is null after asset creation
                targetAsset = await targetClient.Assets.GetAsync(targetConfig.ResourceGroup, targetConfig.AccountName, provisioningRequest.EncodedAssetName).ConfigureAwait(false);
            }

            var sourceAssetContainerSas = await sourceClient.Assets.ListContainerSasAsync(
               sourceConfig.ResourceGroup,
               sourceConfig.AccountName,
               provisioningRequest.EncodedAssetName,
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
                        throw new Exception($"Copy operation failed, sourceAccount={sourceConfig.AccountName} targetAccount={targetConfig.AccountName} assetName={provisioningRequest.EncodedAssetName} blobName={blobItem.Name} httpStatus={copyResult.GetRawResponse().Status}");
                    }
                }));
            }

            await Task.WhenAll(copyTasks).ConfigureAwait(false);
            logger.LogInformation($"AssetDataProvisioningService::CopyAssetAsync completed: provisioningRequest={LogHelper.FormatObjectForLog(provisioningRequest)} sourceInstanceName={sourceConfig.AccountName} targetInstanceName={targetConfig.AccountName} numberOfFiles={copyTasks.Count}");

            return targetAsset;
        }
    }
}
