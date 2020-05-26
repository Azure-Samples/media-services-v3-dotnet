namespace HighAvailability.Tests
{
    using HighAvailability.Interfaces;
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
            var jobOutputStatusStorageServiceMock = new Mock<IJobOutputStatusStorageService>();
            var configServiceMock = new Mock<IConfigService>();
            var mediaServiceInstanceHealthStorageService = new Mock<IMediaServiceInstanceHealthStorageService>();
            var currentTime = DateTime.UtcNow;

            var jobOutputStatusList = new List<JobOutputStatusModel>
            {
                new JobOutputStatusModel {JobName = "job1", MediaServiceAccountName = "account1", JobOutputState = JobState.Processing, EventTime = currentTime},
                new JobOutputStatusModel {JobName = "job1", MediaServiceAccountName = "account1", JobOutputState = JobState.Processing, EventTime = currentTime.AddSeconds(1)},
                new JobOutputStatusModel {JobName = "job1", MediaServiceAccountName = "account1", JobOutputState = JobState.Finished, EventTime = currentTime.AddSeconds(2)},
                new JobOutputStatusModel {JobName = "job2", MediaServiceAccountName = "account1", JobOutputState = JobState.Processing, EventTime = currentTime},
                new JobOutputStatusModel {JobName = "job2", MediaServiceAccountName = "account1", JobOutputState = JobState.Processing, EventTime = currentTime.AddSeconds(2)},
                new JobOutputStatusModel {JobName = "job2", MediaServiceAccountName = "account1", JobOutputState = JobState.Error, EventTime = currentTime.AddSeconds(3)},
                new JobOutputStatusModel {JobName = "job3", MediaServiceAccountName = "account1", JobOutputState = JobState.Processing, EventTime = currentTime},
                new JobOutputStatusModel {JobName = "job3", MediaServiceAccountName = "account1", JobOutputState = JobState.Processing, EventTime = currentTime.AddSeconds(2)},
                new JobOutputStatusModel {JobName = "job3", MediaServiceAccountName = "account1", JobOutputState = JobState.Processing, EventTime = currentTime.AddSeconds(3)},
                new JobOutputStatusModel {JobName = "job4", MediaServiceAccountName = "account1", JobOutputState = JobState.Processing, EventTime = currentTime},
                new JobOutputStatusModel {JobName = "job4", MediaServiceAccountName = "account1", JobOutputState = JobState.Processing, EventTime = currentTime.AddSeconds(2)},
                new JobOutputStatusModel {JobName = "job4", MediaServiceAccountName = "account1", JobOutputState = JobState.Processing, EventTime = currentTime.AddHours(3)}
            };

            var accounts = new List<MediaServiceInstanceHealthModel>
            {
                new MediaServiceInstanceHealthModel {MediaServiceAccountName = "account1", HealthState = InstanceHealthState.Healthy, LastUpdated = currentTime}
            };

            mediaServiceInstanceHealthStorageService.Setup(m => m.ListAsync()).ReturnsAsync(accounts);
            jobOutputStatusStorageServiceMock.Setup(j => j.ListByMediaServiceAccountNameAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(jobOutputStatusList);

            configServiceMock.SetupGet(c => c.NumberOfMinutesInProcessToMarkJobStuck).Returns(60);
            configServiceMock.SetupGet(c => c.TimeWindowToLoadJobsInMinutes).Returns(60);
            configServiceMock.SetupGet(c => c.SuccessRateForHealthyState).Returns(0.9f);
            configServiceMock.SetupGet(c => c.SuccessRateForHealthyState).Returns(0.7f);

            var target = new MediaServiceInstanceHealthService(mediaServiceInstanceHealthStorageService.Object, jobOutputStatusStorageServiceMock.Object, configServiceMock.Object);

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
            var jobOutputStatusStorageServiceMock = new Mock<IJobOutputStatusStorageService>();
            var configServiceMock = new Mock<IConfigService>();
            var mediaServiceInstanceHealthStorageService = new Mock<IMediaServiceInstanceHealthStorageService>();
            var currentTime = DateTime.UtcNow;

            var jobOutputStatusList = CreateTestData(currentTime);
            for (var i = 0; i < 10; i++)
            {
                jobOutputStatusList.AddRange(CreateTestData(currentTime));
            }

            var accounts = new List<MediaServiceInstanceHealthModel>
            {
                new MediaServiceInstanceHealthModel {MediaServiceAccountName = "account1", HealthState = InstanceHealthState.Healthy, LastUpdated = currentTime}
            };

            mediaServiceInstanceHealthStorageService.Setup(m => m.ListAsync()).ReturnsAsync(accounts);
            jobOutputStatusStorageServiceMock.Setup(j => j.ListByMediaServiceAccountNameAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(jobOutputStatusList);

            configServiceMock.SetupGet(c => c.NumberOfMinutesInProcessToMarkJobStuck).Returns(60);
            configServiceMock.SetupGet(c => c.TimeWindowToLoadJobsInMinutes).Returns(60);
            configServiceMock.SetupGet(c => c.SuccessRateForHealthyState).Returns(0.9f);
            configServiceMock.SetupGet(c => c.SuccessRateForHealthyState).Returns(0.7f);

            var target = new MediaServiceInstanceHealthService(mediaServiceInstanceHealthStorageService.Object, jobOutputStatusStorageServiceMock.Object, configServiceMock.Object);

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var result = await target.ReEvaluateMediaServicesHealthAsync(Mock.Of<ILogger>()).ConfigureAwait(false);

            stopWatch.Stop();

            var elapsed = stopWatch.Elapsed.TotalSeconds;
            Console.WriteLine($"It took {elapsed} seconds to run");
        }

        private static List<JobOutputStatusModel> CreateTestData(DateTime currentTime)
        {
            var uniqueness = Guid.NewGuid().ToString().Substring(0, 13);

            return new List<JobOutputStatusModel>
            {
                new JobOutputStatusModel {JobName = $"job1-{uniqueness}", MediaServiceAccountName = "account1", JobOutputState = JobState.Processing, EventTime = currentTime},
                new JobOutputStatusModel {JobName = $"job1-{uniqueness}", MediaServiceAccountName = "account1", JobOutputState = JobState.Processing, EventTime = currentTime.AddSeconds(1)},
                new JobOutputStatusModel {JobName = $"job1-{uniqueness}", MediaServiceAccountName = "account1", JobOutputState = JobState.Finished, EventTime = currentTime.AddSeconds(2)},
                new JobOutputStatusModel {JobName = $"job2-{uniqueness}", MediaServiceAccountName = "account1", JobOutputState = JobState.Processing, EventTime = currentTime},
                new JobOutputStatusModel {JobName = $"job2-{uniqueness}", MediaServiceAccountName = "account1", JobOutputState = JobState.Processing, EventTime = currentTime.AddSeconds(2)},
                new JobOutputStatusModel {JobName = $"job2-{uniqueness}", MediaServiceAccountName = "account1", JobOutputState = JobState.Error, EventTime = currentTime.AddSeconds(3)},
                new JobOutputStatusModel {JobName = $"job3-{uniqueness}", MediaServiceAccountName = "account1", JobOutputState = JobState.Processing, EventTime = currentTime},
                new JobOutputStatusModel {JobName = $"job3-{uniqueness}", MediaServiceAccountName = "account1", JobOutputState = JobState.Processing, EventTime = currentTime.AddSeconds(2)},
                new JobOutputStatusModel {JobName = $"job3-{uniqueness}", MediaServiceAccountName = "account1", JobOutputState = JobState.Processing, EventTime = currentTime.AddSeconds(3)},
                new JobOutputStatusModel {JobName = $"job4-{uniqueness}", MediaServiceAccountName = "account1", JobOutputState = JobState.Processing, EventTime = currentTime},
                new JobOutputStatusModel {JobName = $"job4-{uniqueness}", MediaServiceAccountName = "account1", JobOutputState = JobState.Processing, EventTime = currentTime.AddSeconds(2)},
                new JobOutputStatusModel {JobName = $"job4-{uniqueness}", MediaServiceAccountName = "account1", JobOutputState = JobState.Processing, EventTime = currentTime.AddHours(3)}
            };
        }
    }
}
