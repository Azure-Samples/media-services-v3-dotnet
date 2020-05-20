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
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;

    [TestClass]
    public class IntegrationTests
    {
        private static ITableStorageService jobStatusTableStorageService;
        private static QueueClient jobRequestQueue;
        private static QueueClient jobVerificationRequestQueue;
        private static QueueClient streamProvisioningRequestQueue;
        private static QueueClient streamProvisioningEventQueue;
        private static ITableStorageService mediaServiceInstanceHealthTableStorageService;
        private const string jobStatusTableName = "JobStatusTest";
        private const string jobRequestQueueName = "jobrequests-test";
        private const string jobVerificationRequestQueueName = "jobverificationrequests-test";
        private const string streamProvisioningRequestQueueName = "streamprovisioningrequests-test";
        private const string streamProvisioningEventQueueName = "streamprovisioningevents-test";
        private static IConfigService configService;

        [ClassInitialize]
        public static async Task Initialize(TestContext _)
        {
            configService = new E2ETestConfigService("sipetrik-keyvault");
            await configService.LoadConfigurationAsync().ConfigureAwait(false);

            var storageAccount = CloudStorageAccount.Parse(configService.TableStorageAccountConnectionString);

            // Create a table client for interacting with the table service
            var tableClient = storageAccount.CreateCloudTableClient();

            // Create a table client for interacting with the table service 
            var jobStatusTable = tableClient.GetTableReference(jobStatusTableName);
            await jobStatusTable.CreateIfNotExistsAsync().ConfigureAwait(false);

            jobStatusTableStorageService = new TableStorageService(jobStatusTable);
            await jobStatusTableStorageService.DeleteAllAsync<JobStatusModelTableEntity>().ConfigureAwait(false);

            // Create a table client for interacting with the table service 
            var mediaServiceInstanceHealthTable = tableClient.GetTableReference(configService.MediaServiceInstanceHealthTableName);
            await mediaServiceInstanceHealthTable.CreateIfNotExistsAsync().ConfigureAwait(false);
            mediaServiceInstanceHealthTableStorageService = new TableStorageService(mediaServiceInstanceHealthTable);
            await mediaServiceInstanceHealthTableStorageService.DeleteAllAsync<MediaServiceInstanceHealthModelTableEntity>().ConfigureAwait(false);

            jobRequestQueue = new QueueClient(configService.StorageAccountConnectionString, jobRequestQueueName);
            await jobRequestQueue.CreateIfNotExistsAsync().ConfigureAwait(false);

            streamProvisioningRequestQueue = new QueueClient(configService.StorageAccountConnectionString, streamProvisioningRequestQueueName);
            await streamProvisioningRequestQueue.CreateIfNotExistsAsync().ConfigureAwait(false);

            jobVerificationRequestQueue = new QueueClient(configService.StorageAccountConnectionString, jobVerificationRequestQueueName);
            await jobVerificationRequestQueue.CreateIfNotExistsAsync().ConfigureAwait(false);

            streamProvisioningEventQueue = new QueueClient(configService.StorageAccountConnectionString, streamProvisioningEventQueueName);
            await streamProvisioningEventQueue.CreateIfNotExistsAsync().ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestJobStatusStorageService()
        {
            var uniqueness = Guid.NewGuid().ToString().Substring(0, 13);
            var target = new JobStatusStorageService(jobStatusTableStorageService);
            var jobName = $"JobName-{uniqueness}";
            var mediaServiceAccountName = $"Account1-{uniqueness}";
            var outputAssetName = $"AssetName-{uniqueness}";

            Assert.IsNotNull(await target.CreateOrUpdateAsync(new JobStatusModel
            {
                Id = Guid.NewGuid().ToString(),
                JobName = jobName,
                MediaServiceAccountName = mediaServiceAccountName,
                JobState = JobState.Finished,
                EventTime = DateTime.UtcNow,
                JobOutputAssetName = outputAssetName
            }, Mock.Of<ILogger>()).ConfigureAwait(false));

            var result = await target.ListAsync(jobName, outputAssetName).ConfigureAwait(false);
            Assert.AreEqual(1, result.Count());

            var currentTime = DateTime.UtcNow;
            var testData = new List<JobStatusModel>();
            for (var i = 0; i < 3; i++)
            {
                testData.Add(new JobStatusModel
                {
                    Id = Guid.NewGuid().ToString(),
                    JobName = jobName,
                    MediaServiceAccountName = mediaServiceAccountName,
                    JobState = JobState.Processing,
                    EventTime = currentTime.AddMinutes(-i),
                    JobOutputAssetName = outputAssetName
                });
            }

            foreach (var jobStatusModel in testData)
            {
                await target.CreateOrUpdateAsync(jobStatusModel, Mock.Of<ILogger>()).ConfigureAwait(false);
            }

            var allStatuses = target.ListAsync(jobName, outputAssetName);
            Assert.AreEqual(4, allStatuses.Result.Count());

            var latestStatus = await target.GetLatestJobStatusAsync(jobName, outputAssetName).ConfigureAwait(false);
            Assert.AreEqual(testData[0].Id, latestStatus.Id);
            Assert.AreEqual(testData[0].JobName, latestStatus.JobName);
            Assert.AreEqual(testData[0].JobState, latestStatus.JobState);
            Assert.AreEqual(testData[0].EventTime, latestStatus.EventTime);
            Assert.AreEqual(testData[0].MediaServiceAccountName, latestStatus.MediaServiceAccountName);
            Assert.AreEqual(testData[0].JobOutputAssetName, latestStatus.JobOutputAssetName);
        }

        [TestMethod]
        public async Task TestJobRequestStorageService()
        {
            var uniqueness = Guid.NewGuid().ToString().Substring(0, 13);
            var jobName = $"JobName-{uniqueness}";
            var outputAssetName = $"OutputAssetName-{uniqueness}";
            var transformName = $"TransformName-{uniqueness}";

            var jobInput = new JobInputHttp(
                                  baseUri: $"https://sampleurl{uniqueness}",
                                  files: new List<string> { $"file1{uniqueness}", $"file2{uniqueness}" },
                                  label: $"label {uniqueness}"
                                  );

            var target = new JobRequestStorageService(jobRequestQueue);

            var jobRequest = new JobRequestModel
            {
                Id = Guid.NewGuid().ToString(),
                JobInputs = new JobInputs
                {
                    Inputs = new List<JobInput> { jobInput }
                },
                JobName = jobName,
                OutputAssetName = outputAssetName,
                TransformName = transformName
            };

            Assert.IsNotNull(await target.CreateAsync(jobRequest, Mock.Of<ILogger>()).ConfigureAwait(false));

            var result = await target.GetNextAsync(Mock.Of<ILogger>()).ConfigureAwait(false);

            if (result == null)
            {
                throw new Exception("Got null from the queue");
            }

            Assert.AreEqual(jobRequest.Id, result.Id);
            Assert.AreEqual(jobRequest.JobInputs.Inputs.Count, result.JobInputs.Inputs.Count);
            var jobInputResult = (JobInputHttp)result.JobInputs.Inputs[0];
            Assert.AreEqual(jobInput.BaseUri, jobInputResult.BaseUri);
            Assert.AreEqual(jobInput.Files[0], jobInputResult.Files[0]);
            Assert.AreEqual(jobInput.Files[1], jobInputResult.Files[1]);
            Assert.AreEqual(jobInput.Label, jobInputResult.Label);
            Assert.AreEqual(jobRequest.JobName, result.JobName);
            Assert.AreEqual(jobRequest.OutputAssetName, result.OutputAssetName);
            Assert.AreEqual(jobRequest.TransformName, result.TransformName);
        }

        [TestMethod]
        public async Task TestMediaServiceInstanceHealthStorageService()
        {
            var target = new MediaServiceInstanceHealthStorageService(mediaServiceInstanceHealthTableStorageService);
            var mediaServiceAccountName1 = $"Account1";
            var mediaServiceAccountName2 = $"Account2";

            var currentDateTime = DateTime.UtcNow;

            var model1 = new MediaServiceInstanceHealthModel
            {
                MediaServiceAccountName = mediaServiceAccountName1,
                HealthState = InstanceHealthState.Degraded,
                LastUpdated = currentDateTime
            };

            var model2 = new MediaServiceInstanceHealthModel
            {
                MediaServiceAccountName = mediaServiceAccountName2,
                HealthState = InstanceHealthState.Healthy,
                LastUpdated = currentDateTime
            };


            Assert.IsNotNull(await target.CreateOrUpdateAsync(model1, Mock.Of<ILogger>()).ConfigureAwait(false));
            Assert.IsNotNull(await target.CreateOrUpdateAsync(model2, Mock.Of<ILogger>()).ConfigureAwait(false));

            var result = await target.ListAsync().ConfigureAwait(false);

            var resultModel1 = result.FirstOrDefault(i => i.MediaServiceAccountName == model1.MediaServiceAccountName);
            var resultModel2 = result.FirstOrDefault(i => i.MediaServiceAccountName == model2.MediaServiceAccountName);

            Assert.IsTrue(this.AreEqualMediaServiceInstanceHealthModels(model1, resultModel1));
            Assert.IsTrue(this.AreEqualMediaServiceInstanceHealthModels(model2, resultModel2));

            model1.HealthState = InstanceHealthState.Unhealthy;
            model1.LastUpdated = currentDateTime.AddMinutes(1);
            Assert.IsNotNull(await target.CreateOrUpdateAsync(model1, Mock.Of<ILogger>()).ConfigureAwait(false));

            resultModel1 = await target.GetAsync(mediaServiceAccountName1).ConfigureAwait(false);
            Assert.IsTrue(this.AreEqualMediaServiceInstanceHealthModels(model1, resultModel1));
        }

        [TestMethod]
        public async Task TestJobSchedulerService()
        {
            var jobStatusStorageService = new JobStatusStorageService(jobStatusTableStorageService);
            var mediaServiceInstanceHealthStorageService = new MediaServiceInstanceHealthStorageService(mediaServiceInstanceHealthTableStorageService);
            var mediaServiceInstanceHealthService = new MediaServiceInstanceHealthService(mediaServiceInstanceHealthStorageService, jobStatusStorageService, configService);
            var jobVerificationRequesetStorageService = new JobVerificationRequestStorageService(jobVerificationRequestQueue);
            var target = new JobSchedulerService(mediaServiceInstanceHealthService, jobVerificationRequesetStorageService, jobStatusStorageService, configService);

            await target.Initialize(Mock.Of<ILogger>()).ConfigureAwait(false);

            for (var i = 0; i < 4; i++)
            {
                var request = GenerateJobRequestModel();
                Assert.IsNotNull(await target.SubmitJobAsync(request, Mock.Of<ILogger>()).ConfigureAwait(false));
            }
        }

        [TestMethod]
        public async Task TestStreamProvisioningService()
        {
            var streamProvisioningEventStorageService = new StreamProvisioningEventStorageService(streamProvisioningEventQueue);
            var target = new StreamProvisioningService(streamProvisioningEventStorageService, configService);

            var uniqueness = Guid.NewGuid().ToString().Substring(0, 13);

            // TBD need to update this test not to reference specific item
            var request = new StreamProvisioningRequestModel
            {
                Id = $"Id-{uniqueness}",
                EncodedAssetMediaServiceAccountName = "sipetriktestmain",
                EncodedAssetName = "output-f861dc5c-d7b3",
                StreamingLocatorName = "sipetrik-test-locator"
            };

            await target.ProvisionStreamAsync(request, Mock.Of<ILogger>()).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestStreamProvisioningRequestStorageService()
        {
            var uniqueness = Guid.NewGuid().ToString().Substring(0, 13);

            var target = new StreamProvisioningRequestStorageService(streamProvisioningRequestQueue);

            var streamProvisioningRequest = new StreamProvisioningRequestModel
            {
                Id = Guid.NewGuid().ToString(),
                EncodedAssetMediaServiceAccountName = $"AccountName-{uniqueness}",
                EncodedAssetName = $"AssetName-{uniqueness}",
                StreamingLocatorName = $"StreamLocator-{uniqueness}"
            };

            Assert.IsNotNull(await target.CreateAsync(streamProvisioningRequest, Mock.Of<ILogger>()).ConfigureAwait(false));

            var result = await target.GetNextAsync(Mock.Of<ILogger>()).ConfigureAwait(false);

            if (result == null)
            {
                throw new Exception("Got null from the queue");
            }

            Assert.AreEqual(streamProvisioningRequest.Id, result.Id);
            Assert.AreEqual(streamProvisioningRequest.EncodedAssetMediaServiceAccountName, result.EncodedAssetMediaServiceAccountName);
            Assert.AreEqual(streamProvisioningRequest.EncodedAssetName, result.EncodedAssetName);
            Assert.AreEqual(streamProvisioningRequest.StreamingLocatorName, result.StreamingLocatorName);
        }

        [TestMethod]
        public async Task TestJobVerificationRequestStorageService()
        {
            var uniqueness = Guid.NewGuid().ToString().Substring(0, 13);
            var jobId = $"JobId-{uniqueness}";
            var jobName = $"JobName-{uniqueness}";
            var outputAssetName = $"OutputAssetName-{uniqueness}";
            var transformName = $"TransformName-{uniqueness}";

            var jobInput = new JobInputHttp(
                                  baseUri: $"https://sampleurl{uniqueness}",
                                  files: new List<string> { $"file1{uniqueness}", $"file2{uniqueness}" },
                                  label: $"label {uniqueness}"
                                  );

            var target = new JobVerificationRequestStorageService(jobVerificationRequestQueue);

            var jobRequest = new JobRequestModel
            {
                Id = Guid.NewGuid().ToString(),
                JobInputs = new JobInputs
                {
                    Inputs = new List<JobInput> { jobInput }
                },
                JobName = jobName,
                OutputAssetName = outputAssetName,
                TransformName = transformName
            };

            var jobVerificationRequest = new JobVerificationRequestModel
            {
                Id = Guid.NewGuid().ToString(),
                JobId = jobId,
                OriginalJobRequestModel = jobRequest,
                MediaServiceAccountName = $"AccountName-{uniqueness}",
            };

            Assert.IsNotNull(await target.CreateAsync(jobVerificationRequest, new TimeSpan(0, 0, 5), Mock.Of<ILogger>()).ConfigureAwait(false));

            var result = await target.GetNextAsync(Mock.Of<ILogger>()).ConfigureAwait(false);
            // we should not get a message right away, visibility should be set to 5 seconds
            Assert.IsNull(result);

            await Task.Delay(5500).ConfigureAwait(false);
            result = await target.GetNextAsync(Mock.Of<ILogger>()).ConfigureAwait(false);

            if (result == null)
            {
                throw new Exception("Got null from the queue");
            }

            Assert.AreEqual(jobVerificationRequest.Id, result.Id);
            Assert.AreEqual(jobVerificationRequest.OriginalJobRequestModel.JobInputs.Inputs.Count, result.OriginalJobRequestModel.JobInputs.Inputs.Count);
            var jobInputResult = (JobInputHttp)result.OriginalJobRequestModel.JobInputs.Inputs[0];
            Assert.AreEqual(jobInput.BaseUri, jobInputResult.BaseUri);
            Assert.AreEqual(jobInput.Files[0], jobInputResult.Files[0]);
            Assert.AreEqual(jobInput.Files[1], jobInputResult.Files[1]);
            Assert.AreEqual(jobInput.Label, jobInputResult.Label);
            Assert.AreEqual(jobVerificationRequest.OriginalJobRequestModel.JobName, result.OriginalJobRequestModel.JobName);
            Assert.AreEqual(jobRequest.OutputAssetName, result.OriginalJobRequestModel.OutputAssetName);
            Assert.AreEqual(jobRequest.TransformName, result.OriginalJobRequestModel.TransformName);
            Assert.AreEqual(jobVerificationRequest.JobId, result.JobId);
            Assert.AreEqual(jobVerificationRequest.MediaServiceAccountName, result.MediaServiceAccountName);
        }

        [TestMethod]
        public async Task TestJobVerificationService()
        {
            var uniqueness = Guid.NewGuid().ToString().Substring(0, 13);
            var jobId = $"JobId";
            var jobName = $"jobName-10-cf1cdda5-a7ac";
            var outputAssetName = $"OutputAssetName-{uniqueness}";
            var transformName = "AdaptiveBitrate";

            var jobInput = new JobInputHttp(
                                  baseUri: $"https://sampleurl{uniqueness}",
                                  files: new List<string> { $"file1{uniqueness}", $"file2{uniqueness}" },
                                  label: $"label {uniqueness}"
                                  );

            var jobStatusStorageService = new JobStatusStorageService(jobStatusTableStorageService);
            var mediaServiceInstanceHealthStorageService = new MediaServiceInstanceHealthStorageService(mediaServiceInstanceHealthTableStorageService);
            var mediaServiceInstanceHealthService = new MediaServiceInstanceHealthService(mediaServiceInstanceHealthStorageService, jobStatusStorageService, configService);
            var streamProvisioningRequestStorageService = new StreamProvisioningRequestStorageService(streamProvisioningRequestQueue);
            var jobVerificationRequestStorageService = new JobVerificationRequestStorageService(jobVerificationRequestQueue);

            var target = new JobVerificationService(mediaServiceInstanceHealthService,
                                                    jobStatusStorageService,
                                                    streamProvisioningRequestStorageService,
                                                    jobVerificationRequestStorageService,
                                                    configService);

            var jobRequest = new JobRequestModel
            {
                Id = Guid.NewGuid().ToString(),
                JobInputs = new JobInputs
                {
                    Inputs = new List<JobInput> { jobInput }
                },
                JobName = jobName,
                OutputAssetName = outputAssetName,
                TransformName = transformName
            };

            var jobVerificationRequest = new JobVerificationRequestModel
            {
                Id = Guid.NewGuid().ToString(),
                JobId = jobId,
                OriginalJobRequestModel = jobRequest,
                MediaServiceAccountName = "sipetriktestmain",
            };

            Assert.IsNotNull(await target.VerifyJobAsync(jobVerificationRequest, Mock.Of<ILogger>()).ConfigureAwait(false));
        }

        [TestMethod]
        public async Task TestLoadReEvaluateMediaServicesHealthAsync()
        {
            //  await jobStatusTableStorageService.DeleteAllAsync<JobStatusModelTableEntity>().ConfigureAwait(false);
            await mediaServiceInstanceHealthTableStorageService.DeleteAllAsync<MediaServiceInstanceHealthModelTableEntity>().ConfigureAwait(false);

            var jobStatusStorageService = new JobStatusStorageService(jobStatusTableStorageService);
            var mediaServiceInstanceHealthStorageService = new MediaServiceInstanceHealthStorageService(mediaServiceInstanceHealthTableStorageService);
            var currentTime = DateTime.UtcNow;

            //var jobStatusList = CreateTestData(currentTime, "account2");
            //for (int i = 0; i < 10000; i++)
            //{
            //    jobStatusList.AddRange(CreateTestData(currentTime, "account2"));
            //}

            var accounts = new List<MediaServiceInstanceHealthModel>
            {
                new MediaServiceInstanceHealthModel {MediaServiceAccountName = "account1", HealthState = InstanceHealthState.Healthy, LastUpdated = currentTime},
                new MediaServiceInstanceHealthModel {MediaServiceAccountName = "account2", HealthState = InstanceHealthState.Healthy, LastUpdated = currentTime}
            };

            //foreach (var jobStatus in jobStatusList)
            //{
            //    await jobStatusTableStorageService.CreateOrUpdateAsync(new JobStatusModelTableEntity(jobStatus)).ConfigureAwait(false);
            //}

            //Parallel.ForEach(jobStatusList, new ParallelOptions { MaxDegreeOfParallelism = 400 }, (jobStatus) =>
            //    {
            //        jobStatusTableStorageService.CreateOrUpdateAsync(new JobStatusModelTableEntity(jobStatus)).Wait();
            //    }
            //);

            await mediaServiceInstanceHealthTableStorageService.CreateOrUpdateAsync(new MediaServiceInstanceHealthModelTableEntity(accounts[0])).ConfigureAwait(false);
            await mediaServiceInstanceHealthTableStorageService.CreateOrUpdateAsync(new MediaServiceInstanceHealthModelTableEntity(accounts[1])).ConfigureAwait(false);

            var target = new MediaServiceInstanceHealthService(mediaServiceInstanceHealthStorageService, jobStatusStorageService, configService);

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var result = await target.ReEvaluateMediaServicesHealthAsync(Mock.Of<ILogger>()).ConfigureAwait(false);

            stopWatch.Stop();

            var elapsed = stopWatch.Elapsed.TotalSeconds;
            Console.WriteLine($"It took {elapsed} seconds to run");
        }

        private static List<JobStatusModel> CreateTestData(DateTime currentTime, string accountName)
        {
            var uniqueness = Guid.NewGuid().ToString().Substring(0, 13);

            return new List<JobStatusModel>
            {
                new JobStatusModel {JobName = $"job1-{uniqueness}", MediaServiceAccountName = accountName, JobState = JobState.Processing, EventTime = currentTime, Id = Guid.NewGuid().ToString()},
                new JobStatusModel {JobName = $"job1-{uniqueness}", MediaServiceAccountName = accountName, JobState = JobState.Processing, EventTime = currentTime.AddSeconds(1), Id = Guid.NewGuid().ToString()},
                new JobStatusModel {JobName = $"job1-{uniqueness}", MediaServiceAccountName = accountName, JobState = JobState.Finished, EventTime = currentTime.AddSeconds(2), Id = Guid.NewGuid().ToString()},
                new JobStatusModel {JobName = $"job2-{uniqueness}", MediaServiceAccountName = accountName, JobState = JobState.Processing, EventTime = currentTime, Id = Guid.NewGuid().ToString()},
                new JobStatusModel {JobName = $"job2-{uniqueness}", MediaServiceAccountName = accountName, JobState = JobState.Processing, EventTime = currentTime.AddSeconds(2), Id = Guid.NewGuid().ToString()},
                new JobStatusModel {JobName = $"job2-{uniqueness}", MediaServiceAccountName = accountName, JobState = JobState.Error, EventTime = currentTime.AddSeconds(3), Id = Guid.NewGuid().ToString()},
                new JobStatusModel {JobName = $"job3-{uniqueness}", MediaServiceAccountName = accountName, JobState = JobState.Processing, EventTime = currentTime, Id = Guid.NewGuid().ToString()},
                new JobStatusModel {JobName = $"job3-{uniqueness}", MediaServiceAccountName = accountName, JobState = JobState.Processing, EventTime = currentTime.AddSeconds(2), Id = Guid.NewGuid().ToString()},
                new JobStatusModel {JobName = $"job3-{uniqueness}", MediaServiceAccountName = accountName, JobState = JobState.Processing, EventTime = currentTime.AddSeconds(3), Id = Guid.NewGuid().ToString()},
                new JobStatusModel {JobName = $"job4-{uniqueness}", MediaServiceAccountName = accountName, JobState = JobState.Processing, EventTime = currentTime, Id = Guid.NewGuid().ToString()},
                new JobStatusModel {JobName = $"job4-{uniqueness}", MediaServiceAccountName = accountName, JobState = JobState.Processing, EventTime = currentTime.AddSeconds(2), Id = Guid.NewGuid().ToString()},
                new JobStatusModel {JobName = $"job4-{uniqueness}", MediaServiceAccountName = accountName, JobState = JobState.Processing, EventTime = currentTime.AddHours(3), Id = Guid.NewGuid().ToString()}
            };
        }

        private static JobRequestModel GenerateJobRequestModel()
        {
            var uniqueness = Guid.NewGuid().ToString().Substring(0, 13);

            var jobId = "jobId-" + uniqueness;
            var jobName = "jobName-" + uniqueness;
            var outputAssetName = "output-" + uniqueness;

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
                TransformName = "AdaptiveBitrate",
                JobInputs = new JobInputs
                {
                    Inputs = new List<JobInput> { input }
                }
            };
            return request;
        }

        private bool AreEqualMediaServiceInstanceHealthModels(MediaServiceInstanceHealthModel m1, MediaServiceInstanceHealthModel m2)
        {
            return (m1.MediaServiceAccountName == m2.MediaServiceAccountName &&
                    m1.HealthState == m2.HealthState &&
                    m1.LastUpdated == m2.LastUpdated);
        }
    }
}
