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

    public class ClearStreamingProvisioningService : StreamingProvisioningService, IProvisioningService
    {
        private readonly IConfigService configService;

        public ClearStreamingProvisioningService(IConfigService configService)
        {
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        public async Task ProvisionAsync(ProvisioningRequestModel provisioningRequest, ProvisioningCompletedEventModel provisioningCompletedEventModel, ILogger logger)
        {
            logger.LogInformation($"ClearStreamingProvisioningService::ProvisionAsync started: provisioningRequest={LogHelper.FormatObjectForLog(provisioningRequest)}");

            if (!this.configService.MediaServiceInstanceConfiguration.ContainsKey(provisioningRequest.EncodedAssetMediaServiceAccountName))
            {
                throw new Exception($"ClearStreamingProvisioningService::ProvisionAsync does not have configuration for account={provisioningRequest.EncodedAssetMediaServiceAccountName}");
            }
            var sourceClientConfiguration = this.configService.MediaServiceInstanceConfiguration[provisioningRequest.EncodedAssetMediaServiceAccountName];

            using (var sourceClient = await MediaServicesHelper.CreateMediaServicesClientAsync(sourceClientConfiguration).ConfigureAwait(false))
            {
                var sourceLocator = new StreamingLocator(assetName: provisioningRequest.EncodedAssetName, streamingPolicyName: PredefinedStreamingPolicy.ClearStreamingOnly);
                sourceLocator = await ProvisionLocatorAsync(sourceClient, sourceClientConfiguration, provisioningRequest.EncodedAssetName, provisioningRequest.StreamingLocatorName, sourceLocator, logger).ConfigureAwait(false);
                provisioningCompletedEventModel.AddClearStreamingLocators(sourceLocator);

                var targetInstances = this.configService.MediaServiceInstanceConfiguration.Keys.Where(i => !i.Equals(provisioningRequest.EncodedAssetMediaServiceAccountName, StringComparison.InvariantCultureIgnoreCase));

                foreach (var target in targetInstances)
                {
                    var targetClientConfiguration = this.configService.MediaServiceInstanceConfiguration[target];
                    using (var targetClient = await MediaServicesHelper.CreateMediaServicesClientAsync(targetClientConfiguration).ConfigureAwait(false))
                    {
                        var targetLocator = new StreamingLocator(assetName: sourceLocator.AssetName, streamingPolicyName: sourceLocator.StreamingPolicyName, id: sourceLocator.Id, name: sourceLocator.Name, type: sourceLocator.Type, streamingLocatorId: sourceLocator.StreamingLocatorId);
                        targetLocator = await ProvisionLocatorAsync(targetClient, targetClientConfiguration, provisioningRequest.EncodedAssetName, provisioningRequest.StreamingLocatorName, targetLocator, logger).ConfigureAwait(false);
                        provisioningCompletedEventModel.AddClearStreamingLocators(targetLocator);
                    }
                }
            }

            logger.LogInformation($"ClearStreamingProvisioningService::ProvisionAsync completed: provisioningRequest={LogHelper.FormatObjectForLog(provisioningRequest)}");
        }
    }
}
