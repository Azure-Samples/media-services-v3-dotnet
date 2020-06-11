namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Interfaces;
    using HighAvailability.Models;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Implements logic to keep track of Azure Media Services instance health records.
    /// </summary>
    public class MediaServiceInstanceHealthService : IMediaServiceInstanceHealthService
    {
        /// <summary>
        /// Storage service to persist Azure Media Services instance health records
        /// </summary>
        private readonly IMediaServiceInstanceHealthStorageService mediaServiceInstanceHealthStorageService;

        /// <summary>
        /// Job output status storage service is used to recalculate Azure Media Services instance health
        /// </summary>
        private readonly IJobOutputStatusStorageService jobOutputStatusStorageService;

        /// <summary>
        /// Storage service to persist status of all calls to Media Services APIs
        /// </summary>
        private readonly IMediaServiceCallHistoryStorageService mediaServiceCallHistoryStorageService;

        /// <summary>
        /// Default value for expected max number of minutes required to complete encoding job. If job stays in process longer, it is marked as "stuck" and this information is used to determine instance health.
        /// </summary>
        private int numberOfMinutesInProcessToMarkJobStuck = 60;

        /// <summary>
        /// Default value to determine how far back to go to load job status. 
        /// </summary>
        private int timeWindowToLoadJobsInMinutes = 480;

        /// <summary>
        /// Default value to determine how far back to go to load Azure Media Services call history. 
        /// </summary>
        private int timeWindowToLoadMediaServiceCallsInMinutes = 480;

        /// <summary>
        /// Default value of Success/Total job ration threshold to determine when Azure Media Service instance is healthy.
        /// </summary>
        private float successRateForHealthyState = 0.9f;

        /// <summary>
        /// Default value of Success/Total job ration threshold to determine when Azure Media Service instance is unhealthy.
        /// </summary>
        private float successRateForUnHealthyState = 0.5f;

        /// <summary>
        /// Tracks Azure Media Service instance usage for a given process.
        /// </summary>
        private ConcurrentDictionary<string, ulong> mediaServiceInstanceUsage = new ConcurrentDictionary<string, ulong>();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mediaServiceInstanceHealthStorageService">Storage service to persist status of all calls to Media Services APIs</param>
        /// <param name="jobOutputStatusStorageService">Job output status storage service is used to recalculate Azure Media Services instance health</param>
        /// <param name="mediaServiceCallHistoryStorageService">Service to store Media Services call history</param>
        /// <param name="configService">Configuration container</param>
        public MediaServiceInstanceHealthService(IMediaServiceInstanceHealthStorageService mediaServiceInstanceHealthStorageService,
                                                    IJobOutputStatusStorageService jobOutputStatusStorageService,
                                                    IMediaServiceCallHistoryStorageService mediaServiceCallHistoryStorageService,
                                                    IConfigService configService)
        {
            this.mediaServiceInstanceHealthStorageService = mediaServiceInstanceHealthStorageService ?? throw new ArgumentNullException(nameof(mediaServiceInstanceHealthStorageService));
            this.jobOutputStatusStorageService = jobOutputStatusStorageService ?? throw new ArgumentNullException(nameof(jobOutputStatusStorageService));
            this.mediaServiceCallHistoryStorageService = mediaServiceCallHistoryStorageService ?? throw new ArgumentNullException(nameof(mediaServiceCallHistoryStorageService));
            this.numberOfMinutesInProcessToMarkJobStuck = configService.NumberOfMinutesInProcessToMarkJobStuck;
            this.timeWindowToLoadJobsInMinutes = configService.TimeWindowToLoadJobsInMinutes;
            this.timeWindowToLoadMediaServiceCallsInMinutes = configService.TimeWindowToLoadMediaServiceCallsInMinutes;
            this.successRateForHealthyState = configService.SuccessRateForHealthyState;
            this.successRateForUnHealthyState = configService.SuccessRateForUnHealthyState;
        }

        /// <summary>
        /// Stores Azure Media Services instance health record
        /// </summary>
        /// <param name="mediaServiceInstanceHealthModel">Record to store</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Stored Azure Media Services instance health record</returns>
        public async Task<MediaServiceInstanceHealthModel> CreateOrUpdateAsync(MediaServiceInstanceHealthModel mediaServiceInstanceHealthModel, ILogger logger)
        {
            return await this.mediaServiceInstanceHealthStorageService.CreateOrUpdateAsync(mediaServiceInstanceHealthModel, logger).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists all the media services instances with associated health status 
        /// </summary>
        /// <returns>List of Azure Media Services instance health records</returns>
        public async Task<IEnumerable<MediaServiceInstanceHealthModel>> ListAsync()
        {
            return await this.mediaServiceInstanceHealthStorageService.ListAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Gets next available Azure Media Services instance to process job.
        /// </summary>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Azure Media Services instance account name</returns>
        public async Task<string> GetNextAvailableInstanceAsync(ILogger logger)
        {
            var instance = string.Empty;

            //Get all instances
            var allInstances = await this.mediaServiceInstanceHealthStorageService.ListAsync().ConfigureAwait(false);

            // Filter down to healthy only and that are enabled. 
            var allHealthyInstances = allInstances.Where(i => i.HealthState == InstanceHealthState.Healthy && i.IsEnabled).Select(i => i.MediaServiceAccountName);

            // if there are no healthy instances found, see if there are any in degraded state
            if (!allHealthyInstances.Any())
            {
                logger.LogWarning($"MediaServiceInstanceHealthService::GetNextAvailableInstanceAsync: There are no healthy instances available, falling back to degraded instances");

                allHealthyInstances = allInstances.Where(i => i.HealthState == InstanceHealthState.Degraded && i.IsEnabled).Select(i => i.MediaServiceAccountName);

                if (!allHealthyInstances.Any())
                {
                    // fail request if no instances are found.
                    throw new Exception($"There are no healthy or degraded instances available, can not process job request");
                }
            }

            // Next, need to select single instance from all available ones.
            // First select instances that never were used before.
            var candidates = allHealthyInstances.Except(this.mediaServiceInstanceUsage.Keys);

            if (candidates.Any())
            {
                // if found, pick first available.
                instance = candidates.FirstOrDefault();
            }
            else
            {
                // if not found, pick least used
                instance = this.mediaServiceInstanceUsage.Where(c => allHealthyInstances.Contains(c.Key)).OrderBy(c => c.Value).FirstOrDefault().Key;
            }

            logger.LogInformation($"MediaServiceInstanceHealthService::GetNextAvailableInstanceAsync: result={instance}");
            return instance;
        }

        /// <summary>
        /// Records the fact that new job was submitted to a given Azure Media Services instance. 
        /// </summary>
        /// <param name="mediaServiceName">Azure Media Services instance account name.</param>
        /// <param name="logger">Logger to log data</param>
        public void RecordInstanceUsage(string mediaServiceName, ILogger logger)
        {
            // update usage, if new record, store 1, if existing record, increment value
            this.mediaServiceInstanceUsage.AddOrUpdate(mediaServiceName, 1, (name, usage) => usage + 1);
            logger.LogInformation($"MediaServiceInstanceHealthService::RecordInstanceUsage: mediaServiceInstanceUsage={LogHelper.FormatObjectForLog(this.mediaServiceInstanceUsage)}");
        }

        /// <summary>
        /// Recalculates health rating for each Azure Media Services instance health record
        /// </summary>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Updated list of Azure Media Services instance health records</returns>
        public async Task<IEnumerable<MediaServiceInstanceHealthModel>> ReEvaluateMediaServicesHealthAsync(ILogger logger)
        {
            // Get all available Azure Media Service instances.
            var instances = await this.ListAsync().ConfigureAwait(false);
            var updatedInstances = new List<MediaServiceInstanceHealthModel>();

            // all calculations can be done in parallel
            Parallel.ForEach(instances, new ParallelOptions { MaxDegreeOfParallelism = 5 }, (mediaServiceInstanceHealthModel) =>
            {
                // get all job status records for a given Azure Media Service instance and time period of how recent job status records were created
                var allJobs = this.jobOutputStatusStorageService.ListByMediaServiceAccountNameAsync(mediaServiceInstanceHealthModel.MediaServiceAccountName, this.timeWindowToLoadJobsInMinutes).GetAwaiter().GetResult();
                logger.LogInformation($"MediaServiceInstanceHealthService::ReEvaluateMediaServicesHealthAsync loaded jobs history: instanceName={mediaServiceInstanceHealthModel.MediaServiceAccountName} count={allJobs.Count()}");

                // group all records by job name
                var aggregatedData = allJobs.GroupBy(i => i.JobName);

                // for each Azure Media Service instance, calculate following statistics
                var jobsSuccessCount = 0;
                var jobsTotalCount = 0;
                var jobsInHealthyProgressCount = 0;
                foreach (var jobData in aggregatedData)
                {
                    jobsTotalCount++;
                    var finalStateReached = false;

                    if (jobData.Any(j => j.JobOutputState == JobState.Finished))
                    {
                        jobsSuccessCount++;
                        finalStateReached = true;
                    }

                    if (jobData.Any(j => j.JobOutputState == JobState.Error))
                    {
                        finalStateReached = true;
                    }

                    // do not count canceled jobs
                    if (jobData.Any(j => j.JobOutputState == JobState.Canceled))
                    {
                        jobsTotalCount--;
                        finalStateReached = true;
                    }

                    // check if job is stuck
                    if (!finalStateReached)
                    {
                        var firstUpdate = jobData.Min(j => j.EventTime);
                        var lastUdate = jobData.Max(j => j.EventTime);
                        var duration = lastUdate - firstUpdate;

                        // if duration below max threshold, it is "healthy" in progress, otherwise it is counted as "unhealthy"
                        if (duration.TotalMinutes < this.numberOfMinutesInProcessToMarkJobStuck)
                        {
                            jobsInHealthyProgressCount++;
                        }
                    }
                }

                logger.LogInformation($"MediaServiceInstanceHealthService::ReEvaluateMediaServicesHealthAsync jobs aggregated data: instanceName={mediaServiceInstanceHealthModel.MediaServiceAccountName} jobSuccessCount={jobsSuccessCount} jobTotalCount={jobsTotalCount} jobInHealthyProgressCount={jobsInHealthyProgressCount}");

                // default is healthy
                var jobsSuccessRate = 1f;

                // if no records exist, instance is healthy
                if (jobsTotalCount > 0)
                {
                    jobsSuccessRate = ((float)(jobsSuccessCount + jobsInHealthyProgressCount)) / jobsTotalCount;
                }

                var allCalls = this.mediaServiceCallHistoryStorageService.ListByMediaServiceAccountNameAsync(mediaServiceInstanceHealthModel.MediaServiceAccountName, this.timeWindowToLoadMediaServiceCallsInMinutes).GetAwaiter().GetResult().ToList();

                var callsTotalCount = allCalls.Count;
                var callsSuccessCount = allCalls.Count(c => (int)c.HttpStatus < 500);

                logger.LogInformation($"MediaServiceInstanceHealthService::ReEvaluateMediaServicesHealthAsync calls aggregated data: instanceName={mediaServiceInstanceHealthModel.MediaServiceAccountName} callsSuccessCount={callsSuccessCount} callsTotalCount={callsTotalCount}");

                // default is healthy
                var callsSuccessRate = 1f;

                // if no records exist, instance is healthy
                if (callsTotalCount > 0)
                {
                    callsSuccessRate = ((float)callsSuccessCount) / callsTotalCount;
                }

                var successRate = (jobsSuccessRate + callsSuccessRate) / 2;

                // degraded state is set if successRate is in between successRateForHealthyState and successRateForUnhealthyState
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

                // update newly calculated health rating
                var updatedMediaServiceInstanceHealthModel = this.mediaServiceInstanceHealthStorageService.UpdateHealthStateAsync(
                                                                mediaServiceInstanceHealthModel.MediaServiceAccountName,
                                                                state,
                                                                DateTime.UtcNow).GetAwaiter().GetResult();

                // since this is processed in parallel for all available Azure Media Service instances, need to sync on updating final list
                // this should not cause any performance issue, since it is simple update and rest of the method is significant more expensive to run.
                lock (updatedInstances)
                {
                    updatedInstances.Add(updatedMediaServiceInstanceHealthModel);
                }
            });

            return updatedInstances;
        }
    }
}