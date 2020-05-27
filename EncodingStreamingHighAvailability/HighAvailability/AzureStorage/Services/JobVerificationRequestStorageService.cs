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
    /// Implements methods to store and get job verification requests using Azure Queue
    /// </summary>
    public class JobVerificationRequestStorageService : IJobVerificationRequestStorageService
    {
        /// <summary>
        /// Azure Queue client
        /// </summary>
        private readonly QueueClient queue;

        /// <summary>
        /// Need to include full type names in serialization/deserialization since some of the types are derived
        /// </summary>
        private readonly JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

        public JobVerificationRequestStorageService(QueueClient queue)
        {
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        public async Task<JobVerificationRequestModel> CreateAsync(JobVerificationRequestModel jobVerificationRequestModel, TimeSpan verificationDelay, ILogger logger)
        {
            var message = JsonConvert.SerializeObject(jobVerificationRequestModel, this.settings);
            // Encode message to Base64 before sending to the queue
            await this.queue.SendMessageAsync(QueueServiceHelper.EncodeToBase64(message), verificationDelay).ConfigureAwait(false);
            logger.LogInformation($"JobVerificationRequestStorageService::CreateAsync successfully added request to the queue: jobVerificationRequestModel={LogHelper.FormatObjectForLog(jobVerificationRequestModel)} verificationDelay={verificationDelay}");
            return jobVerificationRequestModel;
        }

        public async Task<JobVerificationRequestModel> GetNextAsync(ILogger logger)
        {
            var messages = await this.queue.ReceiveMessagesAsync(maxMessages: 1).ConfigureAwait(false);
            var message = messages.Value.FirstOrDefault();

            if (message != null)
            {
                // All message are encoded base64 on Azure Queue, decode first
                var decodedMessage = QueueServiceHelper.DecodeFromBase64(message.MessageText);
                var jobVerificationRequestModel = JsonConvert.DeserializeObject<JobVerificationRequestModel>(decodedMessage, this.settings);
                // delete message from the queue
                await this.queue.DeleteMessageAsync(message.MessageId, message.PopReceipt).ConfigureAwait(false);
                logger.LogInformation($"JobVerificationRequestStorageService::GetNextAsync request successfully dequeued from the queue: jobVerificationRequestModel={LogHelper.FormatObjectForLog(jobVerificationRequestModel)}");
                return jobVerificationRequestModel;
            }

            return null;
        }
    }
}
