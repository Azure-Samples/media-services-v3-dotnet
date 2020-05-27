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

    /// <summary>
    /// Implements methods to store and get provisioning requests using Azure Queue.
    /// </summary>
    public class ProvisioningRequestStorageService : IProvisioningRequestStorageService
    {
        /// <summary>
        /// Azure Queue client.
        /// </summary>
        private readonly QueueClient queue;

        public ProvisioningRequestStorageService(QueueClient queue)
        {
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        public async Task<ProvisioningRequestModel> CreateAsync(ProvisioningRequestModel provisioningRequest, ILogger logger)
        {
            var message = JsonConvert.SerializeObject(provisioningRequest);
            // Encode message to Base64 before sending to the queue
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
                // All message are encoded base64 on Azure Queue, decode first
                var decodedMessage = QueueServiceHelper.DecodeFromBase64(message.MessageText);
                var provisioningRequest = JsonConvert.DeserializeObject<ProvisioningRequestModel>(decodedMessage);
                // delete message from the queue
                await this.queue.DeleteMessageAsync(message.MessageId, message.PopReceipt).ConfigureAwait(false);
                logger.LogInformation($"ProvisioningRequestStorageService::GetNextAsync request successfully dequeued from the queue: provisioningRequest={LogHelper.FormatObjectForLog(provisioningRequest)}");
                return provisioningRequest;
            }

            return null;
        }
    }
}
