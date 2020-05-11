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
    using System.Linq;
    using System.Threading.Tasks;

    [TestClass]
    public class IntegrationTests
    {
        private static TableStorageService? jobStatusTableStorageService;
        private static QueueClient? jobRequestQueue;
        private static QueueClient? jobVerificationRequestQueue;
        private static QueueClient? streamProvisioningRequestQueue;
        private static QueueClient? streamProvisioningEventQueue;
        private static TableStorageService? mediaServiceInstanceHealthTableStorageService;
        private const string jobStatusTableName = "JobStatusTest";
        private const string jobRequestQueueName = "jobrequests-test";
        private const string jobVerificationRequestQueueName = "jobverificationrequests-test";
        private const string streamProvisioningRequestQueueName = "streamprovisioningrequests-test";
        private const string streamProvisioningEventQueueName = "streamprovisioningevents-test";
        private static IConfigService? configService;

        [ClassInitialize]
        public static async Task Initialize(TestContext testContext)
        {
            if (testContext is null)
            {
                throw new System.ArgumentNullException(nameof(testContext));
            }

            configService = new ConfigService("sipetrikha2-keyvault");
            await configService.LoadConfigurationAsync().ConfigureAwait(false);

            var storageAccount = CloudStorageAccount.Parse(configService.TableStorageAccountConnectionString);

            // Create a table client for interacting with the table service
            var tableClient = storageAccount.CreateCloudTableClient();

            // Create a table client for interacting with the table service 
            var jobStatusTable = tableClient.GetTableReference(jobStatusTableName);
            await jobStatusTable.CreateIfNotExistsAsync().ConfigureAwait(false);
            jobStatusTableStorageService = new TableStorageService(jobStatusTable);

            // Create a table client for interacting with the table service 
            var mediaServiceInstanceHealthTable = tableClient.GetTableReference(configService.MediaServiceInstanceHealthTableName);
            await mediaServiceInstanceHealthTable.CreateIfNotExistsAsync().ConfigureAwait(false);
            mediaServiceInstanceHealthTableStorageService = new TableStorageService(mediaServiceInstanceHealthTable);

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
            if (jobStatusTableStorageService == null)
            {
                throw new Exception("jobStatusTableStorageService is not initialized");
            }

            var uniqueness = Guid.NewGuid().ToString().Substring(0, 13);
            var target = new JobStatusStorageService(jobStatusTableStorageService, Mock.Of<ILogger>());
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
            }).ConfigureAwait(false));

            var result = await target.ListAsync(jobName).ConfigureAwait(false);
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
                await target.CreateOrUpdateAsync(jobStatusModel).ConfigureAwait(false);
            }

            var allStatuses = target.ListAsync(jobName);
            Assert.AreEqual(4, allStatuses.Result.Count());

            var latestStatus = await target.GetLatestJobStatusAsync(jobName).ConfigureAwait(false);
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
            if (jobRequestQueue == null)
            {
                throw new Exception("jobRequestQueue is not initialized");
            }

            var uniqueness = Guid.NewGuid().ToString().Substring(0, 13);
            var jobName = $"JobName-{uniqueness}";
            var outputAssetName = $"OutputAssetName-{uniqueness}";
            var transformName = $"TransformName-{uniqueness}";

            var jobInput = new JobInputHttp(
                                  baseUri: $"https://sampleurl{uniqueness}",
                                  files: new List<string> { $"file1{uniqueness}", $"file2{uniqueness}" },
                                  label: $"label {uniqueness}"
                                  );

            var target = new JobRequestStorageService(jobRequestQueue, Mock.Of<ILogger>());

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

            Assert.IsNotNull(await target.CreateAsync(jobRequest).ConfigureAwait(false));

            var result = await target.GetNextAsync().ConfigureAwait(false);

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
            if (mediaServiceInstanceHealthTableStorageService == null)
            {
                throw new Exception("mediaServiceInstanceHealthTableStorageService is not initialized");
            }

            var target = new MediaServiceInstanceHealthStorageService(mediaServiceInstanceHealthTableStorageService, Mock.Of<ILogger>());
            var mediaServiceAccountName1 = $"Account1";
            var mediaServiceAccountName2 = $"Account2";

            var currentDateTime = DateTime.UtcNow;

            var model1 = new MediaServiceInstanceHealthModel
            {
                MediaServiceAccountName = mediaServiceAccountName1,
                IsHealthy = true,
                LastSuccessfulJob = currentDateTime.AddHours(-2),
                LastFailedJob = currentDateTime.AddHours(-1),
                LastUpdated = currentDateTime,
                LastSubmittedJob = currentDateTime.AddMinutes(-1)
            };

            var model2 = new MediaServiceInstanceHealthModel
            {
                MediaServiceAccountName = mediaServiceAccountName2,
                IsHealthy = true,
                LastSuccessfulJob = currentDateTime.AddHours(-3),
                LastFailedJob = currentDateTime.AddHours(-4),
                LastUpdated = currentDateTime,
                LastSubmittedJob = currentDateTime.AddMinutes(-2)
            };


            Assert.IsNotNull(await target.CreateOrUpdateAsync(model1).ConfigureAwait(false));
            Assert.IsNotNull(await target.CreateOrUpdateAsync(model2).ConfigureAwait(false));

            var result = await target.ListAsync().ConfigureAwait(false);

            var resultModel1 = result.FirstOrDefault(i => i.MediaServiceAccountName == model1.MediaServiceAccountName);
            var resultModel2 = result.FirstOrDefault(i => i.MediaServiceAccountName == model2.MediaServiceAccountName);

            Assert.IsTrue(this.AreEqualMediaServiceInstanceHealthModels(model1, resultModel1));
            Assert.IsTrue(this.AreEqualMediaServiceInstanceHealthModels(model2, resultModel2));

            model1.IsHealthy = false;
            model1.LastUpdated = currentDateTime.AddMinutes(1);
            Assert.IsNotNull(await target.CreateOrUpdateAsync(model1).ConfigureAwait(false));

            resultModel1 = await target.GetAsync(mediaServiceAccountName1).ConfigureAwait(false);
            Assert.IsTrue(this.AreEqualMediaServiceInstanceHealthModels(model1, resultModel1));

            var updateDateTime = currentDateTime.AddMinutes(100);
            var updatedResultModel1 = await target.UpdateProcessedJobStateAsync(mediaServiceAccountName1, false, updateDateTime).ConfigureAwait(false);
            updatedResultModel1 = await target.GetAsync(mediaServiceAccountName1).ConfigureAwait(false);

            Assert.AreEqual(updateDateTime, updatedResultModel1.LastFailedJob);
            Assert.AreEqual(updateDateTime, updatedResultModel1.LastUpdated);
            Assert.AreEqual(resultModel1.LastSuccessfulJob, updatedResultModel1.LastSuccessfulJob);
            Assert.AreEqual(resultModel1.LastSubmittedJob, updatedResultModel1.LastSubmittedJob);

            updateDateTime = currentDateTime.AddMinutes(200);
            var updatedResultModel2 = await target.UpdateProcessedJobStateAsync(mediaServiceAccountName2, true, updateDateTime).ConfigureAwait(false);
            updatedResultModel2 = await target.GetAsync(mediaServiceAccountName2).ConfigureAwait(false);

            Assert.AreEqual(updateDateTime, updatedResultModel2.LastSuccessfulJob);
            Assert.AreEqual(updateDateTime, updatedResultModel2.LastUpdated);
            Assert.AreEqual(resultModel2.LastFailedJob, updatedResultModel2.LastFailedJob);
            Assert.AreEqual(resultModel2.LastSubmittedJob, updatedResultModel2.LastSubmittedJob);

            updateDateTime = currentDateTime.AddMinutes(300);
            var updatedSubmittedResultModel2 = await target.UpdateSubmittedJobStateAsync(mediaServiceAccountName2, updateDateTime).ConfigureAwait(false);
            updatedSubmittedResultModel2 = await target.GetAsync(mediaServiceAccountName2).ConfigureAwait(false);

            Assert.AreEqual(updatedResultModel2.LastSuccessfulJob, updatedSubmittedResultModel2.LastSuccessfulJob);
            Assert.AreEqual(updatedResultModel2.LastFailedJob, updatedSubmittedResultModel2.LastFailedJob);
            Assert.AreEqual(updateDateTime, updatedSubmittedResultModel2.LastSubmittedJob);
            Assert.AreEqual(updateDateTime, updatedSubmittedResultModel2.LastUpdated);
        }

        [TestMethod]
        public async Task TestJobSchedulerService()
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

            var mediaServiceInstanceHealthStorageService = new MediaServiceInstanceHealthStorageService(mediaServiceInstanceHealthTableStorageService, Mock.Of<ILogger>());
            var mediaServiceInstanceHealthService = new MediaServiceInstanceHealthService(mediaServiceInstanceHealthStorageService, Mock.Of<ILogger>());
            var jobVerificationRequesetStorageService = new JobVerificationRequestStorageService(jobVerificationRequestQueue, Mock.Of<ILogger>());
            var target = new JobSchedulerService(mediaServiceInstanceHealthService, jobVerificationRequesetStorageService, configService, Mock.Of<ILogger>());

            await target.Initialize().ConfigureAwait(false);

            for (var i = 0; i < 4; i++)
            {
                var request = GenerateJobRequestModel();
                Assert.IsNotNull(await target.SubmitJobAsync(request).ConfigureAwait(false));
            }
        }

        [TestMethod]
        public async Task TestStreamProvisioningService()
        {
            if (streamProvisioningEventQueue == null)
            {
                throw new Exception("streamProvisioningEventQueue is not initialized");
            }

            if (configService == null)
            {
                throw new Exception("configService is not initialized");
            }

            var streamProvisioningEventStorageService = new StreamProvisioningEventStorageService(streamProvisioningEventQueue, Mock.Of<ILogger>());
            var target = new StreamProvisioningService(streamProvisioningEventStorageService, configService, Mock.Of<ILogger>());

            var uniqueness = Guid.NewGuid().ToString().Substring(0, 13);

            // TBD need to update this test not to reference specific item
            var request = new StreamProvisioningRequestModel
            {
                Id = $"Id-{uniqueness}",
                EncodedAssetMediaServiceAccountName = "sipetriktestmain",
                EncodedAssetName = "output-f861dc5c-d7b3",
                StreamingLocatorName = "sipetrik-test-locator"
            };

            await target.ProvisionStreamAsync(request).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task TestStreamProvisioningRequestStorageService()
        {
            if (streamProvisioningRequestQueue == null)
            {
                throw new Exception("streamProvisioningRequestQueue is not initialized");
            }

            var uniqueness = Guid.NewGuid().ToString().Substring(0, 13);

            var target = new StreamProvisioningRequestStorageService(streamProvisioningRequestQueue, Mock.Of<ILogger>());

            var streamProvisioningRequest = new StreamProvisioningRequestModel
            {
                Id = Guid.NewGuid().ToString(),
                EncodedAssetMediaServiceAccountName = $"AccountName-{uniqueness}",
                EncodedAssetName = $"AssetName-{uniqueness}",
                StreamingLocatorName = $"StreamLocator-{uniqueness}"
            };

            Assert.IsNotNull(await target.CreateAsync(streamProvisioningRequest).ConfigureAwait(false));

            var result = await target.GetNextAsync().ConfigureAwait(false);

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
            if (jobVerificationRequestQueue == null)
            {
                throw new Exception("jobVerificationRequestQueue is not initialized");
            }

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

            var target = new JobVerificationRequestStorageService(jobVerificationRequestQueue, Mock.Of<ILogger>());

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
                JobRequest = jobRequest,
                MediaServiceAccountName = $"AccountName-{uniqueness}",
            };

            Assert.IsNotNull(await target.CreateAsync(jobVerificationRequest, new TimeSpan(0, 0, 5)).ConfigureAwait(false));

            var result = await target.GetNextAsync().ConfigureAwait(false);
            // we should not get a message right away, visibility should be set to 5 seconds
            Assert.IsNull(result);

            await Task.Delay(5500).ConfigureAwait(false);
            result = await target.GetNextAsync().ConfigureAwait(false);

            if (result == null)
            {
                throw new Exception("Got null from the queue");
            }

            Assert.AreEqual(jobVerificationRequest.Id, result.Id);
            Assert.AreEqual(jobVerificationRequest.JobRequest.JobInputs.Inputs.Count, result.JobRequest.JobInputs.Inputs.Count);
            var jobInputResult = (JobInputHttp)result.JobRequest.JobInputs.Inputs[0];
            Assert.AreEqual(jobInput.BaseUri, jobInputResult.BaseUri);
            Assert.AreEqual(jobInput.Files[0], jobInputResult.Files[0]);
            Assert.AreEqual(jobInput.Files[1], jobInputResult.Files[1]);
            Assert.AreEqual(jobInput.Label, jobInputResult.Label);
            Assert.AreEqual(jobVerificationRequest.JobRequest.JobName, result.JobRequest.JobName);
            Assert.AreEqual(jobRequest.OutputAssetName, result.JobRequest.OutputAssetName);
            Assert.AreEqual(jobRequest.TransformName, result.JobRequest.TransformName);
            Assert.AreEqual(jobVerificationRequest.JobId, result.JobId);
            Assert.AreEqual(jobVerificationRequest.MediaServiceAccountName, result.MediaServiceAccountName);
        }

        [TestMethod]
        public async Task TestJobVerificationService()
        {
            if (mediaServiceInstanceHealthTableStorageService == null)
            {
                throw new Exception("mediaServiceInstanceHealthTableStorageService is not initialized");
            }

            if (jobStatusTableStorageService == null)
            {
                throw new Exception("jobStatusTableStorageService is not initialized");
            }

            if (streamProvisioningRequestQueue == null)
            {
                throw new Exception("streamProvisioningRequestQueue is not initialized");
            }

            if (configService == null)
            {
                throw new Exception("configService is not initialized");
            }

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

            var mediaServiceInstanceHealthStorageService = new MediaServiceInstanceHealthStorageService(mediaServiceInstanceHealthTableStorageService, Mock.Of<ILogger>());
            var mediaServiceInstanceHealthService = new MediaServiceInstanceHealthService(mediaServiceInstanceHealthStorageService, Mock.Of<ILogger>());
            var jobStatusStorageService = new JobStatusStorageService(jobStatusTableStorageService, Mock.Of<ILogger>());
            var streamProvisioningRequestStorageService = new StreamProvisioningRequestStorageService(streamProvisioningRequestQueue, Mock.Of<ILogger>());

            var target = new JobVerificationService(mediaServiceInstanceHealthService,
                                                    jobStatusStorageService,
                                                    streamProvisioningRequestStorageService,
                                                    configService,
                                                    Mock.Of<ILogger>());

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
                JobRequest = jobRequest,
                MediaServiceAccountName = "sipetriktestmain",
            };

            Assert.IsNotNull(await target.VerifyJobAsync(jobVerificationRequest).ConfigureAwait(false));
        }

        private static JobRequestModel GenerateJobRequestModel()
        {
            var uniqueness = Guid.NewGuid().ToString().Substring(0, 13);

            var jobId = "jobId-" + uniqueness;
            var jobName = "jobName-" + uniqueness;
            var inputAssetName = "input-" + uniqueness;
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

        private bool AreEqualMediaServiceInstanceHealthModels(MediaServiceInstanceHealthModel m1, MediaServiceInstanceHealthModel m2)
        {
            return (m1.MediaServiceAccountName == m2.MediaServiceAccountName &&
                    m1.IsHealthy == m2.IsHealthy &&
                    m1.LastUpdated == m2.LastUpdated &&
                    m1.LastSuccessfulJob == m2.LastSuccessfulJob &&
                    m1.LastFailedJob == m2.LastFailedJob &&
                    m1.LastSubmittedJob == m2.LastSubmittedJob);
        }
    }
}
