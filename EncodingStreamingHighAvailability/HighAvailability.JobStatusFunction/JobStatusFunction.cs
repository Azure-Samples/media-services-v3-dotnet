namespace HighAvailability.JobStatusFunction
{
    using Azure.Storage.Queues;
    using HighAvailability.Helpers;
    using HighAvailability.Services;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Azure.EventGrid.Models;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.EventGrid;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    public static class AzureFunction
    {
        private static IConfigService? configService;
        private static TableStorageService? mediaServiceInstanceHealthTableStorageService;
        private static TableStorageService? jobStatusTableStorageService;
        private static QueueClient? streamProvisioningRequestQueue;
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

            var jobStatusTable = tableClient.GetTableReference(configService.JobStatusTableName);
            await jobStatusTable.CreateIfNotExistsAsync().ConfigureAwait(false);
            jobStatusTableStorageService = new TableStorageService(jobStatusTable);

            streamProvisioningRequestQueue = new QueueClient(configService.StorageAccountConnectionString, configService.StreamProvisioningRequestQueueName);
            await streamProvisioningRequestQueue.CreateIfNotExistsAsync().ConfigureAwait(false);
        }

        [FunctionName("JobStatusFunction")]
        public static async void Run([EventGridTrigger]EventGridEvent eventGridEvent, ILogger logger)
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

                if (jobStatusTableStorageService == null)
                {
                    throw new Exception("jobStatusTableStorageService is null");
                }

                if (streamProvisioningRequestQueue == null)
                {
                    throw new Exception("streamProvisioningRequestQueue is null");
                }

                if (mediaServiceInstanceHealthTableStorageService == null)
                {
                    throw new Exception("mediaServiceInstanceHealthTableStorageService is null");
                }

                if (configService == null)
                {
                    throw new Exception("configService is null");
                }

                if (eventGridEvent == null)
                {
                    throw new ArgumentNullException(nameof(eventGridEvent));
                }

                logger.LogInformation($"JobStatusFunction::Run triggered: message={LogHelper.FormatObjectForLog(eventGridEvent)}");
                var jobStatusStorageService = new JobStatusStorageService(jobStatusTableStorageService, logger);
                var mediaServiceInstanceHealthStorageService = new MediaServiceInstanceHealthStorageService(mediaServiceInstanceHealthTableStorageService, logger);
                var mediaServiceInstanceHealthService = new MediaServiceInstanceHealthService(mediaServiceInstanceHealthStorageService, logger);
                var streamProvisioningRequestStorageService = new StreamProvisioningRequestStorageService(streamProvisioningRequestQueue, logger);
                var jobStatusService = new JobStatusService(mediaServiceInstanceHealthService, jobStatusStorageService, streamProvisioningRequestStorageService, logger);
                var eventGridService = new EventGridService(logger);

                var jobStatusModel = eventGridService.ParseEventData(eventGridEvent);
                if (jobStatusModel != null)
                {
                    var result = await jobStatusService.ProcessJobStatusAsync(jobStatusModel).ConfigureAwait(false);
                    logger.LogInformation($"JobStatusFunction::Run completed: result={LogHelper.FormatObjectForLog(result)}");
                }
                else
                {
                    logger.LogInformation($"JobStatusFunction::Run event data skipped: result={LogHelper.FormatObjectForLog(eventGridEvent)}");
                }
            }
            catch (Exception e)
            {
                logger.LogError($"JobSchedulerFunction::Run failed: exception={e.Message} eventGridEvent={LogHelper.FormatObjectForLog(eventGridEvent)}");
                throw;
            }
        }
    }
}
