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

    public class ClearStreamingProvisioningService : IProvisioningService
    {
        private readonly IConfigService configService;

        public ClearStreamingProvisioningService(IConfigService configService)
        {
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        public async Task ProvisionAsync(StreamProvisioningRequestModel streamProvisioningRequest, ILogger logger)
        {
            logger.LogInformation($"ClearStreamingProvisioningService::ProvisionAsync started: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)}");

            if (!this.configService.MediaServiceInstanceConfiguration.ContainsKey(streamProvisioningRequest.EncodedAssetMediaServiceAccountName))
            {
                throw new Exception($"ClearStreamingProvisioningService::ProvisionAsync does not have configuration for account={streamProvisioningRequest.EncodedAssetMediaServiceAccountName}");
            }
            var sourceClientConfiguration = this.configService.MediaServiceInstanceConfiguration[streamProvisioningRequest.EncodedAssetMediaServiceAccountName];

            using (var sourceClient = await MediaServicesHelper.CreateMediaServicesClientAsync(sourceClientConfiguration).ConfigureAwait(false))
            {
                var sourceLocator = new StreamingLocator(assetName: streamProvisioningRequest.EncodedAssetName, streamingPolicyName: PredefinedStreamingPolicy.ClearStreamingOnly);
                sourceLocator = await this.ProvisionLocatorAsync(sourceClient, sourceClientConfiguration, streamProvisioningRequest, sourceLocator, logger).ConfigureAwait(false);

                var targetInstances = this.configService.MediaServiceInstanceConfiguration.Keys.Where(i => !i.Equals(streamProvisioningRequest.EncodedAssetMediaServiceAccountName, StringComparison.InvariantCultureIgnoreCase));

                foreach (var target in targetInstances)
                {
                    var targetClientConfiguration = this.configService.MediaServiceInstanceConfiguration[target];
                    using (var targetClient = await MediaServicesHelper.CreateMediaServicesClientAsync(targetClientConfiguration).ConfigureAwait(false))
                    {
                        var targetLocator = new StreamingLocator(assetName: sourceLocator.AssetName, streamingPolicyName: sourceLocator.StreamingPolicyName, id: sourceLocator.Id, name: sourceLocator.Name, type: sourceLocator.Type, streamingLocatorId: sourceLocator.StreamingLocatorId);
                        targetLocator = await this.ProvisionLocatorAsync(targetClient, targetClientConfiguration, streamProvisioningRequest, targetLocator, logger).ConfigureAwait(false);
                    }
                }
            }

            logger.LogInformation($"ClearStreamingProvisioningService::ProvisionAsync completed: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)}");
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
    }
}
