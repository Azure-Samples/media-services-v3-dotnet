namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Interfaces;
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Implements logic to provision encoded assets. It supports multiple provisioning services that are called for a specific asset.
    /// </summary>
    public class ProvisioningOrchestrator : IProvisioningOrchestrator
    {
        /// <summary>
        /// List of provisioning services.
        /// </summary>
        private readonly IList<IProvisioningService> provisioningServices;

        /// <summary>
        /// Storage service to persist provisioning completed event.
        /// </summary>
        private readonly IProvisioningCompletedEventStorageService provisioningCompletedEventStorageService;

        public ProvisioningOrchestrator(IList<IProvisioningService> provisioningServices, IProvisioningCompletedEventStorageService provisioningCompletedEventStorageService)
        {
            this.provisioningServices = provisioningServices ?? throw new ArgumentNullException(nameof(provisioningServices));
            this.provisioningCompletedEventStorageService = provisioningCompletedEventStorageService ?? throw new ArgumentNullException(nameof(provisioningCompletedEventStorageService));
        }

        public async Task ProvisionAsync(ProvisioningRequestModel provisioningRequestModel, ILogger logger)
        {
            logger.LogInformation($"ProvisioningOrchestrator::ProvisionAsync started: provisioningRequestModel={LogHelper.FormatObjectForLog(provisioningRequestModel)}");

            // Create provisioning completed event and start filling out data
            var provisioningCompletedEventModel = new ProvisioningCompletedEventModel
            {
                Id = Guid.NewGuid().ToString(),
                AssetName = provisioningRequestModel.EncodedAssetName
            };

            // Iterate through all available provisioning services and let each service handle provisioning request.
            // Services are called in the same order as in the list, in most cases that is important, provisioning services may have dependency on each other
            foreach (var service in this.provisioningServices)
            {
                // provisioningCompletedEventModel is updated with each call
                await service.ProvisionAsync(provisioningRequestModel, provisioningCompletedEventModel, logger).ConfigureAwait(false);
            }

            // Provisioning is done, store event
            await this.provisioningCompletedEventStorageService.CreateAsync(provisioningCompletedEventModel, logger).ConfigureAwait(false);

            logger.LogInformation($"ProvisioningOrchestrator::ProvisionAsync completed: provisioningRequestModel={LogHelper.FormatObjectForLog(provisioningRequestModel)}");
        }
    }
}
