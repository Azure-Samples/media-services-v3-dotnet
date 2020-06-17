// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
    /// Implements methods to store and get provisioning completed events using Azure Queue
    /// </summary>
    public class ProvisioningCompletedEventStorageService : IProvisioningCompletedEventStorageService
    {
        /// <summary>
        /// Azure Queue client
        /// </summary>
        private readonly QueueClient queue;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="queue">Azure Queue client</param>
        public ProvisioningCompletedEventStorageService(QueueClient queue)
        {
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        /// <summary>
        /// Stores provisioning completed event.
        /// </summary>
        /// <param name="provisioningCompletedEventModel">Event to store</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Stored provisioning completed event</returns>
        public async Task<ProvisioningCompletedEventModel> CreateAsync(ProvisioningCompletedEventModel provisioningCompletedEventModel, ILogger logger)
        {
            var message = JsonConvert.SerializeObject(provisioningCompletedEventModel);
            // Encode message to Base64 before sending to the queue
            await this.queue.SendMessageAsync(QueueServiceHelper.EncodeToBase64(message)).ConfigureAwait(false);
            logger.LogInformation($"ProvisioningCompletedEventStorageService::CreateAsync successfully added request to the queue: provisioningCompletedEventModel={LogHelper.FormatObjectForLog(provisioningCompletedEventModel)}");

            return provisioningCompletedEventModel;
        }

        /// <summary>
        /// Gets next provisioning completed event from storage
        /// </summary>
        /// <param name="logger"></param>
        /// <returns>Provisioning completed event</returns>
        public async Task<ProvisioningCompletedEventModel> GetNextAsync(ILogger logger)
        {
            var messages = await this.queue.ReceiveMessagesAsync(maxMessages: 1).ConfigureAwait(false);
            var message = messages.Value.FirstOrDefault();

            if (message != null)
            {
                // All message are encoded base64 on Azure Queue, decode first
                var decodedMessage = QueueServiceHelper.DecodeFromBase64(message.MessageText);
                var provisioningCompletedEventModel = JsonConvert.DeserializeObject<ProvisioningCompletedEventModel>(decodedMessage);
                logger.LogInformation($"ProvisioningCompletedEventStorageService::GetNextAsync request successfully dequeued from the queue: provisioningCompletedEventModel={LogHelper.FormatObjectForLog(provisioningCompletedEventModel)}");
                // delete message from the queue
                await this.queue.DeleteMessageAsync(message.MessageId, message.PopReceipt).ConfigureAwait(false);
                return provisioningCompletedEventModel;
            }

            return null;
        }
    }
}
