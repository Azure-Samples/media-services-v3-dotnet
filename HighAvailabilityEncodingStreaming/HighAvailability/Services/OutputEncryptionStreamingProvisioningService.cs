// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Interfaces;
    using HighAvailability.Models;
    using Microsoft.Azure.Management.Media;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Implements clear key streaming locator provisioning to multiple Azure Media Services instances
    /// </summary>
    public class OutputEncryptionStreamingProvisioningService : StreamingProvisioningService, IProvisioningService
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
        public OutputEncryptionStreamingProvisioningService(IMediaServiceInstanceFactory mediaServiceInstanceFactory,
                                                    IConfigService configService)
        {
            this.mediaServiceInstanceFactory = mediaServiceInstanceFactory ?? throw new ArgumentNullException(nameof(mediaServiceInstanceFactory));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        /// <summary>
        /// Provisions clear key streaming locator in all Azure Media Services instances for a given processed asset. 
        /// This implementation is based on https://github.com/Azure-Samples/media-services-v3-dotnet-core-tutorials/tree/master/NETCore/EncodeHTTPAndPublishAESEncrypted
        /// </summary>
        /// <param name="provisioningRequest">Provisioning request associated with processed asset</param>
        /// <param name="provisioningCompletedEventModel">Provision completed event model to store provisioning data</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns></returns>
        public async Task ProvisionAsync(ProvisioningRequestModel provisioningRequest, ProvisioningCompletedEventModel provisioningCompletedEventModel, ILogger logger)
        {
            logger.LogInformation($"OutputEncryptionStreamingProvisioningService::ProvisionAsync started: provisioningRequest={LogHelper.FormatObjectForLog(provisioningRequest)}");

            // Make sure that account name that asset is provisioned exists in current configuration
            if (!this.configService.MediaServiceInstanceConfiguration.ContainsKey(provisioningRequest.ProcessedAssetMediaServiceAccountName))
            {
                throw new Exception($"OutputEncryptionStreamingProvisioningService::ProvisionAsync does not have configuration for account={provisioningRequest.ProcessedAssetMediaServiceAccountName}");
            }

            // Get source configuration that asset is provisioned as part of processing job
            var sourceClientConfiguration = this.configService.MediaServiceInstanceConfiguration[provisioningRequest.ProcessedAssetMediaServiceAccountName];

            // Create a custom streaming locator name, it has to differ from originally requested locator name to avoid name collision
            var streamingLocatorName = $"{provisioningRequest.StreamingLocatorName}-encrypted";

            // Get Azure Media Services instance client associated with provisioned asset
            var sourceClient = this.mediaServiceInstanceFactory.GetMediaServiceInstance(provisioningRequest.ProcessedAssetMediaServiceAccountName, logger);

            // Create locator for the source instance
            var sourceLocator = new StreamingLocator(
                assetName: provisioningRequest.ProcessedAssetName,
                streamingPolicyName: PredefinedStreamingPolicy.ClearKey,
                defaultContentKeyPolicyName: this.configService.ContentKeyPolicyName);

            // Provision created locator
            sourceLocator = await ProvisionLocatorAsync(
                sourceClient,
                sourceClientConfiguration,
                provisioningRequest.ProcessedAssetName,
                streamingLocatorName,
                sourceLocator,
                logger).ConfigureAwait(false);

            // Record the fact that locator was created
            provisioningCompletedEventModel.AddClearKeyStreamingLocators(sourceLocator);

            // List all content keys
            var sourceContentKeysResponse = await sourceClient.StreamingLocators.ListContentKeysAsync(sourceClientConfiguration.ResourceGroup, sourceClientConfiguration.AccountName, streamingLocatorName).ConfigureAwait(false);

            var keyIdentifier = sourceContentKeysResponse.ContentKeys.First().Id.ToString();

            // Generate primary URL for streaming, it includes token to decrypt content
            provisioningCompletedEventModel.PrimaryUrl = await this.GenerateStreamingUrl(sourceClient, sourceClientConfiguration, streamingLocatorName, keyIdentifier).ConfigureAwait(false);

            // Create a list of Azure Media Services instances that locator needs to be provisioned. It should be all instances listed in configuration, except source instance
            var targetInstances = this.configService.MediaServiceInstanceConfiguration.Keys.Where(
                                    i => !i.Equals(provisioningRequest.ProcessedAssetMediaServiceAccountName, StringComparison.InvariantCultureIgnoreCase));

            // Iterate through the list of all Azure Media Services instance names that locator needs to be provisioned to
            foreach (var target in targetInstances)
            {
                // Get target configuration
                var targetClientConfiguration = this.configService.MediaServiceInstanceConfiguration[target];

                // Get client associated with target instance
                var targetClient = this.mediaServiceInstanceFactory.GetMediaServiceInstance(target, logger);

                // Create locator for target instance
                var targetLocator = new StreamingLocator(
                    assetName: sourceLocator.AssetName,
                    streamingPolicyName: sourceLocator.StreamingPolicyName,
                    id: sourceLocator.Id,
                    name: sourceLocator.Name,
                    type: sourceLocator.Type,
                    streamingLocatorId: sourceLocator.StreamingLocatorId,
                    defaultContentKeyPolicyName: sourceLocator.DefaultContentKeyPolicyName,
                    contentKeys: sourceContentKeysResponse.ContentKeys);

                // Provision created locator
                targetLocator = await ProvisionLocatorAsync(
                    targetClient,
                    targetClientConfiguration,
                    provisioningRequest.ProcessedAssetName,
                    streamingLocatorName,
                    targetLocator,
                    logger).ConfigureAwait(false);

                // Record fact that locator was provisioned to target instance
                provisioningCompletedEventModel.AddClearKeyStreamingLocators(targetLocator);
            }

            logger.LogInformation($"OutputEncryptionStreamingProvisioningService::ProvisionAsync completed: provisioningRequest={LogHelper.FormatObjectForLog(provisioningRequest)}");
        }

        /// <summary>
        /// Generates streaming locator using Azure Front Door URL
        /// This implementation is based on https://github.com/Azure-Samples/media-services-v3-dotnet-core-tutorials/tree/master/NETCore/EncodeHTTPAndPublishAESEncrypted
        /// </summary>
        /// <param name="client">Azure Media Services instance client</param>
        /// <param name="config">Azure Media Services instance configuration</param>
        /// <param name="locatorName">locator name</param>
        /// <param name="keyIdentifier">key identifier</param>
        /// <returns></returns>
        private async Task<string> GenerateStreamingUrl(IAzureMediaServicesClient client, MediaServiceConfigurationModel config, string locatorName, string keyIdentifier)
        {
            // Get token to access content
            var token = MediaServicesHelper.GetToken(this.configService.TokenIssuer, this.configService.TokenAudience, keyIdentifier, this.configService.GetClearKeyStreamingKey());

            // Get list of all paths associated with specific locator
            var paths = await client.StreamingLocators.ListPathsAsync(config.ResourceGroup, config.AccountName, locatorName).ConfigureAwait(false);

            // Create Dash URL
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
                        var dashPath = uriBuilder.ToString();
                        return $"https://ampdemo.azureedge.net/?url={dashPath}&aes=true&aestoken=Bearer%3D{token}";
                    }
                }
            }

            return null;
        }
    }
}
