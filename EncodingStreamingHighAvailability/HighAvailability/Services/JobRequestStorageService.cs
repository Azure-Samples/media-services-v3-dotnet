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

    public class JobRequestStorageService : IJobRequestStorageService
    {
        private readonly QueueClient queue;
        private readonly ILogger logger;
        private readonly JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

        public JobRequestStorageService(QueueClient queue, ILogger logger)
        {
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<JobRequestModel> CreateAsync(JobRequestModel jobRequestModel)
        {
            if (jobRequestModel == null)
            {
                throw new ArgumentNullException(nameof(jobRequestModel));
            }

            var message = JsonConvert.SerializeObject(jobRequestModel, this.settings);
            await this.queue.SendMessageAsync(QueueServiceHelper.EncodeToBase64(message)).ConfigureAwait(false);
            this.logger.LogInformation($"JobRequestStorageService::CreateAsync successfully added request to the queue: jobRequestModel={LogHelper.FormatObjectForLog(jobRequestModel)}");
            return jobRequestModel;
        }

        public async Task<JobRequestModel?> GetNextAsync()
        {
            var messages = await this.queue.ReceiveMessagesAsync(maxMessages: 1).ConfigureAwait(false);
            var message = messages.Value.FirstOrDefault();

            if (message != null)
            {
                var decodedMessage = QueueServiceHelper.DecodeFromBase64(message.MessageText);
                var jobRequestModel = JsonConvert.DeserializeObject<JobRequestModel>(decodedMessage, this.settings);
                await this.queue.DeleteMessageAsync(message.MessageId, message.PopReceipt).ConfigureAwait(false);
                this.logger.LogInformation($"JobRequestStorageService::GetNextAsync request successfully dequeued from the queue: jobRequestModel={LogHelper.FormatObjectForLog(jobRequestModel)}");
                return jobRequestModel;
            }

            return null;
        }
    }
}
