namespace HighAvailability.Tests
{
    using Azure.Storage.Queues;
    using HighAvailability.Helpers;
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
        private static string transformName = "TestTransform";

        private static QueueClient jobRequestQueue;
        private static ITableStorageService mediaServiceInstanceHealthTableStorageService;
        private static ITableStorageService jobOutputStatusTableStorageService;
        private static IConfigService configService;
        private static QueueClient jobVerificationRequestQueue;

        [ClassInitialize]
        public static async Task Initialize(TestContext _)
        {
            configService = new E2ETestConfigService("sipetrik-keyvault");
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

            jobRequestQueue = new QueueClient(configService.StorageAccountConnectionString, configService.JobRequestQueueName);
            await jobRequestQueue.CreateIfNotExistsAsync().ConfigureAwait(false);

            jobVerificationRequestQueue = new QueueClient(configService.StorageAccountConnectionString, configService.JobVerificationRequestQueueName);
            await jobVerificationRequestQueue.CreateIfNotExistsAsync().ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestJobRequestStorageService()
        {
            var jobOutputStatusStorageService = new JobOutputStatusStorageService(jobOutputStatusTableStorageService);
            var mediaServiceInstanceHealthStorageService = new MediaServiceInstanceHealthStorageService(mediaServiceInstanceHealthTableStorageService);
            var mediaServiceInstanceHealthService = new MediaServiceInstanceHealthService(mediaServiceInstanceHealthStorageService, jobOutputStatusStorageService, configService);

            foreach (var config in configService.MediaServiceInstanceConfiguration)
            {
                using (var client = await MediaServicesHelper.CreateMediaServicesClientAsync(config.Value).ConfigureAwait(false))
                {
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
            }

            var target = new JobRequestStorageService(jobRequestQueue);
            var uniqueness = Guid.NewGuid().ToString().Substring(0, 13);

            for (var i = 0; i < 2; i++)
            {
                Assert.IsNotNull(await target.CreateAsync(GenerateJobRequestModel(i, uniqueness), Mock.Of<ILogger>()).ConfigureAwait(false));
            }
        }

        private static JobRequestModel GenerateJobRequestModel(int sequenceNumber, string uniqueness)
        {
            var jobId = $"jobId-{sequenceNumber}-{uniqueness}";
            var jobName = $"jobName-{sequenceNumber}-{uniqueness}";
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
