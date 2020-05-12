namespace HighAvailability.Tests
{
    using Azure.Storage.Queues;
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

    [TestClass]
    public class E2ETests
    {
        private static QueueClient? jobRequestQueue;
        private static TableStorageService? mediaServiceInstanceHealthTableStorageService;
        private static IConfigService? configService;
        private static QueueClient? jobVerificationRequestQueue;

        [ClassInitialize]
        public static async Task Initialize(TestContext testContext)
        {
            if (testContext is null)
            {
                throw new System.ArgumentNullException(nameof(testContext));
            }

            configService = new E2ETestConfigService("sipetrik-keyvault");
            await configService.LoadConfigurationAsync().ConfigureAwait(false);

            var storageAccount = CloudStorageAccount.Parse(configService.TableStorageAccountConnectionString);

            // Create a table client for interacting with the table service
            var tableClient = storageAccount.CreateCloudTableClient();

            // Create a table client for interacting with the table service 
            var mediaServiceInstanceHealthTable = tableClient.GetTableReference(configService.MediaServiceInstanceHealthTableName);
            await mediaServiceInstanceHealthTable.CreateIfNotExistsAsync().ConfigureAwait(false);
            mediaServiceInstanceHealthTableStorageService = new TableStorageService(mediaServiceInstanceHealthTable);

            jobRequestQueue = new QueueClient(configService.StorageAccountConnectionString, configService.JobRequestQueueName);
            await jobRequestQueue.CreateIfNotExistsAsync().ConfigureAwait(false);

            jobVerificationRequestQueue = new QueueClient(configService.StorageAccountConnectionString, configService.JobVerificationRequestQueueName);
            await jobVerificationRequestQueue.CreateIfNotExistsAsync().ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestJobRequestStorageService()
        {
            if (mediaServiceInstanceHealthTableStorageService == null)
            {
                throw new Exception("mediaServiceInstanceHealthTableStorageService is not initialized");
            }
            if (jobVerificationRequestQueue == null)
            {
                throw new Exception("jobVerificationRequestQueue is not initialized");
            }
            if (configService == null)
            {
                throw new Exception("configService is not initialized");
            }
            if (jobRequestQueue == null)
            {
                throw new Exception("jobRequestQueue is not initialized");
            }

            var mediaServiceInstanceHealthStorageService = new MediaServiceInstanceHealthStorageService(mediaServiceInstanceHealthTableStorageService);
            var mediaServiceInstanceHealthService = new MediaServiceInstanceHealthService(mediaServiceInstanceHealthStorageService);
            var jobVerificationRequesetStorageService = new JobVerificationRequestStorageService(jobVerificationRequestQueue);
            var jobSchedulerService = new JobSchedulerService(mediaServiceInstanceHealthService, jobVerificationRequesetStorageService, configService);

            await jobSchedulerService.Initialize(Mock.Of<ILogger>()).ConfigureAwait(false);

            var target = new JobRequestStorageService(jobRequestQueue);
            var uniqueness = Guid.NewGuid().ToString().Substring(0, 13);

            for (var i = 0; i < 5; i++)
            {
                Assert.IsNotNull(await target.CreateAsync(GenerateJobRequestModel(i, uniqueness), Mock.Of<ILogger>()).ConfigureAwait(false));
            }
        }

        private static JobRequestModel GenerateJobRequestModel(int sequenceNumber, string uniqueness)
        {
            var jobId = $"jobId-{sequenceNumber}-{uniqueness}";
            var jobName = $"jobName-{sequenceNumber}-{uniqueness}";
            var inputAssetName = $"input-{sequenceNumber}-{uniqueness}";
            var outputAssetName = $"output-{sequenceNumber}-{uniqueness}";

            var input = new JobInputHttp(
                                    baseUri: "https://nimbuscdn-nimbuspm.streaming.mediaservices.windows.net/2b533311-b215-4409-80af-529c3e853622/",
                                    files: new List<string> { "Ignite-short.mp4" },
                                    label: "input1"
                                    );

            var request = new JobRequestModel
            {
                Id = jobId,
                JobName = jobName,
                InputAssetName = inputAssetName,
                OutputAssetName = outputAssetName,
                TransformName = "AdaptiveBitrate",
                JobInputs = new JobInputs
                {
                    Inputs = new List<JobInput> { input }
                }
            };
            return request;
        }
    }
}