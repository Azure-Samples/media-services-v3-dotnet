namespace HighAvailabikity.Provisioner
{
    using HighAvailability.Models;
    using HighAvailability.Services;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System;
    using System.Threading.Tasks;

    public class ProvisioningFunction
    {
        private IProvisioningOrchestrator provisioningOrchestrator { get; set; }

        public ProvisioningFunction(IProvisioningOrchestrator provisioningOrchestrator)
        {
            this.provisioningOrchestrator = provisioningOrchestrator ?? throw new ArgumentNullException(nameof(provisioningOrchestrator));
        }

        [FunctionName("ProvisioningFunction")]
        public async Task Run([QueueTrigger("stream-provisioning-requests", Connection = "StorageAccountConnectionString")] string message, ILogger logger)
        {
            try
            {
                logger.LogInformation($"ProvisioningFunction::Run triggered, message={message}");

                var provisioningRequestModel = JsonConvert.DeserializeObject<ProvisioningRequestModel>(message);
                await this.provisioningOrchestrator.ProvisionAsync(provisioningRequestModel, logger).ConfigureAwait(false);

                logger.LogInformation($"ProvisioningFunction::Run completed, message={message}");
            }
            catch (Exception e)
            {
                logger.LogError($"ProvisioningFunction::Run failed: exception={e.Message} message={message}");
                throw;
            }
        }
    }
}
