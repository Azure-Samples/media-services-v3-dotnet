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

    public class ClearKeyStreamingProvisioningService : StreamingProvisioningService, IProvisioningService
    {
        private readonly IConfigService configService;

        public ClearKeyStreamingProvisioningService(IConfigService configService)
        {
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        public async Task ProvisionAsync(ProvisioningRequestModel provisioningRequest, ILogger logger)
        {
            logger.LogInformation($"ClearKeyStreamingProvisioningService::ProvisionAsync started: provisioningRequest={LogHelper.FormatObjectForLog(provisioningRequest)}");

            if (!this.configService.MediaServiceInstanceConfiguration.ContainsKey(provisioningRequest.EncodedAssetMediaServiceAccountName))
            {
                throw new Exception($"ClearKeyStreamingProvisioningService::ProvisionAsync does not have configuration for account={provisioningRequest.EncodedAssetMediaServiceAccountName}");
            }
            var sourceClientConfiguration = this.configService.MediaServiceInstanceConfiguration[provisioningRequest.EncodedAssetMediaServiceAccountName];

            var streamingLocatorName = $"{provisioningRequest.StreamingLocatorName}-encrypted";

            using (var sourceClient = await MediaServicesHelper.CreateMediaServicesClientAsync(sourceClientConfiguration).ConfigureAwait(false))
            {
                var sourceLocator = new StreamingLocator(assetName: provisioningRequest.EncodedAssetName, streamingPolicyName: PredefinedStreamingPolicy.ClearKey, defaultContentKeyPolicyName: configService.ContentKeyPolicyName);
                sourceLocator = await ProvisionLocatorAsync(sourceClient, sourceClientConfiguration, provisioningRequest.EncodedAssetName, streamingLocatorName, sourceLocator, logger).ConfigureAwait(false);

                var sourceContentKeysResponse = await sourceClient.StreamingLocators.ListContentKeysAsync(sourceClientConfiguration.ResourceGroup, sourceClientConfiguration.AccountName, streamingLocatorName).ConfigureAwait(false);
                string keyIdentifier = sourceContentKeysResponse.ContentKeys.First().Id.ToString();

                //  var sourceContentKeyPolicyProperties = await sourceClient.ContentKeyPolicies.GetPolicyPropertiesWithSecretsAsync(sourceClientConfiguration.ResourceGroup, sourceClientConfiguration.AccountName, sourceLocator.DefaultContentKeyPolicyName).ConfigureAwait(false);
                var streamUrl = await this.GenerateStreamingUrl(sourceClient, sourceClientConfiguration, streamingLocatorName, keyIdentifier).ConfigureAwait(false);

                var targetInstances = this.configService.MediaServiceInstanceConfiguration.Keys.Where(i => !i.Equals(provisioningRequest.EncodedAssetMediaServiceAccountName, StringComparison.InvariantCultureIgnoreCase));

                foreach (var target in targetInstances)
                {
                    var targetClientConfiguration = this.configService.MediaServiceInstanceConfiguration[target];
                    using (var targetClient = await MediaServicesHelper.CreateMediaServicesClientAsync(targetClientConfiguration).ConfigureAwait(false))
                    {
                        var targetLocator = new StreamingLocator(assetName: sourceLocator.AssetName, streamingPolicyName: sourceLocator.StreamingPolicyName, id: sourceLocator.Id, name: sourceLocator.Name, type: sourceLocator.Type, streamingLocatorId: sourceLocator.StreamingLocatorId, defaultContentKeyPolicyName: sourceLocator.DefaultContentKeyPolicyName, contentKeys: sourceContentKeysResponse.ContentKeys);
                        targetLocator = await ProvisionLocatorAsync(targetClient, targetClientConfiguration, provisioningRequest.EncodedAssetName, streamingLocatorName, targetLocator, logger).ConfigureAwait(false);
                    }
                }
            }

            logger.LogInformation($"ClearKeyStreamingProvisioningService::ProvisionAsync completed: provisioningRequest={LogHelper.FormatObjectForLog(provisioningRequest)}");
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
