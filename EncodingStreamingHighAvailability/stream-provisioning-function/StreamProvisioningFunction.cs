#pragma warning disable CA1707 // Identifiers should not contain underscores
namespace stream_provisioning_function
#pragma warning restore CA1707 // Identifiers should not contain underscores
{
    using Azure.Storage.Queues;
    using media_services_high_availability_shared.Models;
    using media_services_high_availability_shared.Services;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System;
    using System.Threading.Tasks;

    public static class StreamProvisioningFunction
    {
        private static IConfigService? configService;
        private static QueueClient? streamProvisioningEventQueue;
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

            streamProvisioningEventQueue = new QueueClient(configService.StorageAccountConnectionString, configService.StreamProvisioningEventQueueName);
            await streamProvisioningEventQueue.CreateIfNotExistsAsync().ConfigureAwait(false);
        }

        [FunctionName("StreamProvisioningFunction")]
        public static async Task Run([QueueTrigger("stream-provisioning-requests", Connection = "StreamProvisioningRequestsQueueConnectionString")]string message, ILogger logger)
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
                if (streamProvisioningEventQueue == null)
                {
                    throw new Exception("streamProvisioningEventQueue is null");
                }

                if (configService == null)
                {
                    throw new Exception("configService is null");
                }
#pragma warning restore CA1303 // Do not pass literals as localized parameters

                logger.LogInformation($"StreamProvisioningFunction::Run triggered, message={message}");
                var streamProvisioningRequestModel = JsonConvert.DeserializeObject<StreamProvisioningRequestModel>(message);
                var streamProvisioningEventStorageService = new StreamProvisioningEventStorageService(streamProvisioningEventQueue, logger);
                var streamProvisioningService = new StreamProvisioningService(streamProvisioningEventStorageService, configService, logger);

                await streamProvisioningService.ProvisionStreamAsync(streamProvisioningRequestModel).ConfigureAwait(false);

                logger.LogInformation($"StreamProvisioningFunction::Run completed, message={message}");
            }
            catch (Exception e)
            {
                logger.LogError($"StreamProvisioningFunction::Run failed: exception={e.Message} message={message}");
                throw;
            }
        }
    }
}
