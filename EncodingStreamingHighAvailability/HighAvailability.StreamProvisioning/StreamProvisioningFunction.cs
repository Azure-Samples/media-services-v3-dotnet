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
        private IStreamProvisioningService streamProvisioningService { get; set; }

        public StreamProvisioningFunction(IStreamProvisioningService streamProvisioningService)
        {
            this.streamProvisioningService = streamProvisioningService ?? throw new ArgumentNullException(nameof(streamProvisioningService));
        }

        [FunctionName("StreamProvisioningFunction")]
        public async Task Run([QueueTrigger("stream-provisioning-requests", Connection = "StorageAccountConnectionString")]string message, ILogger logger)
        {
            try
            {
                logger.LogInformation($"StreamProvisioningFunction::Run triggered, message={message}");
                var streamProvisioningRequestModel = JsonConvert.DeserializeObject<StreamProvisioningRequestModel>(message);

                await this.streamProvisioningService.ProvisionStreamAsync(streamProvisioningRequestModel, logger).ConfigureAwait(false);

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
