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

    public class StreamProvisioningService : IStreamProvisioningService
    {
        private readonly IStreamProvisioningEventStorageService streamProvisioningEventStorageService;
        private readonly IConfigService configService;

        public StreamProvisioningService(IStreamProvisioningEventStorageService streamProvisioningEventStorageService, IConfigService configService)
        {
            this.streamProvisioningEventStorageService = streamProvisioningEventStorageService ?? throw new ArgumentNullException(nameof(streamProvisioningEventStorageService));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        public async Task ProvisionStreamAsync(StreamProvisioningRequestModel streamProvisioningRequest, ILogger logger)
        {
            logger.LogInformation($"StreamProvisioningService::ProvisionStreamAsync started: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)}");

            if (!this.configService.MediaServiceInstanceConfiguration.ContainsKey(streamProvisioningRequest.EncodedAssetMediaServiceAccountName))
            {
                throw new Exception($"StreamProvisioningService::ProvisionStreamAsync does not have configuration for account={streamProvisioningRequest.EncodedAssetMediaServiceAccountName}");
            }
            var sourceClientConfiguration = this.configService.MediaServiceInstanceConfiguration[streamProvisioningRequest.EncodedAssetMediaServiceAccountName];

            using (var sourceClient = await MediaServicesHelper.CreateMediaServicesClientAsync(sourceClientConfiguration).ConfigureAwait(false))
            {
                var locator = new StreamingLocator(assetName: streamProvisioningRequest.EncodedAssetName, streamingPolicyName: PredefinedStreamingPolicy.ClearStreamingOnly);
                locator = await this.ProvisionLocatorAsync(sourceClient, sourceClientConfiguration, streamProvisioningRequest, locator, logger).ConfigureAwait(false);

                var targetInstances = this.configService.MediaServiceInstanceConfiguration.Keys.Where(i => !i.Equals(streamProvisioningRequest.EncodedAssetMediaServiceAccountName, StringComparison.InvariantCultureIgnoreCase));

                foreach (var target in targetInstances)
                {
                    var targetClientConfiguration = this.configService.MediaServiceInstanceConfiguration[target];
                    using (var targetClient = await MediaServicesHelper.CreateMediaServicesClientAsync(targetClientConfiguration).ConfigureAwait(false))
                    {
                        var asset = await this.CopyAssetAsync(sourceClient, sourceClientConfiguration, targetClient, targetClientConfiguration, streamProvisioningRequest, logger).ConfigureAwait(false);
                        var targetLocator = await this.ProvisionLocatorAsync(targetClient, targetClientConfiguration, streamProvisioningRequest, locator, logger).ConfigureAwait(false);
                    }
                }

                var streamProvisioningEventModel = await this.CreateStreamProvisioningEventModelAsync(sourceClient, sourceClientConfiguration, locator, streamProvisioningRequest, logger).ConfigureAwait(false);
                var streamProvisioningEventResult = await this.streamProvisioningEventStorageService.CreateAsync(streamProvisioningEventModel, logger).ConfigureAwait(false);

                logger.LogInformation($"StreamProvisioningService::ProvisionStreamAsync completed: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)} streamProvisioningEventResult={LogHelper.FormatObjectForLog(streamProvisioningEventResult)}");
            }
        }

        private async Task<StreamingLocator> ProvisionLocatorAsync(IAzureMediaServicesClient client, MediaServiceConfigurationModel config, StreamProvisioningRequestModel streamProvisioningRequest, StreamingLocator locatorToProvision, ILogger logger)
        {
            logger.LogInformation($"StreamProvisioningService::ProvisionLocatorAsync started: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)} instanceName={config.AccountName}");

            var locator = await client.StreamingLocators.GetAsync(config.ResourceGroup, config.AccountName, streamProvisioningRequest.StreamingLocatorName).ConfigureAwait(false);

            if (locator != null && !locator.AssetName.Equals(streamProvisioningRequest.EncodedAssetName, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new Exception($"Locator already exists with incorrect asset name, accountName={config.AccountName} locatorName={locator.Name} existingAssetNane={locator.AssetName} requestedAssetName={streamProvisioningRequest.EncodedAssetName}");
            }

            if (locator == null)
            {
                locator = await client.StreamingLocators.CreateAsync(config.ResourceGroup, config.AccountName, streamProvisioningRequest.StreamingLocatorName, locatorToProvision).ConfigureAwait(false);
                logger.LogInformation($"StreamProvisioningService::ProvisionLocatorAsync new locator provisioned: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)} locator={LogHelper.FormatObjectForLog(locator)}");
            }

            logger.LogInformation($"StreamProvisioningService::ProvisionLocatorAsync completed: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)} locator={LogHelper.FormatObjectForLog(locator)}");

            return locator;
        }

        private async Task<StreamProvisioningEventModel> CreateStreamProvisioningEventModelAsync(IAzureMediaServicesClient client, MediaServiceConfigurationModel config, StreamingLocator locator, StreamProvisioningRequestModel streamProvisioningRequest, ILogger logger)
        {
            var result = new StreamProvisioningEventModel
            {
                Id = Guid.NewGuid().ToString(),
                MediaServiceAccountName = config.AccountName,
                AssetName = streamProvisioningRequest.EncodedAssetName,
                StreamingLocatorName = locator.Name
            };

            var paths = await client.StreamingLocators.ListPathsAsync(config.ResourceGroup, config.AccountName, locator.Name).ConfigureAwait(false);

            for (var i = 0; i < paths.StreamingPaths.Count; i++)
            {
                var uriBuilder = new UriBuilder();
                uriBuilder.Scheme = "https";
                uriBuilder.Host = this.configService.FrontDoorHostName;
                if (paths.StreamingPaths[i].Paths.Count > 0)
                {
                    if (paths.StreamingPaths[i].StreamingProtocol == StreamingPolicyStreamingProtocol.Dash)
                    {
                        uriBuilder.Path = paths.StreamingPaths[i].Paths[0];
                        result.PrimaryUrl = uriBuilder.ToString();
                        break;
                    }
                }
            }

            logger.LogInformation($"StreamProvisioningService::CreateStreamProvisioningEventModelAsync completed: streamProvisioningEventModel={LogHelper.FormatObjectForLog(result)}");

            return result;
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
            logger.LogInformation($"StreamProvisioningService::CopyAssetAsync completed: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)} sourceInstanceName={sourceConfig.AccountName} targetInstanceName={targetConfig.AccountName} numberOfFiles={loadTasks.Count}");

            return targetAsset;
        }
    }
}
