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
    /// Implements clear streaming locator provisioning to multiple Azure Media Services instances
    /// </summary>
    public class ClearStreamingProvisioningService : StreamingProvisioningService, IProvisioningService
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
        public ClearStreamingProvisioningService(IMediaServiceInstanceFactory mediaServiceInstanceFactory,
                                                    IConfigService configService)
        {
            this.mediaServiceInstanceFactory = mediaServiceInstanceFactory ?? throw new ArgumentNullException(nameof(mediaServiceInstanceFactory));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        /// <summary>
        /// Provisions clear streaming locator in all Azure Media Services instances for a given processed asset. 
        /// </summary>
        /// <param name="provisioningRequest">Provisioning request associated with processed asset</param>
        /// <param name="provisioningCompletedEventModel">Provision completed event model to store provisioning data</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns></returns>
        public async Task ProvisionAsync(ProvisioningRequestModel provisioningRequest, ProvisioningCompletedEventModel provisioningCompletedEventModel, ILogger logger)
        {
            logger.LogInformation($"ClearStreamingProvisioningService::ProvisionAsync started: provisioningRequest={LogHelper.FormatObjectForLog(provisioningRequest)}");

            // Make sure that account name that asset is provisioned exists in current configuration
            if (!this.configService.MediaServiceInstanceConfiguration.ContainsKey(provisioningRequest.ProcessedAssetMediaServiceAccountName))
            {
                throw new Exception($"ClearStreamingProvisioningService::ProvisionAsync does not have configuration for account={provisioningRequest.ProcessedAssetMediaServiceAccountName}");
            }

            // Get source configuration that asset is provisioned as part of encoding job
            var sourceClientConfiguration = this.configService.MediaServiceInstanceConfiguration[provisioningRequest.ProcessedAssetMediaServiceAccountName];

            // Get Azure Media Services instance client associated with provisioned asset
            var sourceClient = this.mediaServiceInstanceFactory.GetMediaServiceInstance(provisioningRequest.ProcessedAssetMediaServiceAccountName, logger);

            // Create locator for the source instance
            var sourceLocator = new StreamingLocator(
                assetName: provisioningRequest.ProcessedAssetName,
                streamingPolicyName: PredefinedStreamingPolicy.ClearStreamingOnly);

            // Provision created locator
            sourceLocator = await ProvisionLocatorAsync(
                sourceClient,
                sourceClientConfiguration,
                provisioningRequest.ProcessedAssetName,
                provisioningRequest.StreamingLocatorName,
                sourceLocator,
                logger).ConfigureAwait(false);

            // Record the fact that locator was created
            provisioningCompletedEventModel.AddClearStreamingLocators(sourceLocator);

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
                    streamingLocatorId: sourceLocator.StreamingLocatorId);

                // Provision created locator
                targetLocator = await ProvisionLocatorAsync(
                    targetClient,
                    targetClientConfiguration,
                    provisioningRequest.ProcessedAssetName,
                    provisioningRequest.StreamingLocatorName,
                    targetLocator,
                    logger).ConfigureAwait(false);

                // Record fact that locator was provisioned to target instance
                provisioningCompletedEventModel.AddClearStreamingLocators(targetLocator);
            }

            logger.LogInformation($"ClearStreamingProvisioningService::ProvisionAsync completed: provisioningRequest={LogHelper.FormatObjectForLog(provisioningRequest)}");
        }
    }
}
