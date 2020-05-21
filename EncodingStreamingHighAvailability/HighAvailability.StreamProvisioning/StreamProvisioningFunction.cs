namespace HighAvailabikity.StreamProvisioning
{
    using HighAvailability.Models;
    using HighAvailability.Services;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System;
    using System.Threading.Tasks;

    public class StreamProvisioningFunction
    {
        private IProvisioningOrchestrator provisioningOrchestrator { get; set; }

        public StreamProvisioningFunction(IProvisioningOrchestrator provisioningOrchestrator)
        {
            this.provisioningOrchestrator = provisioningOrchestrator ?? throw new ArgumentNullException(nameof(provisioningOrchestrator));
        }

        [FunctionName("StreamProvisioningFunction")]
        public async Task Run([QueueTrigger("stream-provisioning-requests", Connection = "StorageAccountConnectionString")] string message, ILogger logger)
        {
            try
            {
                logger.LogInformation($"StreamProvisioningFunction::Run triggered, message={message}");

                var streamProvisioningRequestModel = JsonConvert.DeserializeObject<StreamProvisioningRequestModel>(message);
                await this.provisioningOrchestrator.ProvisionAsync(streamProvisioningRequestModel, logger).ConfigureAwait(false);

                logger.LogInformation($"StreamProvisioningFunction::Run completed, message={message}");
            }
            catch (Exception e)
            {
                logger.LogError($"StreamProvisioningFunction::Run failed: exception={e.Message} message={message}");
                throw;
            }
        }
    }
}
