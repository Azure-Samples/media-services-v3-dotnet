namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class MediaServiceInstanceHealthService : IMediaServiceInstanceHealthService
    {
        private readonly IMediaServiceInstanceHealthStorageService mediaServiceInstanceHealthStorageService;
        private readonly IJobStatusStorageService jobStatusStorageService;
        private readonly int numberOfMinutesInProcessToMarkJobStuck = 60;
        private readonly float successRateForHealthyState = 0.9f;
        private readonly float successRateForUnHealthyState = 0.5f;

        public MediaServiceInstanceHealthService(IMediaServiceInstanceHealthStorageService mediaServiceInstanceHealthStorageService, IJobStatusStorageService jobStatusStorageService)
        {
            this.mediaServiceInstanceHealthStorageService = mediaServiceInstanceHealthStorageService ?? throw new ArgumentNullException(nameof(mediaServiceInstanceHealthStorageService));
            this.jobStatusStorageService = jobStatusStorageService ?? throw new ArgumentNullException(nameof(jobStatusStorageService));
        }

        public async Task<MediaServiceInstanceHealthModel> CreateOrUpdateAsync(MediaServiceInstanceHealthModel mediaServiceInstanceHealthModel, ILogger logger)
        {
            return await this.mediaServiceInstanceHealthStorageService.CreateOrUpdateAsync(mediaServiceInstanceHealthModel, logger).ConfigureAwait(false);
        }

        public async Task<MediaServiceInstanceHealthModel> GetAsync(string mediaServiceName)
        {
            return await this.mediaServiceInstanceHealthStorageService.GetAsync(mediaServiceName).ConfigureAwait(false);
        }

        public async Task<InstanceHealthState> GetHealthStateAsync(string mediaServiceName)
        {
            var mediaServiceInstanceHealthModel = await this.mediaServiceInstanceHealthStorageService.GetAsync(mediaServiceName).ConfigureAwait(false);
            return mediaServiceInstanceHealthModel.HealthState;
        }

        public async Task<IEnumerable<MediaServiceInstanceHealthModel>> ListAsync()
        {
            return await this.mediaServiceInstanceHealthStorageService.ListAsync().ConfigureAwait(false);
        }

        public async Task<IEnumerable<string>> ListHealthyAsync(ILogger logger)
        {
            var result = (await this.mediaServiceInstanceHealthStorageService.ListAsync().ConfigureAwait(false)).Where(i => i.HealthState == InstanceHealthState.Healthy).Select(i => i.MediaServiceAccountName);
            logger.LogInformation($"MediaServiceInstanceHealthService::ListHealthyAsync: result={LogHelper.FormatObjectForLog(result)}");
            return result;
        }

        public async Task<IEnumerable<string>> ListUnHealthyAsync(ILogger logger)
        {
            var result = (await this.mediaServiceInstanceHealthStorageService.ListAsync().ConfigureAwait(false)).Where(i => i.HealthState != InstanceHealthState.Healthy).Select(i => i.MediaServiceAccountName);
            logger.LogInformation($"MediaServiceInstanceHealthService::ListUnHealthyAsync: result={LogHelper.FormatObjectForLog(result)}");
            return result;
        }

        public async Task<IEnumerable<MediaServiceInstanceHealthModel>> ReEvaluateMediaServicesHealthAsync()
        {
            var instances = await this.ListAsync().ConfigureAwait(false);

            Parallel.ForEach(instances, new ParallelOptions { MaxDegreeOfParallelism = 5 }, (mediaServiceInstanceHealthModel) =>
            {
                var allJobs = this.jobStatusStorageService.ListByMediaServiceAccountNameAsync(mediaServiceInstanceHealthModel.MediaServiceAccountName).GetAwaiter().GetResult();
                var aggregatedData = allJobs.GroupBy(i => i.JobName);
                int successCount = 0;
                int totalCount = 0;
                int inHealthyProgressCount = 0;
                foreach (var jobData in aggregatedData)
                {
                    totalCount++;
                    bool finalStateReached = false;

                    if (jobData.Any(j => j.JobState == JobState.Finished))
                    {
                        successCount++;
                        finalStateReached = true;
                    }

                    if (jobData.Any(j => j.JobState == JobState.Error))
                    {
                        finalStateReached = true;
                    }

                    // do not count canceled jobs
                    if (jobData.Any(j => j.JobState == JobState.Canceled))
                    {
                        totalCount--;
                        finalStateReached = true;
                    }

                    if (!finalStateReached)
                    {
                        var firstUpdate = jobData.Min(j => j.EventTime);
                        var lastUdate = jobData.Max(j => j.EventTime);
                        var duration = lastUdate - firstUpdate;

                        // if duration below max threshold, 
                        if (duration.TotalMinutes < numberOfMinutesInProcessToMarkJobStuck)
                        {
                            inHealthyProgressCount++;
                        }
                    }
                }

                float successRate = ((float)(successCount + inHealthyProgressCount)) / totalCount;
                InstanceHealthState state = InstanceHealthState.Degraded;
                if (successRate > successRateForHealthyState)
                {
                    state = InstanceHealthState.Healthy;
                }
                else if (successRate < successRateForUnHealthyState)
                {
                    state = InstanceHealthState.Unhealthy;
                }

                this.UpdateHealthStateAsync(mediaServiceInstanceHealthModel.MediaServiceAccountName, state, DateTime.UtcNow).Wait();
            });

            return null;
        }

        public async Task<MediaServiceInstanceHealthModel> UpdateHealthStateAsync(string mediaServiceName, InstanceHealthState instanceHealthState, DateTimeOffset eventDateTime)
        {
            return await this.mediaServiceInstanceHealthStorageService.UpdateHealthStateAsync(mediaServiceName, instanceHealthState, eventDateTime).ConfigureAwait(false);
        }       
    }
}