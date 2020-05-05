namespace media_services_high_availability_shared.Services
{
    using Azure.Storage.Queues;
    using media_services_high_availability_shared.Helpers;
    using media_services_high_availability_shared.Models;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public class StreamProvisioningRequestStorageService : IStreamProvisioningRequestStorageService
    {
        private readonly QueueClient queue;
        private readonly ILogger logger;

        public StreamProvisioningRequestStorageService(QueueClient queue, ILogger logger)
        {
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<StreamProvisioningRequestModel> CreateAsync(StreamProvisioningRequestModel streamProvisioningRequest)
        {
            if (streamProvisioningRequest == null)
            {
                throw new ArgumentNullException(nameof(streamProvisioningRequest));
            }

            var message = JsonConvert.SerializeObject(streamProvisioningRequest);
            await this.queue.SendMessageAsync(QueueServiceHelper.EncodeToBase64(message)).ConfigureAwait(false);
            this.logger.LogInformation($"StreamProvisioningRequestStorageService::CreateAsync successfully added request to the queue: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)}");

            return streamProvisioningRequest;
        }

        public async Task<StreamProvisioningRequestModel?> GetNextAsync()
        {
            var messages = await this.queue.ReceiveMessagesAsync(maxMessages: 1).ConfigureAwait(false);
            var message = messages.Value.FirstOrDefault();
            if (message != null)
            {
                var decodedMessage = QueueServiceHelper.DecodeFromBase64(message.MessageText);
                var streamProvisioningRequest = JsonConvert.DeserializeObject<StreamProvisioningRequestModel>(decodedMessage);
                await this.queue.DeleteMessageAsync(message.MessageId, message.PopReceipt).ConfigureAwait(false);
                this.logger.LogInformation($"StreamProvisioningRequestStorageService::GetNextAsync request successfully dequeued from the queue: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)}");
                return streamProvisioningRequest;
            }

            return null;
        }
    }
}
