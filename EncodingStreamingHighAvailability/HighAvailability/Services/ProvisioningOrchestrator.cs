namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class ProvisioningOrchestrator : IProvisioningOrchestrator
    {
        private readonly IList<IProvisioningService> provisioningServices;

        public ProvisioningOrchestrator(IList<IProvisioningService> provisioningServices)
        {
            this.provisioningServices = provisioningServices ?? throw new ArgumentNullException(nameof(provisioningServices));
        }

        public async Task ProvisionAsync(ProvisioningRequestModel provisioningRequestModel, ILogger logger)
        {
            logger.LogInformation($"ProvisioningOrchestrator::ProvisionAsync started: provisioningRequestModel={LogHelper.FormatObjectForLog(provisioningRequestModel)}");

            foreach (var service in this.provisioningServices)
            {
                await service.ProvisionAsync(provisioningRequestModel, logger).ConfigureAwait(false);
            }

            logger.LogInformation($"ProvisioningOrchestrator::ProvisionAsync completed: provisioningRequestModel={LogHelper.FormatObjectForLog(provisioningRequestModel)}");
        }
    }
}
