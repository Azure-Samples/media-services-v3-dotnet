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

    public class StreamProvisioningEventStorageService : IStreamProvisioningEventStorageService
    {
        private readonly QueueClient queue;

        public StreamProvisioningEventStorageService(QueueClient queue)
        {
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        public async Task<StreamProvisioningEventModel> CreateAsync(StreamProvisioningEventModel streamProvisioningEventModel, ILogger logger)
        {
            if (streamProvisioningEventModel == null)
            {
                throw new ArgumentNullException(nameof(streamProvisioningEventModel));
            }

            var message = JsonConvert.SerializeObject(streamProvisioningEventModel);
            await this.queue.SendMessageAsync(QueueServiceHelper.EncodeToBase64(message)).ConfigureAwait(false);
            logger.LogInformation($"StreamProvisioningEventStorageService::CreateAsync successfully added request to the queue: streamProvisioningEventModel={LogHelper.FormatObjectForLog(streamProvisioningEventModel)}");

            return streamProvisioningEventModel;
        }

        public async Task<StreamProvisioningEventModel?> GetNextAsync(ILogger logger)
        {
            var messages = await this.queue.ReceiveMessagesAsync(maxMessages: 1).ConfigureAwait(false);
            var message = messages.Value.FirstOrDefault();
            if (message != null)
            {
                var decodedMessage = QueueServiceHelper.DecodeFromBase64(message.MessageText);
                var streamProvisioningEventModel = JsonConvert.DeserializeObject<StreamProvisioningEventModel>(decodedMessage);
                logger.LogInformation($"StreamProvisioningEventStorageService::GetNextAsync request successfully dequeued from the queue: streamProvisioningEventModel={LogHelper.FormatObjectForLog(streamProvisioningEventModel)}");
                await this.queue.DeleteMessageAsync(message.MessageId, message.PopReceipt).ConfigureAwait(false);
                return streamProvisioningEventModel;
            }

            return null;
        }
    }
}
