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

    public class ProvisioningCompletedEventStorageService : IProvisioningCompletedEventStorageService
    {
        private readonly QueueClient queue;

        public ProvisioningCompletedEventStorageService(QueueClient queue)
        {
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        public async Task<ProvisioningCompletedEventModel> CreateAsync(ProvisioningCompletedEventModel provisioningCompletedEventModel, ILogger logger)
        {
            var message = JsonConvert.SerializeObject(provisioningCompletedEventModel);
            await this.queue.SendMessageAsync(QueueServiceHelper.EncodeToBase64(message)).ConfigureAwait(false);
            logger.LogInformation($"ProvisioningCompletedEventStorageService::CreateAsync successfully added request to the queue: provisioningCompletedEventModel={LogHelper.FormatObjectForLog(provisioningCompletedEventModel)}");

            return provisioningCompletedEventModel;
        }

        public async Task<ProvisioningCompletedEventModel> GetNextAsync(ILogger logger)
        {
            var messages = await this.queue.ReceiveMessagesAsync(maxMessages: 1).ConfigureAwait(false);
            var message = messages.Value.FirstOrDefault();

            if (message != null)
            {
                var decodedMessage = QueueServiceHelper.DecodeFromBase64(message.MessageText);
                var provisioningCompletedEventModel = JsonConvert.DeserializeObject<ProvisioningCompletedEventModel>(decodedMessage);
                logger.LogInformation($"ProvisioningCompletedEventStorageService::GetNextAsync request successfully dequeued from the queue: provisioningCompletedEventModel={LogHelper.FormatObjectForLog(provisioningCompletedEventModel)}");
                await this.queue.DeleteMessageAsync(message.MessageId, message.PopReceipt).ConfigureAwait(false);
                return provisioningCompletedEventModel;
            }

            return null;
        }
    }
}
