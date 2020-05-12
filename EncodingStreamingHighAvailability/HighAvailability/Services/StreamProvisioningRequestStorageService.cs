namespace HighAvailability.Services
{
    using Azure.Storage.Queues;
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public class StreamProvisioningRequestStorageService : IStreamProvisioningRequestStorageService
    {
        private readonly QueueClient queue;

        public StreamProvisioningRequestStorageService(QueueClient queue)
        {
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        public async Task<StreamProvisioningRequestModel> CreateAsync(StreamProvisioningRequestModel streamProvisioningRequest, ILogger logger)
        {
            if (streamProvisioningRequest == null)
            {
                throw new ArgumentNullException(nameof(streamProvisioningRequest));
            }

            var message = JsonConvert.SerializeObject(streamProvisioningRequest);
            await this.queue.SendMessageAsync(QueueServiceHelper.EncodeToBase64(message)).ConfigureAwait(false);
            logger.LogInformation($"StreamProvisioningRequestStorageService::CreateAsync successfully added request to the queue: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)}");

            return streamProvisioningRequest;
        }

        public async Task<StreamProvisioningRequestModel?> GetNextAsync(ILogger logger)
        {
            var messages = await this.queue.ReceiveMessagesAsync(maxMessages: 1).ConfigureAwait(false);
            var message = messages.Value.FirstOrDefault();
            if (message != null)
            {
                var decodedMessage = QueueServiceHelper.DecodeFromBase64(message.MessageText);
                var streamProvisioningRequest = JsonConvert.DeserializeObject<StreamProvisioningRequestModel>(decodedMessage);
                await this.queue.DeleteMessageAsync(message.MessageId, message.PopReceipt).ConfigureAwait(false);
                logger.LogInformation($"StreamProvisioningRequestStorageService::GetNextAsync request successfully dequeued from the queue: streamProvisioningRequest={LogHelper.FormatObjectForLog(streamProvisioningRequest)}");
                return streamProvisioningRequest;
            }

            return null;
        }
    }
}
