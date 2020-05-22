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
                sourceLocator = await ProvisionLocatorAsync(sourceClient, sourceClientConfiguration, streamProvisioningRequest.EncodedAssetName, streamProvisioningRequest.StreamingLocatorName, sourceLocator, logger).ConfigureAwait(false);

                var targetInstances = this.configService.MediaServiceInstanceConfiguration.Keys.Where(i => !i.Equals(streamProvisioningRequest.EncodedAssetMediaServiceAccountName, StringComparison.InvariantCultureIgnoreCase));

                foreach (var target in targetInstances)
                {
                    var targetClientConfiguration = this.configService.MediaServiceInstanceConfiguration[target];
                    using (var targetClient = await MediaServicesHelper.CreateMediaServicesClientAsync(targetClientConfiguration).ConfigureAwait(false))
                    {
                        var targetLocator = new StreamingLocator(assetName: sourceLocator.AssetName, streamingPolicyName: sourceLocator.StreamingPolicyName, id: sourceLocator.Id, name: sourceLocator.Name, type: sourceLocator.Type, streamingLocatorId: sourceLocator.StreamingLocatorId);
                        targetLocator = await ProvisionLocatorAsync(targetClient, targetClientConfiguration, streamProvisioningRequest.EncodedAssetName, streamProvisioningRequest.StreamingLocatorName, targetLocator, logger).ConfigureAwait(false);
                    }
                }
            }

            logger.LogInformation($"ClearStreamingProvisioningService::ProvisionAsync completed: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)}");
        }
    }
}
