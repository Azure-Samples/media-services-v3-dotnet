namespace HighAvailability.Tests
{
    using HighAvailability.Models;
    using HighAvailability.Services;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;

    [TestClass]
    public class MediaServiceInstanceHealthServiceUnitTests
    {
        [TestMethod]
        public async Task TestReEvaluateMediaServicesHealthAsync()
        {
            var jobStatusStorageServiceMock = new Mock<IJobStatusStorageService>();
            var configServiceMock = new Mock<IConfigService>();
            var mediaServiceInstanceHealthStorageService = new Mock<IMediaServiceInstanceHealthStorageService>();
            var currentTime = DateTime.UtcNow;

            var jobStatusList = new List<JobStatusModel>
            {
                new JobStatusModel {JobName = "job1", MediaServiceAccountName = "account1", JobState = JobState.Processing, EventTime = currentTime},
                new JobStatusModel {JobName = "job1", MediaServiceAccountName = "account1", JobState = JobState.Processing, EventTime = currentTime.AddSeconds(1)},
                new JobStatusModel {JobName = "job1", MediaServiceAccountName = "account1", JobState = JobState.Finished, EventTime = currentTime.AddSeconds(2)},
                new JobStatusModel {JobName = "job2", MediaServiceAccountName = "account1", JobState = JobState.Processing, EventTime = currentTime},
                new JobStatusModel {JobName = "job2", MediaServiceAccountName = "account1", JobState = JobState.Processing, EventTime = currentTime.AddSeconds(2)},
                new JobStatusModel {JobName = "job2", MediaServiceAccountName = "account1", JobState = JobState.Error, EventTime = currentTime.AddSeconds(3)},
                new JobStatusModel {JobName = "job3", MediaServiceAccountName = "account1", JobState = JobState.Processing, EventTime = currentTime},
                new JobStatusModel {JobName = "job3", MediaServiceAccountName = "account1", JobState = JobState.Processing, EventTime = currentTime.AddSeconds(2)},
                new JobStatusModel {JobName = "job3", MediaServiceAccountName = "account1", JobState = JobState.Processing, EventTime = currentTime.AddSeconds(3)},
                new JobStatusModel {JobName = "job4", MediaServiceAccountName = "account1", JobState = JobState.Processing, EventTime = currentTime},
                new JobStatusModel {JobName = "job4", MediaServiceAccountName = "account1", JobState = JobState.Processing, EventTime = currentTime.AddSeconds(2)},
                new JobStatusModel {JobName = "job4", MediaServiceAccountName = "account1", JobState = JobState.Processing, EventTime = currentTime.AddHours(3)}
            };

            var accounts = new List<MediaServiceInstanceHealthModel>
            {
                new MediaServiceInstanceHealthModel {MediaServiceAccountName = "account1", HealthState = InstanceHealthState.Healthy, LastUpdated = currentTime}
            };

            mediaServiceInstanceHealthStorageService.Setup(m => m.ListAsync()).ReturnsAsync(accounts);
            jobStatusStorageServiceMock.Setup(j => j.ListByMediaServiceAccountNameAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(jobStatusList);

            configServiceMock.SetupGet(c => c.NumberOfMinutesInProcessToMarkJobStuck).Returns(60);
            configServiceMock.SetupGet(c => c.TimeWindowToLoadJobsInMinutes).Returns(60);
            configServiceMock.SetupGet(c => c.SuccessRateForHealthyState).Returns(0.9f);
            configServiceMock.SetupGet(c => c.SuccessRateForHealthyState).Returns(0.7f);

            var target = new MediaServiceInstanceHealthService(mediaServiceInstanceHealthStorageService.Object, jobStatusStorageServiceMock.Object, configServiceMock.Object);

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var result = await target.ReEvaluateMediaServicesHealthAsync(Mock.Of<ILogger>()).ConfigureAwait(false);
            stopWatch.Stop();

            var elapsed = stopWatch.Elapsed.TotalSeconds;
            Console.WriteLine($"It took {elapsed} seconds to run");
        }

        [TestMethod]
        public async Task TestLoadReEvaluateMediaServicesHealthAsync()
        {
            var jobStatusStorageServiceMock = new Mock<IJobStatusStorageService>();
            var configServiceMock = new Mock<IConfigService>();
            var mediaServiceInstanceHealthStorageService = new Mock<IMediaServiceInstanceHealthStorageService>();
            var currentTime = DateTime.UtcNow;

            var jobStatusList = CreateTestData(currentTime);
            for (var i = 0; i < 1000000; i++)
            {
                jobStatusList.AddRange(CreateTestData(currentTime));
            }

            var accounts = new List<MediaServiceInstanceHealthModel>
            {
                new MediaServiceInstanceHealthModel {MediaServiceAccountName = "account1", HealthState = InstanceHealthState.Healthy, LastUpdated = currentTime}
            };

            mediaServiceInstanceHealthStorageService.Setup(m => m.ListAsync()).ReturnsAsync(accounts);
            jobStatusStorageServiceMock.Setup(j => j.ListByMediaServiceAccountNameAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(jobStatusList);

            configServiceMock.SetupGet(c => c.NumberOfMinutesInProcessToMarkJobStuck).Returns(60);
            configServiceMock.SetupGet(c => c.TimeWindowToLoadJobsInMinutes).Returns(60);
            configServiceMock.SetupGet(c => c.SuccessRateForHealthyState).Returns(0.9f);
            configServiceMock.SetupGet(c => c.SuccessRateForHealthyState).Returns(0.7f);

            var target = new MediaServiceInstanceHealthService(mediaServiceInstanceHealthStorageService.Object, jobStatusStorageServiceMock.Object, configServiceMock.Object);

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var result = await target.ReEvaluateMediaServicesHealthAsync(Mock.Of<ILogger>()).ConfigureAwait(false);

            stopWatch.Stop();

            var elapsed = stopWatch.Elapsed.TotalSeconds;
            Console.WriteLine($"It took {elapsed} seconds to run");
        }

        private static List<JobStatusModel> CreateTestData(DateTime currentTime)
        {
            var uniqueness = Guid.NewGuid().ToString().Substring(0, 13);

            return new List<JobStatusModel>
            {
                new JobStatusModel {JobName = $"job1-{uniqueness}", MediaServiceAccountName = "account1", JobState = JobState.Processing, EventTime = currentTime},
                new JobStatusModel {JobName = $"job1-{uniqueness}", MediaServiceAccountName = "account1", JobState = JobState.Processing, EventTime = currentTime.AddSeconds(1)},
                new JobStatusModel {JobName = $"job1-{uniqueness}", MediaServiceAccountName = "account1", JobState = JobState.Finished, EventTime = currentTime.AddSeconds(2)},
                new JobStatusModel {JobName = $"job2-{uniqueness}", MediaServiceAccountName = "account1", JobState = JobState.Processing, EventTime = currentTime},
                new JobStatusModel {JobName = $"job2-{uniqueness}", MediaServiceAccountName = "account1", JobState = JobState.Processing, EventTime = currentTime.AddSeconds(2)},
                new JobStatusModel {JobName = $"job2-{uniqueness}", MediaServiceAccountName = "account1", JobState = JobState.Error, EventTime = currentTime.AddSeconds(3)},
                new JobStatusModel {JobName = $"job3-{uniqueness}", MediaServiceAccountName = "account1", JobState = JobState.Processing, EventTime = currentTime},
                new JobStatusModel {JobName = $"job3-{uniqueness}", MediaServiceAccountName = "account1", JobState = JobState.Processing, EventTime = currentTime.AddSeconds(2)},
                new JobStatusModel {JobName = $"job3-{uniqueness}", MediaServiceAccountName = "account1", JobState = JobState.Processing, EventTime = currentTime.AddSeconds(3)},
                new JobStatusModel {JobName = $"job4-{uniqueness}", MediaServiceAccountName = "account1", JobState = JobState.Processing, EventTime = currentTime},
                new JobStatusModel {JobName = $"job4-{uniqueness}", MediaServiceAccountName = "account1", JobState = JobState.Processing, EventTime = currentTime.AddSeconds(2)},
                new JobStatusModel {JobName = $"job4-{uniqueness}", MediaServiceAccountName = "account1", JobState = JobState.Processing, EventTime = currentTime.AddHours(3)}
            };
        }
    }
}
