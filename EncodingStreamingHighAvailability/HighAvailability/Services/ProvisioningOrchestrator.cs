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

        public async Task ProvisionAsync(StreamProvisioningRequestModel request, ILogger logger)
        {
            logger.LogInformation($"ProvisioningOrchestrator::ProvisionAsync started: streamProvisioningRequest={LogHelper.FormatObjectForLog(request)}");

            foreach (var service in this.provisioningServices)
            {
                await service.ProvisionAsync(request, logger).ConfigureAwait(false);
            }

            logger.LogInformation($"ProvisioningOrchestrator::ProvisionAsync completed: streamProvisioningRequest={LogHelper.FormatObjectForLog(request)}");
        }
    }
}
