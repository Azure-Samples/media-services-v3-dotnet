#pragma warning disable CA1707 // Identifiers should not contain underscores
namespace job_verification_function
#pragma warning restore CA1707 // Identifiers should not contain underscores
{
    using Azure.Storage.Queues;
    using media_services_high_availability_shared.Helpers;
    using media_services_high_availability_shared.Models;
    using media_services_high_availability_shared.Services;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System;
    using System.Threading.Tasks;

    public static class JobVerificationFunction
    {
        private static IConfigService? configService;
        private static CloudTable? mediaServiceInstanceHealthTable;
        private static CloudTable? jobStatusTable;
        private static QueueClient? streamProvisioningRequestQueue;
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
        private static readonly object configLock = new object();
        private static bool configLoaded = false;

        public static async Task Initialize()
        {
            var keyVaultName = Environment.GetEnvironmentVariable("KeyVaultName");
            if (keyVaultName == null)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("keyVaultName is not set");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }

            configService = new ConfigService(keyVaultName);
            await configService.LoadConfigurationAsync().ConfigureAwait(false);

            var tableStorageAccount = CloudStorageAccount.Parse(configService.TableStorageAccountConnectionString);
            var tableClient = tableStorageAccount.CreateCloudTableClient();

            // Create a table client for interacting with the table service 
            mediaServiceInstanceHealthTable = tableClient.GetTableReference(configService.MediaServiceInstanceHealthTableName);
            await mediaServiceInstanceHealthTable.CreateIfNotExistsAsync().ConfigureAwait(false);

            jobStatusTable = tableClient.GetTableReference(configService.JobStatusTableName);
            await jobStatusTable.CreateIfNotExistsAsync().ConfigureAwait(false);

            streamProvisioningRequestQueue = new QueueClient(configService.StorageAccountConnectionString, configService.StreamProvisioningRequestQueueName);
            await streamProvisioningRequestQueue.CreateIfNotExistsAsync().ConfigureAwait(false);
        }

        [FunctionName("JobVerificationFunction")]
        public static async Task Run([QueueTrigger("job-verification-requests", Connection = "JobVerificationQueueConnectionString")]string message, ILogger logger)
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

#pragma warning disable CA1303 // Do not pass literals as localized parameters
                if (mediaServiceInstanceHealthTable == null)
                {
                    throw new Exception("mediaServiceInstanceHealthTable is null");
                }

                if (jobStatusTable == null)
                {
                    throw new Exception("jobStatusTable is null");
                }

                if (streamProvisioningRequestQueue == null)
                {
                    throw new Exception("streamProvisioningRequestQueue is null");
                }

                if (configService == null)
                {
                    throw new Exception("configService is null");
                }
#pragma warning restore CA1303 // Do not pass literals as localized parameters

                logger.LogInformation($"JobVerificationFunction::Run triggered, message={message}");
                var jobVerificationRequestModel = JsonConvert.DeserializeObject<JobVerificationRequestModel>(message, jsonSettings);
                var mediaServiceInstanceHealthStorageService = new MediaServiceInstanceHealthStorageService(mediaServiceInstanceHealthTable, logger);
                var mediaServiceInstanceHealthService = new MediaServiceInstanceHealthService(mediaServiceInstanceHealthStorageService, logger);
                var jobStatusStorageService = new JobStatusStorageService(jobStatusTable, logger);
                var streamProvisioningRequestStorageService = new StreamProvisioningRequestStorageService(streamProvisioningRequestQueue, logger);
                var jobVerificationService = new JobVerificationService(mediaServiceInstanceHealthService, jobStatusStorageService, streamProvisioningRequestStorageService, configService, logger);

                var result = await jobVerificationService.VerifyJobAsync(jobVerificationRequestModel).ConfigureAwait(false);

                logger.LogInformation($"JobVerificationFunction::Run completed, result={LogHelper.FormatObjectForLog(result)}");
            }
            catch (Exception e)
            {
                logger.LogError($"JobVerificationFunction::Run failed: exception={e.Message} message={message}");
                throw;
            }
        }
    }
}
