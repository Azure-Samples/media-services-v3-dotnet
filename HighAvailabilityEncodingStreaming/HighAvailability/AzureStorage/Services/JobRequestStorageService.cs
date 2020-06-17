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
    /// Implements methods to store and get job requests using Azure Queue
    /// </summary>
    public class JobRequestStorageService : IJobRequestStorageService
    {
        /// <summary>
        /// Azure Queue client
        /// </summary>
        private readonly QueueClient queue;

        /// <summary>
        /// Need to include full type names in serialization/deserialization since some of the types are derived
        /// </summary>
        private readonly JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="queue">Azure Queue client</param>
        public JobRequestStorageService(QueueClient queue)
        {
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        /// <summary>
        /// Stores a new job request
        /// </summary>
        /// <param name="jobRequestModel">Job request to store</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns></returns>
        public async Task<JobRequestModel> CreateAsync(JobRequestModel jobRequestModel, ILogger logger)
        {
            var message = JsonConvert.SerializeObject(jobRequestModel, this.settings);
            // Encode message to Base64 before sending to the queue
            await this.queue.SendMessageAsync(QueueServiceHelper.EncodeToBase64(message)).ConfigureAwait(false);
            logger.LogInformation($"JobRequestStorageService::CreateAsync successfully added request to the queue: jobRequestModel={LogHelper.FormatObjectForLog(jobRequestModel)}");
            return jobRequestModel;
        }

        /// <summary>
        /// Gets next available job request.
        /// </summary>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Job request from the storage</returns>
        public async Task<JobRequestModel> GetNextAsync(ILogger logger)
        {
            var messages = await this.queue.ReceiveMessagesAsync(maxMessages: 1).ConfigureAwait(false);
            var message = messages.Value.FirstOrDefault();

            if (message != null)
            {
                // All message are encoded base64 on Azure Queue, decode first
                var decodedMessage = QueueServiceHelper.DecodeFromBase64(message.MessageText);
                var jobRequestModel = JsonConvert.DeserializeObject<JobRequestModel>(decodedMessage, this.settings);
                // delete message from the queue
                await this.queue.DeleteMessageAsync(message.MessageId, message.PopReceipt).ConfigureAwait(false);
                logger.LogInformation($"JobRequestStorageService::GetNextAsync request successfully dequeued from the queue: jobRequestModel={LogHelper.FormatObjectForLog(jobRequestModel)}");
                return jobRequestModel;
            }

            return null;
        }
    }
}
