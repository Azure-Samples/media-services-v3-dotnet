namespace HighAvailability.Services
{
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using Microsoft.Azure.Management.Media;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    public class StreamProvisioningService : IStreamProvisioningService
    {
        private readonly IStreamProvisioningEventStorageService streamProvisioningEventStorageService;
        private readonly IConfigService configService;
        private readonly ILogger logger;

        public StreamProvisioningService(IStreamProvisioningEventStorageService streamProvisioningEventStorageService, IConfigService configService, ILogger logger)
        {
            this.streamProvisioningEventStorageService = streamProvisioningEventStorageService ?? throw new ArgumentNullException(nameof(streamProvisioningEventStorageService));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ProvisionStreamAsync(StreamProvisioningRequestModel streamProvisioningRequest)
        {
            if (streamProvisioningRequest == null)
            {
                throw new ArgumentNullException(nameof(streamProvisioningRequest));
            }

            this.logger.LogInformation($"StreamProvisioningService::ProvisionStreamAsync started: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)}");

            if (!this.configService.MediaServiceInstanceConfiguration.ContainsKey(streamProvisioningRequest.EncodedAssetMediaServiceAccountName))
            {
                throw new Exception($"StreamProvisioningService::ProvisionStreamAsync does not have configuration for account={streamProvisioningRequest.EncodedAssetMediaServiceAccountName}");
            }
            var sourceClientConfiguration = this.configService.MediaServiceInstanceConfiguration[streamProvisioningRequest.EncodedAssetMediaServiceAccountName];

            using (var sourceClient = await MediaServicesHelper.CreateMediaServicesClientAsync(sourceClientConfiguration).ConfigureAwait(false))
            {
                var locator = await this.ProvisionPrimaryLocatorAsync(sourceClient, sourceClientConfiguration, streamProvisioningRequest).ConfigureAwait(false);

                var streamProvisioningEventModel = await this.CreateStreamProvisioningEventModelAsync(sourceClient, sourceClientConfiguration, locator, streamProvisioningRequest).ConfigureAwait(false);
                var streamProvisioningEventResult = await this.streamProvisioningEventStorageService.CreateAsync(streamProvisioningEventModel).ConfigureAwait(false);
                this.logger.LogInformation($"StreamProvisioningService::ProvisionStreamAsync created stream provisining event: result={LogHelper.FormatObjectForLog(streamProvisioningEventResult)}");

                var targetInstances = this.configService.MediaServiceInstanceConfiguration.Keys.Where(i => !i.Equals(streamProvisioningRequest.EncodedAssetMediaServiceAccountName, StringComparison.InvariantCultureIgnoreCase));
                foreach (var target in targetInstances)
                {
                    var targetClientConfiguration = this.configService.MediaServiceInstanceConfiguration[target];
                    using (var targetClient = await MediaServicesHelper.CreateMediaServicesClientAsync(targetClientConfiguration).ConfigureAwait(false))
                    {
                        var asset = await this.CopyAssetAsync(sourceClient, sourceClientConfiguration, targetClient, targetClientConfiguration, streamProvisioningRequest, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).ConfigureAwait(false);
                        var targetLocator = await this.ProvisionSecondaryLocatorAsync(targetClient, targetClientConfiguration, locator).ConfigureAwait(false);
                    }
                }
                this.logger.LogInformation($"StreamProvisioningService::ProvisionStreamAsync completed: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)}");
            }
        }

        private async Task<StreamProvisioningEventModel> CreateStreamProvisioningEventModelAsync(IAzureMediaServicesClient client, MediaServiceConfigurationModel config, StreamingLocator locator, StreamProvisioningRequestModel streamProvisioningRequest)
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

            this.logger.LogInformation($"StreamProvisioningService::CreateStreamProvisioningEventModelAsync completed: streamProvisioningEventModel={LogHelper.FormatObjectForLog(result)}");

            return result;
        }

        private async Task<StreamingLocator> ProvisionPrimaryLocatorAsync(IAzureMediaServicesClient client, MediaServiceConfigurationModel config, StreamProvisioningRequestModel streamProvisioningRequest)
        {
            this.logger.LogInformation($"StreamProvisioningService::ProvisionPrimaryLocatorAsync started: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)} instanceName={config.AccountName}");
            var existingLocators = await client.StreamingLocators.ListAsync(config.ResourceGroup, config.AccountName).ConfigureAwait(false);
            var locator = existingLocators.FirstOrDefault(i => i.Name.Equals(streamProvisioningRequest.StreamingLocatorName, StringComparison.InvariantCultureIgnoreCase) && i.AssetName.Equals(streamProvisioningRequest.EncodedAssetName, StringComparison.InvariantCultureIgnoreCase));

            if (locator == null)
            {
                locator = new StreamingLocator(assetName: streamProvisioningRequest.EncodedAssetName, streamingPolicyName: PredefinedStreamingPolicy.ClearStreamingOnly);
                locator = await client.StreamingLocators.CreateAsync(config.ResourceGroup, config.AccountName, streamProvisioningRequest.StreamingLocatorName, locator).ConfigureAwait(false);
                this.logger.LogInformation($"StreamProvisioningService::ProvisionPrimaryLocatorAsync new locator provisioned: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)} locator={LogHelper.FormatObjectForLog(locator)}");
            }

            this.logger.LogInformation($"StreamProvisioningService::ProvisionPrimaryLocatorAsync completed: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)} locator={LogHelper.FormatObjectForLog(locator)}");

            return locator;
        }

        private async Task<StreamingLocator> ProvisionSecondaryLocatorAsync(IAzureMediaServicesClient client, MediaServiceConfigurationModel config, StreamingLocator sourceLocator)
        {
            this.logger.LogInformation($"StreamProvisioningService::ProvisionSecondaryLocatorAsync started: sourceLocator={LogHelper.FormatObjectForLog(sourceLocator)} instanceName={config.AccountName}");
            var existingLocators = await client.StreamingLocators.ListAsync(config.ResourceGroup, config.AccountName).ConfigureAwait(false);
            var locator = existingLocators.FirstOrDefault(i => i.Name.Equals(sourceLocator.Name, StringComparison.InvariantCultureIgnoreCase) && i.AssetName.Equals(sourceLocator.AssetName, StringComparison.InvariantCultureIgnoreCase));

            if (locator == null)
            {
                locator = new StreamingLocator(assetName: sourceLocator.AssetName, streamingPolicyName: sourceLocator.StreamingPolicyName, id: sourceLocator.Id, name: sourceLocator.Name, type: sourceLocator.Type, streamingLocatorId: sourceLocator.StreamingLocatorId);
                locator = await client.StreamingLocators.CreateAsync(config.ResourceGroup, config.AccountName, locator.Name, locator).ConfigureAwait(false);
                this.logger.LogInformation($"StreamProvisioningService::ProvisionSecondaryLocatorAsync new locator provisioned: sourceLocator={LogHelper.FormatObjectForLog(sourceLocator)} locator={LogHelper.FormatObjectForLog(locator)}");
            }

            this.logger.LogInformation($"StreamProvisioningService::ProvisionSecondaryLocatorAsync completed: sourceLocator={LogHelper.FormatObjectForLog(sourceLocator)} locator={LogHelper.FormatObjectForLog(locator)}");

            return locator;
        }

        private async Task<Asset> CopyAssetAsync(IAzureMediaServicesClient sourceClient, MediaServiceConfigurationModel sourceConfig,
                                                        IAzureMediaServicesClient targetClient, MediaServiceConfigurationModel targetConfig,
                                                        StreamProvisioningRequestModel streamProvisioningRequest, string tempFolder)
        {

            this.logger.LogInformation($"StreamProvisioningService::CopyAssetAsync started: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)} sourceInstanceName={sourceConfig.AccountName} targetInstanceName={targetConfig.AccountName}");

            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, true);
            }

            Directory.CreateDirectory(tempFolder);

            var existingAssets = await targetClient.Assets.ListAsync(targetConfig.ResourceGroup, targetConfig.AccountName).ConfigureAwait(false);
            var targetAsset = existingAssets.FirstOrDefault(i => i.Name.Equals(streamProvisioningRequest.EncodedAssetName, StringComparison.InvariantCultureIgnoreCase));

            if (targetAsset == null)
            {
                targetAsset = await targetClient.Assets.CreateOrUpdateAsync(targetConfig.ResourceGroup, targetConfig.AccountName, streamProvisioningRequest.EncodedAssetName, new Asset()).ConfigureAwait(false);
            }
            var sourceAssetContainerSas = await sourceClient.Assets.ListContainerSasAsync(
                sourceConfig.ResourceGroup,
                sourceConfig.AccountName,
                streamProvisioningRequest.EncodedAssetName,
                permissions: AssetContainerPermission.Read,
                expiryTime: DateTime.UtcNow.AddHours(1).ToUniversalTime()).ConfigureAwait(false);

            var sourceContainerSasUrl = new Uri(sourceAssetContainerSas.AssetContainerSasUrls.FirstOrDefault());

            var targetAssetContainerSas = await targetClient.Assets.ListContainerSasAsync(
                targetConfig.ResourceGroup,
                targetConfig.AccountName,
                targetAsset.Name,
                permissions: AssetContainerPermission.ReadWrite,
                expiryTime: DateTime.UtcNow.AddHours(1).ToUniversalTime()).ConfigureAwait(false);

            var targetContainerSasUrl = new Uri(targetAssetContainerSas.AssetContainerSasUrls.FirstOrDefault());

            var sourceBlobClient = new BlobContainerClient(sourceContainerSasUrl);

            var loadTasks = new List<Task>();
            var contentTypeMapping = new Dictionary<string, string>();

            await foreach (var blobItem in sourceBlobClient.GetBlobsAsync())
            {
                contentTypeMapping.Add(blobItem.Name, blobItem.Properties.ContentType);
                loadTasks.Add(Task.Run(() =>
                {
                    var downloadPath = Path.Combine(tempFolder, blobItem.Name);
                    var blob = sourceBlobClient.GetBlobClient(blobItem.Name);
                    BlobDownloadInfo download = blob.Download();
                    using (var file = File.OpenWrite(downloadPath))
                    {
                        download.Content.CopyTo(file);
                    }
                }));
            }

            await Task.WhenAll(loadTasks).ConfigureAwait(false);
            this.logger.LogInformation($"StreamProvisioningService::CopyAssetAsync downloaded files to temp storage: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)} sourceInstanceName={sourceConfig.AccountName} targetInstanceName={targetConfig.AccountName} numberOfFiles={loadTasks.Count}");

            var targetBlobClient = new BlobContainerClient(targetContainerSasUrl);
            loadTasks.Clear();

            foreach (var filename in Directory.GetFiles(tempFolder))
            {
                loadTasks.Add(Task.Run(() =>
                {
                    var uploadFileName = Path.GetFileName(filename);
                    var blob = targetBlobClient.GetBlobClient(uploadFileName);
                    blob.Upload(filename, true);
                    blob.SetHttpHeaders(new BlobHttpHeaders { ContentType = contentTypeMapping[uploadFileName] });
                }));
            }

            await Task.WhenAll(loadTasks).ConfigureAwait(false);

            Directory.Delete(tempFolder, true);

            this.logger.LogInformation($"StreamProvisioningService::CopyAssetAsync completed: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)} sourceInstanceName={sourceConfig.AccountName} targetInstanceName={targetConfig.AccountName} numberOfFiles={loadTasks.Count}");

            return targetAsset;
        }
    }
}
