namespace HighAvailability.AzureStorage.Services
{
    using Azure.Storage.Queues;
    using HighAvailability.Helpers;
    using HighAvailability.Interfaces;
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public class ProvisioningRequestStorageService : IProvisioningRequestStorageService
    {
        private readonly QueueClient queue;

        public ProvisioningRequestStorageService(QueueClient queue)
        {
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        public async Task<ProvisioningRequestModel> CreateAsync(ProvisioningRequestModel provisioningRequest, ILogger logger)
        {
            var message = JsonConvert.SerializeObject(provisioningRequest);
            await this.queue.SendMessageAsync(QueueServiceHelper.EncodeToBase64(message)).ConfigureAwait(false);
            logger.LogInformation($"ProvisioningRequestStorageService::CreateAsync successfully added request to the queue: provisioningRequest={LogHelper.FormatObjectForLog(provisioningRequest)}");

            return provisioningRequest;
        }

        public async Task<ProvisioningRequestModel> GetNextAsync(ILogger logger)
        {
            var messages = await this.queue.ReceiveMessagesAsync(maxMessages: 1).ConfigureAwait(false);
            var message = messages.Value.FirstOrDefault();
            if (message != null)
            {
                var decodedMessage = QueueServiceHelper.DecodeFromBase64(message.MessageText);
                var provisioningRequest = JsonConvert.DeserializeObject<ProvisioningRequestModel>(decodedMessage);
                await this.queue.DeleteMessageAsync(message.MessageId, message.PopReceipt).ConfigureAwait(false);
                logger.LogInformation($"ProvisioningRequestStorageService::GetNextAsync request successfully dequeued from the queue: provisioningRequest={LogHelper.FormatObjectForLog(provisioningRequest)}");
                return provisioningRequest;
            }

            return null;
        }
    }
}
