#pragma warning disable CA1707 // Identifiers should not contain underscores
namespace job_status_function
#pragma warning restore CA1707 // Identifiers should not contain underscores
{
    using Azure.Storage.Queues;
    using media_services_high_availability_shared.Helpers;
    using media_services_high_availability_shared.Services;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Azure.EventGrid.Models;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.EventGrid;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    public static class JobStatusFunction
    {
        private static IConfigService? configService;
        private static CloudTable? mediaServiceInstanceHealthTable;
        private static CloudTable? jobStatusTable;
        private static QueueClient? streamProvisioningRequestQueue;
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

            mediaServiceInstanceHealthTable = tableClient.GetTableReference(configService.MediaServiceInstanceHealthTableName);
            await mediaServiceInstanceHealthTable.CreateIfNotExistsAsync().ConfigureAwait(false);

            jobStatusTable = tableClient.GetTableReference(configService.JobStatusTableName);
            await jobStatusTable.CreateIfNotExistsAsync().ConfigureAwait(false);

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

#pragma warning disable CA1303 // Do not pass literals as localized parameters
                if (jobStatusTable == null)
                {
                    throw new Exception("jobStatusTable is null");
                }

                if (streamProvisioningRequestQueue == null)
                {
                    throw new Exception("streamProvisioningRequestQueue is null");
                }

                if (mediaServiceInstanceHealthTable == null)
                {
                    throw new Exception("mediaServiceInstanceHealthTable is null");
                }

                if (configService == null)
                {
                    throw new Exception("configService is null");
                }
#pragma warning restore CA1303 // Do not pass literals as localized parameters

                if (eventGridEvent == null)
                {
                    throw new ArgumentNullException(nameof(eventGridEvent));
                }

                logger.LogInformation($"JobStatusFunction::Run triggered: message={LogHelper.FormatObjectForLog(eventGridEvent)}");
                var jobStatusStorageService = new JobStatusStorageService(jobStatusTable, logger);
                var mediaServiceInstanceHealthStorageService = new MediaServiceInstanceHealthStorageService(mediaServiceInstanceHealthTable, logger);
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
