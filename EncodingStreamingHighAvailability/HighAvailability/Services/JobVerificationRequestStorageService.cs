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

    public class JobVerificationRequestStorageService : IJobVerificationRequestStorageService
    {
        private readonly QueueClient queue;
        private readonly JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

        public JobVerificationRequestStorageService(QueueClient queue)
        {
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        public async Task<JobVerificationRequestModel> CreateAsync(JobVerificationRequestModel jobVerificationRequestModel, TimeSpan verificationDelay, ILogger logger)
        {
            if (jobVerificationRequestModel == null)
            {
                throw new ArgumentNullException(nameof(jobVerificationRequestModel));
            }

            var message = JsonConvert.SerializeObject(jobVerificationRequestModel, this.settings);
            await this.queue.SendMessageAsync(QueueServiceHelper.EncodeToBase64(message), verificationDelay).ConfigureAwait(false);
            logger.LogInformation($"JobVerificationRequestStorageService::CreateAsync successfully added request to the queue: jobVerificationRequestModel={LogHelper.FormatObjectForLog(jobVerificationRequestModel)} verificationDelay={verificationDelay}");
            return jobVerificationRequestModel;
        }

        public async Task<JobVerificationRequestModel?> GetNextAsync(ILogger logger)
        {
            var messages = await this.queue.ReceiveMessagesAsync(maxMessages: 1).ConfigureAwait(false);
            var message = messages.Value.FirstOrDefault();

            if (message != null)
            {
                var decodedMessage = QueueServiceHelper.DecodeFromBase64(message.MessageText);
                var jobVerificationRequestModel = JsonConvert.DeserializeObject<JobVerificationRequestModel>(decodedMessage, this.settings);
                await this.queue.DeleteMessageAsync(message.MessageId, message.PopReceipt).ConfigureAwait(false);
                logger.LogInformation($"JobVerificationRequestStorageService::GetNextAsync request successfully dequeued from the queue: jobVerificationRequestModel={LogHelper.FormatObjectForLog(jobVerificationRequestModel)}");
                return jobVerificationRequestModel;
            }

            return null;
        }
    }
}
