namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using Microsoft.Azure.Management.Media;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    public class StreamingProvisioningService
    {
        protected static async Task<StreamingLocator> ProvisionLocatorAsync(IAzureMediaServicesClient client, MediaServiceConfigurationModel config, string assetName, string locatorName, StreamingLocator locatorToProvision, ILogger logger)
        {
            logger.LogInformation($"StreamingProvisioningService::ProvisionLocatorAsync started: instanceName={config.AccountName} assetName={assetName} locatorName={locatorName}");

            var locator = await client.StreamingLocators.GetAsync(config.ResourceGroup, config.AccountName, locatorName).ConfigureAwait(false);

            if (locator != null && !locator.AssetName.Equals(assetName, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new Exception($"Locator already exists with incorrect asset name, accountName={config.AccountName} locatorName={locator.Name} existingAssetNane={locator.AssetName} requestedAssetName={assetName}");
            }

            if (locator == null)
            {
                locator = await client.StreamingLocators.CreateAsync(config.ResourceGroup, config.AccountName, locatorName, locatorToProvision).ConfigureAwait(false);
                logger.LogInformation($"StreamingProvisioningService::ProvisionLocatorAsync new locator provisioned: locator={LogHelper.FormatObjectForLog(locator)}");
            }

            logger.LogInformation($"StreamingProvisioningService::ProvisionLocatorAsync completed: instanceName={config.AccountName} assetName={assetName} locatorName={locatorName} locator={LogHelper.FormatObjectForLog(locator)}");

            return locator;
        }
    }
}
