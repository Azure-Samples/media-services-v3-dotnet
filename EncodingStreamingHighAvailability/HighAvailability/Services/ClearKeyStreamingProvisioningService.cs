namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using Microsoft.Azure.Management.Media;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public class ClearKeyStreamingProvisioningService : IProvisioningService
    {
        private readonly IConfigService configService;

        public ClearKeyStreamingProvisioningService(IConfigService configService)
        {
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        public async Task ProvisionAsync(StreamProvisioningRequestModel streamProvisioningRequest, ILogger logger)
        {
            logger.LogInformation($"ClearKeyStreamingProvisioningService::ProvisionAsync started: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)}");

            if (!this.configService.MediaServiceInstanceConfiguration.ContainsKey(streamProvisioningRequest.EncodedAssetMediaServiceAccountName))
            {
                throw new Exception($"ClearKeyStreamingProvisioningService::ProvisionAsync does not have configuration for account={streamProvisioningRequest.EncodedAssetMediaServiceAccountName}");
            }
            var sourceClientConfiguration = this.configService.MediaServiceInstanceConfiguration[streamProvisioningRequest.EncodedAssetMediaServiceAccountName];

            var streamingLocatorName = $"{streamProvisioningRequest.StreamingLocatorName}-encrypted";

            using (var sourceClient = await MediaServicesHelper.CreateMediaServicesClientAsync(sourceClientConfiguration).ConfigureAwait(false))
            {
                var sourceLocator = new StreamingLocator(assetName: streamProvisioningRequest.EncodedAssetName, streamingPolicyName: PredefinedStreamingPolicy.ClearKey, defaultContentKeyPolicyName: configService.ContentKeyPolicyName);
                sourceLocator = await this.ProvisionLocatorAsync(sourceClient, sourceClientConfiguration, streamProvisioningRequest, streamingLocatorName, sourceLocator, logger).ConfigureAwait(false);

                var sourceContentKeysResponse = await sourceClient.StreamingLocators.ListContentKeysAsync(sourceClientConfiguration.ResourceGroup, sourceClientConfiguration.AccountName, streamingLocatorName).ConfigureAwait(false);
                string keyIdentifier = sourceContentKeysResponse.ContentKeys.First().Id.ToString();

                //  var sourceContentKeyPolicyProperties = await sourceClient.ContentKeyPolicies.GetPolicyPropertiesWithSecretsAsync(sourceClientConfiguration.ResourceGroup, sourceClientConfiguration.AccountName, sourceLocator.DefaultContentKeyPolicyName).ConfigureAwait(false);
                var streamUrl = await this.GenerateStreamingUrl(sourceClient, sourceClientConfiguration, streamingLocatorName, keyIdentifier).ConfigureAwait(false);

                var targetInstances = this.configService.MediaServiceInstanceConfiguration.Keys.Where(i => !i.Equals(streamProvisioningRequest.EncodedAssetMediaServiceAccountName, StringComparison.InvariantCultureIgnoreCase));

                foreach (var target in targetInstances)
                {
                    var targetClientConfiguration = this.configService.MediaServiceInstanceConfiguration[target];
                    using (var targetClient = await MediaServicesHelper.CreateMediaServicesClientAsync(targetClientConfiguration).ConfigureAwait(false))
                    {
                        var targetLocator = new StreamingLocator(assetName: sourceLocator.AssetName, streamingPolicyName: sourceLocator.StreamingPolicyName, id: sourceLocator.Id, name: sourceLocator.Name, type: sourceLocator.Type, streamingLocatorId: sourceLocator.StreamingLocatorId, defaultContentKeyPolicyName: sourceLocator.DefaultContentKeyPolicyName, contentKeys: sourceContentKeysResponse.ContentKeys);
                        targetLocator = await this.ProvisionLocatorAsync(targetClient, targetClientConfiguration, streamProvisioningRequest, streamingLocatorName, targetLocator, logger).ConfigureAwait(false);
                    }
                }
            }

            logger.LogInformation($"ClearKeyStreamingProvisioningService::ProvisionAsync completed: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)}");
        }

        private async Task<StreamingLocator> ProvisionLocatorAsync(IAzureMediaServicesClient client, MediaServiceConfigurationModel config, StreamProvisioningRequestModel streamProvisioningRequest, string locatorName, StreamingLocator locatorToProvision, ILogger logger)
        {
            logger.LogInformation($"StreamProvisioningService::ProvisionLocatorAsync started: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)} instanceName={config.AccountName}");

            var locator = await client.StreamingLocators.GetAsync(config.ResourceGroup, config.AccountName, locatorName).ConfigureAwait(false);

            if (locator != null && !locator.AssetName.Equals(streamProvisioningRequest.EncodedAssetName, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new Exception($"Locator already exists with incorrect asset name, accountName={config.AccountName} locatorName={locator.Name} existingAssetNane={locator.AssetName} requestedAssetName={streamProvisioningRequest.EncodedAssetName}");
            }

            if (locator == null)
            {
                locator = await client.StreamingLocators.CreateAsync(config.ResourceGroup, config.AccountName, locatorName, locatorToProvision).ConfigureAwait(false);
                logger.LogInformation($"StreamProvisioningService::ProvisionLocatorAsync new locator provisioned: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)} locator={LogHelper.FormatObjectForLog(locator)}");
            }

            logger.LogInformation($"StreamProvisioningService::ProvisionLocatorAsync completed: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)} locator={LogHelper.FormatObjectForLog(locator)}");

            return locator;
        }

        private async Task<string> GenerateStreamingUrl(IAzureMediaServicesClient client, MediaServiceConfigurationModel config, string locatorName, string keyIdentifier)
        {
            var token = MediaServicesHelper.GetToken(configService.TokenIssuer, configService.TokenAudience, keyIdentifier, configService.GetClearKeyStreamingKey());

            var paths = await client.StreamingLocators.ListPathsAsync(config.ResourceGroup, config.AccountName, locatorName).ConfigureAwait(false);

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
