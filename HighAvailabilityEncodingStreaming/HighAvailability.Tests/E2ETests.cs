namespace HighAvailability.Tests
{
    using Azure.Storage.Queues;
    using HighAvailability.AzureStorage.Models;
    using HighAvailability.AzureStorage.Services;
    using HighAvailability.Factories;
    using HighAvailability.Helpers;
    using HighAvailability.Interfaces;
    using HighAvailability.Models;
    using HighAvailability.Services;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// This class contains code that needs to run once after initial install of this sample.
    /// It sets up transform and key policy for each Media Services instance, upload initial records for health state tracking for each instance.
    /// </summary>
    [TestClass]
    public class E2ETests
    {
        /// <summary>
        /// Default transform name
        /// </summary>
        private static string transformName = "TestTransform";

        /// <summary>
        /// Job request queue client
        /// </summary>
        private static QueueClient jobRequestQueue;

        /// <summary>
        /// Table storage service to persist Azure Media Service instance health
        /// </summary>
        private static ITableStorageService mediaServiceInstanceHealthTableStorageService;

        /// <summary>
        /// Storage service to persist status of all calls to Media Services APIs
        /// </summary>
        private static MediaServiceCallHistoryStorageService mediaServiceCallHistoryStorageService;

        /// <summary>
        /// Table storage service to store job output status
        /// </summary>
        private static ITableStorageService jobOutputStatusTableStorageService;

        /// <summary>
        /// Configuration container
        /// </summary>
        private static IConfigService configService;

        /// <summary>
        /// Job verification request queue client
        /// </summary>
        private static QueueClient jobVerificationRequestQueue;

        /// <summary>
        /// Initialize objects to run this setup
        /// </summary>
        /// <param name="_">not used in this sample</param>
        /// <returns>Task for async operation</returns>
        [ClassInitialize]
        public static async Task Initialize(TestContext _)
        {
            configService = new E2ETestConfigService("sipetrik-keyvault");
            //  configService = new E2ETestConfigService("<enter keyvault name>");
            await configService.LoadConfigurationAsync().ConfigureAwait(false);

            var storageAccount = CloudStorageAccount.Parse(configService.TableStorageAccountConnectionString);

            // Create a table client for interacting with the table service
            var tableClient = storageAccount.CreateCloudTableClient();

            // Create a table client for interacting with the table service 
            var mediaServiceInstanceHealthTable = tableClient.GetTableReference(configService.MediaServiceInstanceHealthTableName);
            await mediaServiceInstanceHealthTable.CreateIfNotExistsAsync().ConfigureAwait(false);
            mediaServiceInstanceHealthTableStorageService = new TableStorageService(mediaServiceInstanceHealthTable);
            await mediaServiceInstanceHealthTableStorageService.DeleteAllAsync<MediaServiceInstanceHealthModelTableEntity>().ConfigureAwait(false);

            var jobOutputStatusTable = tableClient.GetTableReference(configService.JobOutputStatusTableName);
            await jobOutputStatusTable.CreateIfNotExistsAsync().ConfigureAwait(false);
            jobOutputStatusTableStorageService = new TableStorageService(jobOutputStatusTable);
            await jobOutputStatusTableStorageService.DeleteAllAsync<JobOutputStatusModelTableEntity>().ConfigureAwait(false);

            var mediaServiceCallHistoryTable = tableClient.GetTableReference(configService.MediaServiceCallHistoryTableName);
            mediaServiceCallHistoryTable.CreateIfNotExists();
            var mediaServiceCallHistoryTableStorageService = new TableStorageService(mediaServiceCallHistoryTable);
            mediaServiceCallHistoryStorageService = new MediaServiceCallHistoryStorageService(mediaServiceCallHistoryTableStorageService);

            jobRequestQueue = new QueueClient(configService.StorageAccountConnectionString, configService.JobRequestQueueName);
            await jobRequestQueue.CreateIfNotExistsAsync().ConfigureAwait(false);

            jobVerificationRequestQueue = new QueueClient(configService.StorageAccountConnectionString, configService.JobVerificationRequestQueueName);
            await jobVerificationRequestQueue.CreateIfNotExistsAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Uploads initial values to services for this sample to work. 
        /// Submits test requests.
        /// </summary>
        /// <returns>Task for async operation</returns>
        [TestMethod]
        public async Task SubmitTestRequests()
        {
            var jobOutputStatusStorageService = new JobOutputStatusStorageService(jobOutputStatusTableStorageService);
            var mediaServiceInstanceHealthStorageService = new MediaServiceInstanceHealthStorageService(mediaServiceInstanceHealthTableStorageService);
            var mediaServiceInstanceHealthService = new MediaServiceInstanceHealthService(mediaServiceInstanceHealthStorageService, jobOutputStatusStorageService, mediaServiceCallHistoryStorageService, configService);
            var mediaServiceInstanceFactory = new MediaServiceInstanceFactory(mediaServiceCallHistoryStorageService, configService);

            foreach (var config in configService.MediaServiceInstanceConfiguration)
            {
                var client = await mediaServiceInstanceFactory.GetMediaServiceInstanceAsync(config.Value.AccountName, Mock.Of<ILogger>()).ConfigureAwait(false);
                client.LongRunningOperationRetryTimeout = 2;

                await MediaServicesHelper.EnsureTransformExists(
                    client,
                    config.Value.ResourceGroup,
                    config.Value.AccountName,
                    transformName,
                    new BuiltInStandardEncoderPreset(EncoderNamedPreset.AdaptiveStreaming)).ConfigureAwait(false);

                await mediaServiceInstanceHealthService.CreateOrUpdateAsync(new MediaServiceInstanceHealthModel
                {
                    MediaServiceAccountName = config.Value.AccountName,
                    HealthState = InstanceHealthState.Healthy,
                    LastUpdated = DateTime.UtcNow,
                    IsEnabled = true
                },
                    Mock.Of<ILogger>()).ConfigureAwait(false);

                await MediaServicesHelper.EnsureContentKeyPolicyExists(
                    client,
                    config.Value.ResourceGroup,
                    config.Value.AccountName,
                    configService.ContentKeyPolicyName,
                    configService.GetClearKeyStreamingKey(),
                    configService.TokenIssuer,
                    configService.TokenAudience).ConfigureAwait(false);
            }

            var target = new JobRequestStorageService(jobRequestQueue);
            var uniqueness = Guid.NewGuid().ToString().Substring(0, 13);

            for (var i = 0; i < 10; i++)
            {
                Assert.IsNotNull(await target.CreateAsync(GenerateJobRequestModel(i, uniqueness), Mock.Of<ILogger>()).ConfigureAwait(false));
            }
        }

        /// <summary>
        /// Generates test data
        /// </summary>
        /// <param name="sequenceNumber">sequence number</param>
        /// <param name="uniqueness">unique part of the names</param>
        /// <returns>Generated JobRequestModel</returns>
        private static JobRequestModel GenerateJobRequestModel(int sequenceNumber, string uniqueness)
        {
            var jobId = $"jobId-{sequenceNumber}-{uniqueness}";
            var jobName = $"jobName-{sequenceNumber}-{uniqueness}";
            var outputAssetName = $"output-{sequenceNumber}-{uniqueness}";

            // Add job input for this test
            // TBD remove following value
            var input = new JobInputHttp(
                                   baseUri: "https://nimbuscdn-nimbuspm.streaming.mediaservices.windows.net/2b533311-b215-4409-80af-529c3e853622/",
                                   files: new List<string> { "Ignite-short.mp4" },
                                   label: "input1"
                                   );

            var request = new JobRequestModel
            {
                Id = jobId,
                JobName = jobName,
                OutputAssetName = outputAssetName,
                TransformName = transformName,
                JobInputs = new JobInputs
                {
                    Inputs = new List<JobInput> { input }
                }
            };
            return request;
        }
    }
}
