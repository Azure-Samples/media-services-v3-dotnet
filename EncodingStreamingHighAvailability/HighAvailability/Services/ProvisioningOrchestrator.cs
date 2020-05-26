namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Interfaces;
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class ProvisioningOrchestrator : IProvisioningOrchestrator
    {
        private readonly IList<IProvisioningService> provisioningServices;
        private readonly IProvisioningCompletedEventStorageService provisioningCompletedEventStorageService;

        public ProvisioningOrchestrator(IList<IProvisioningService> provisioningServices, IProvisioningCompletedEventStorageService provisioningCompletedEventStorageService)
        {
            this.provisioningServices = provisioningServices ?? throw new ArgumentNullException(nameof(provisioningServices));
            this.provisioningCompletedEventStorageService = provisioningCompletedEventStorageService ?? throw new ArgumentNullException(nameof(provisioningCompletedEventStorageService));
        }

        public async Task ProvisionAsync(ProvisioningRequestModel provisioningRequestModel, ILogger logger)
        {
            logger.LogInformation($"ProvisioningOrchestrator::ProvisionAsync started: provisioningRequestModel={LogHelper.FormatObjectForLog(provisioningRequestModel)}");

            var provisioningCompletedEventModel = new ProvisioningCompletedEventModel
            {
                Id = Guid.NewGuid().ToString(),
                AssetName = provisioningRequestModel.EncodedAssetName
            };

            foreach (var service in this.provisioningServices)
            {
                await service.ProvisionAsync(provisioningRequestModel, provisioningCompletedEventModel, logger).ConfigureAwait(false);
            }

            await this.provisioningCompletedEventStorageService.CreateAsync(provisioningCompletedEventModel, logger).ConfigureAwait(false);

            logger.LogInformation($"ProvisioningOrchestrator::ProvisionAsync completed: provisioningRequestModel={LogHelper.FormatObjectForLog(provisioningRequestModel)}");
        }
    }
}
