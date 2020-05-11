namespace HighAvailability.JobScheduler
{
    using Azure.Storage.Queues;
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using HighAvailability.Services;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System;
    using System.Threading.Tasks;

    public static class JobSchedulerFunction
    {
        private static IConfigService? configService;
        private static TableStorageService? mediaServiceInstanceHealthTableStorageService;
        private static QueueClient? jobVerificationRequestQueue;
        private static JsonSerializerSettings jsonSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
        private static readonly object configLock = new object();
        private static bool configLoaded = false;

        public static async Task Initialize()
        {
            var keyVaultName = Environment.GetEnvironmentVariable("KeyVaultName");
            if (keyVaultName == null)
            {
                throw new Exception("keyVaultName is not set");
            }

            configService = new ConfigService(keyVaultName);
            await configService.LoadConfigurationAsync().ConfigureAwait(false);

            var tableStorageAccount = CloudStorageAccount.Parse(configService.TableStorageAccountConnectionString);
            var tableClient = tableStorageAccount.CreateCloudTableClient();
            var mediaServiceInstanceHealthTable = tableClient.GetTableReference(configService.MediaServiceInstanceHealthTableName);
            await mediaServiceInstanceHealthTable.CreateIfNotExistsAsync().ConfigureAwait(false);
            mediaServiceInstanceHealthTableStorageService = new TableStorageService(mediaServiceInstanceHealthTable);

            jobVerificationRequestQueue = new QueueClient(configService.StorageAccountConnectionString, configService.JobVerificationRequestQueueName);
            await jobVerificationRequestQueue.CreateIfNotExistsAsync().ConfigureAwait(false);
        }


        [FunctionName("JobSchedulerFunction")]
        public static async Task Run([QueueTrigger("job-requests", Connection = "StorageAccountConnectionString")]string message, ILogger logger)
        {
            try
            {
                lock (configLock)
                {
                    if (!configLoaded)
                    {
                        Initialize().Wait();
                        configLoaded = true;
                    }
                }

                if (mediaServiceInstanceHealthTableStorageService == null)
                {
                    throw new Exception("mediaServiceInstanceHealthTableStorageService is null");
                }

                if (jobVerificationRequestQueue == null)
                {
                    throw new Exception("jobVerificationRequestQueue is null");
                }

                if (configService == null)
                {
                    throw new Exception("configService is null");
                }

                logger.LogInformation($"JobSchedulerFunction::Run triggered, message={message}");
                var jobRequestModel = JsonConvert.DeserializeObject<JobRequestModel>(message, jsonSettings);
                var mediaServiceInstanceHealthStorageService = new MediaServiceInstanceHealthStorageService(mediaServiceInstanceHealthTableStorageService, logger);
                var mediaServiceInstanceHealthService = new MediaServiceInstanceHealthService(mediaServiceInstanceHealthStorageService, logger);
                var jobVerificationRequesetStorageService = new JobVerificationRequestStorageService(jobVerificationRequestQueue, logger);
                var jobSchedulerService = new JobSchedulerService(mediaServiceInstanceHealthService, jobVerificationRequesetStorageService, configService, logger);

                var result = await jobSchedulerService.SubmitJobAsync(jobRequestModel).ConfigureAwait(false);

                logger.LogInformation($"JobSchedulerFunction::Run completed, result={LogHelper.FormatObjectForLog(result)}");
            }
            catch (Exception e)
            {
                logger.LogError($"JobSchedulerFunction::Run failed, exception={e.Message} message={message}");
                throw;
            }
        }
    }
}
