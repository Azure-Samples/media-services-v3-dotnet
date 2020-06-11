namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Interfaces;
    using HighAvailability.Models;
    using Microsoft.Azure.Management.Media;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// This class implements common logic for provisioning service implementations
    /// </summary>
    public class StreamingProvisioningService
    {
        /// <summary>
        /// Storage service to persist status of all calls to Media Services APIs
        /// </summary>
        protected IMediaServiceCallHistoryStorageService mediaServiceCallHistoryStorageService { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mediaServiceCallHistoryStorageService">Service to store Media Services call history</param>
        public StreamingProvisioningService(IMediaServiceCallHistoryStorageService mediaServiceCallHistoryStorageService)
        {
            this.mediaServiceCallHistoryStorageService = mediaServiceCallHistoryStorageService ?? throw new ArgumentNullException(nameof(mediaServiceCallHistoryStorageService));
        }

        /// <summary>
        /// Provisions created locator to specific Azure Media Services instance
        /// </summary>
        /// <param name="client">Azure Media Services instance client</param>
        /// <param name="config">Azure Media Services instance config</param>
        /// <param name="assetName">Asset name associated with locator</param>
        /// <param name="locatorName">Locator name</param>
        /// <param name="locatorToProvision">Locator object</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns></returns>
        protected async Task<StreamingLocator> ProvisionLocatorAsync(IAzureMediaServicesClient client, MediaServiceConfigurationModel config, string assetName, string locatorName, StreamingLocator locatorToProvision, ILogger logger)
        {
            logger.LogInformation($"StreamingProvisioningService::ProvisionLocatorAsync started: instanceName={config.AccountName} assetName={assetName} locatorName={locatorName}");

            // Check if locator already exists
            var locator = await MediaServicesHelper.CallAzureMediaServices(
                async () =>
                {
                    return await client.StreamingLocators.GetWithHttpMessagesAsync(config.ResourceGroup, config.AccountName, locatorName).ConfigureAwait(false);
                },
                locatorToProvision,
                config.AccountName,
                this.mediaServiceCallHistoryStorageService,
                "StreamingLocators.GetWithHttpMessagesAsync",
                logger).ConfigureAwait(false);

            // if locator exists, but associated with different asset, throw. This should only happen if locators are created outside from this automated process
            if (locator != null && !locator.AssetName.Equals(assetName, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new Exception($"Locator already exists with incorrect asset name, accountName={config.AccountName} locatorName={locator.Name} existingAssetName={locator.AssetName} requestedAssetName={assetName}");
            }

            // locator does not exists, need to create new one
            if (locator == null)
            {
                locator = await MediaServicesHelper.CallAzureMediaServices(
                    async () =>
                    {
                        return await client.StreamingLocators.CreateWithHttpMessagesAsync(
                            config.ResourceGroup,
                            config.AccountName,
                            locatorName,
                            locatorToProvision).ConfigureAwait(false);
                    },
                    locatorToProvision,
                    config.AccountName,
                    this.mediaServiceCallHistoryStorageService,
                    "StreamingLocators.CreateWithHttpMessagesAsync",
                    logger).ConfigureAwait(false);

                logger.LogInformation($"StreamingProvisioningService::ProvisionLocatorAsync new locator provisioned: locator={LogHelper.FormatObjectForLog(locator)}");
            }

            logger.LogInformation($"StreamingProvisioningService::ProvisionLocatorAsync completed: instanceName={config.AccountName} assetName={assetName} locatorName={locatorName} locator={LogHelper.FormatObjectForLog(locator)}");

            return locator;
        }
    }
}
