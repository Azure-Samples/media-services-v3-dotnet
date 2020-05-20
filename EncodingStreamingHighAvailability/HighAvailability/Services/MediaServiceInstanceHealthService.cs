namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class MediaServiceInstanceHealthService : IMediaServiceInstanceHealthService
    {
        private readonly IMediaServiceInstanceHealthStorageService mediaServiceInstanceHealthStorageService;
        private readonly IJobOutputStatusStorageService jobOutputStatusStorageService;
        private readonly IConfigService configService;
        private int numberOfMinutesInProcessToMarkJobStuck = 60;
        private int timeWindowToLoadJobsInMinutes = 480;
        private float successRateForHealthyState = 0.9f;
        private float successRateForUnHealthyState = 0.5f;
        private ConcurrentDictionary<string, ulong> mediaServiceInstanceUsage = new ConcurrentDictionary<string, ulong>();

        public MediaServiceInstanceHealthService(IMediaServiceInstanceHealthStorageService mediaServiceInstanceHealthStorageService,
                                                    IJobOutputStatusStorageService jobOutputStatusStorageService,
                                                    IConfigService configService)
        {
            this.mediaServiceInstanceHealthStorageService = mediaServiceInstanceHealthStorageService ?? throw new ArgumentNullException(nameof(mediaServiceInstanceHealthStorageService));
            this.jobOutputStatusStorageService = jobOutputStatusStorageService ?? throw new ArgumentNullException(nameof(jobOutputStatusStorageService));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
            this.numberOfMinutesInProcessToMarkJobStuck = configService.NumberOfMinutesInProcessToMarkJobStuck;
            this.timeWindowToLoadJobsInMinutes = configService.TimeWindowToLoadJobsInMinutes;
            this.successRateForHealthyState = configService.SuccessRateForHealthyState;
            this.successRateForUnHealthyState = configService.SuccessRateForUnHealthyState;
        }

        public async Task<MediaServiceInstanceHealthModel> CreateOrUpdateAsync(MediaServiceInstanceHealthModel mediaServiceInstanceHealthModel, ILogger logger)
        {
            return await this.mediaServiceInstanceHealthStorageService.CreateOrUpdateAsync(mediaServiceInstanceHealthModel, logger).ConfigureAwait(false);
        }

        public async Task<IEnumerable<MediaServiceInstanceHealthModel>> ListAsync()
        {
            return await this.mediaServiceInstanceHealthStorageService.ListAsync().ConfigureAwait(false);
        }

        public async Task<string> GetNextAvailableInstanceAsync(ILogger logger)
        {
            var instance = string.Empty;

            var allInstances = await this.mediaServiceInstanceHealthStorageService.ListAsync().ConfigureAwait(false);

            var allHealthyInstances = allInstances.Where(i => i.HealthState == InstanceHealthState.Healthy && i.IsEnabled).Select(i => i.MediaServiceAccountName);

            if (!allHealthyInstances.Any())
            {
                logger.LogWarning($"MediaServiceInstanceHealthService::GetNextAvailableInstanceAsync: There are no healthy instances available, falling back to degraded instances");
                allHealthyInstances = allInstances.Where(i => i.HealthState == InstanceHealthState.Degraded && i.IsEnabled).Select(i => i.MediaServiceAccountName);

                if (!allHealthyInstances.Any())
                {
                    throw new Exception($"There are no healthy or degraded instances available, can not process job request");
                }
            }

            var candidates = allHealthyInstances.Except(this.mediaServiceInstanceUsage.Keys);

            if (candidates.Any())
            {
                instance = candidates.FirstOrDefault();
            }
            else
            {
                instance = this.mediaServiceInstanceUsage.Where(c => allHealthyInstances.Contains(c.Key)).OrderBy(c => c.Value).FirstOrDefault().Key;
            }

            logger.LogInformation($"MediaServiceInstanceHealthService::GetNextAvailableInstanceAsync: result={instance}");
            return instance;
        }

        public void RecordInstanceUsage(string mediaServiceName, ILogger logger)
        {
            this.mediaServiceInstanceUsage.AddOrUpdate(mediaServiceName, 1, (name, usage) => usage + 1);
            logger.LogInformation($"MediaServiceInstanceHealthService::RecordInstanceUsage: mediaServiceInstanceUsage={LogHelper.FormatObjectForLog(this.mediaServiceInstanceUsage)}");
        }

        public async Task<IEnumerable<MediaServiceInstanceHealthModel>> ReEvaluateMediaServicesHealthAsync(ILogger logger)
        {
            var instances = await this.ListAsync().ConfigureAwait(false);
            var updatedInstances = new List<MediaServiceInstanceHealthModel>();

            Parallel.ForEach(instances, new ParallelOptions { MaxDegreeOfParallelism = 5 }, (mediaServiceInstanceHealthModel) =>
            {
                var allJobs = this.jobOutputStatusStorageService.ListByMediaServiceAccountNameAsync(mediaServiceInstanceHealthModel.MediaServiceAccountName, this.timeWindowToLoadJobsInMinutes).GetAwaiter().GetResult();
                logger.LogInformation($"MediaServiceInstanceHealthService::ReEvaluateMediaServicesHealthAsync loaded jobs history: instanceName={mediaServiceInstanceHealthModel.MediaServiceAccountName} count={allJobs.Count()}");
                var aggregatedData = allJobs.GroupBy(i => i.JobName);
                var successCount = 0;
                var totalCount = 0;
                var inHealthyProgressCount = 0;
                foreach (var jobData in aggregatedData)
                {
                    totalCount++;
                    var finalStateReached = false;

                    if (jobData.Any(j => j.JobOutputState == JobState.Finished))
                    {
                        successCount++;
                        finalStateReached = true;
                    }

                    if (jobData.Any(j => j.JobOutputState == JobState.Error))
                    {
                        finalStateReached = true;
                    }

                    // do not count canceled jobs
                    if (jobData.Any(j => j.JobOutputState == JobState.Canceled))
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
                        if (duration.TotalMinutes < this.numberOfMinutesInProcessToMarkJobStuck)
                        {
                            inHealthyProgressCount++;
                        }
                    }
                }

                logger.LogInformation($"MediaServiceInstanceHealthService::ReEvaluateMediaServicesHealthAsync aggregated data: instanceName={mediaServiceInstanceHealthModel.MediaServiceAccountName} successCount={successCount} totalCount={totalCount} inHealthyProgressCount={inHealthyProgressCount}");

                // default is healthy
                var successRate = 1f;
                if (totalCount > 0)
                {
                    successRate = ((float)(successCount + inHealthyProgressCount)) / totalCount;
                }

                var state = InstanceHealthState.Degraded;
                if (successRate > this.successRateForHealthyState)
                {
                    state = InstanceHealthState.Healthy;
                }
                else if (successRate < this.successRateForUnHealthyState)
                {
                    state = InstanceHealthState.Unhealthy;
                }

                logger.LogInformation($"MediaServiceInstanceHealthService::ReEvaluateMediaServicesHealthAsync setting health state: instanceName={mediaServiceInstanceHealthModel.MediaServiceAccountName} state={state}");

                var updatedMediaServiceInstanceHealthModel = this.UpdateHealthStateAsync(mediaServiceInstanceHealthModel.MediaServiceAccountName, state, DateTime.UtcNow).GetAwaiter().GetResult();
                lock (updatedInstances)
                {
                    updatedInstances.Add(updatedMediaServiceInstanceHealthModel);
                }
            });

            return updatedInstances;
        }

        public async Task<MediaServiceInstanceHealthModel> UpdateHealthStateAsync(string mediaServiceName, InstanceHealthState instanceHealthState, DateTimeOffset eventDateTime)
        {
            return await this.mediaServiceInstanceHealthStorageService.UpdateHealthStateAsync(mediaServiceName, instanceHealthState, eventDateTime).ConfigureAwait(false);
        }
    }
}