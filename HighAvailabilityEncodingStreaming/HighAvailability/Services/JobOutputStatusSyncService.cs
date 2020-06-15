namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Interfaces;
    using HighAvailability.Models;
    using Microsoft.Azure.Management.Media;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using Microsoft.Rest.Azure.OData;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Class to implement methods to sync job output status from Azure Media Services APIs.
    /// </summary>
    public class JobOutputStatusSyncService : IJobOutputStatusSyncService
    {
        /// <summary>
        /// Service to load Azure Media Service instance health information.
        /// </summary>
        private readonly IMediaServiceInstanceHealthService mediaServiceInstanceHealthService;

        /// <summary>
        /// Storage service for job output status records.
        /// </summary>
        private readonly IJobOutputStatusStorageService jobOutputStatusStorageService;

        /// <summary>
        /// Factory to create Azure Media Services instance.
        /// </summary>
        private readonly IMediaServiceInstanceFactory mediaServiceInstanceFactory;

        /// <summary>
        /// Configuration container.
        /// </summary>
        private readonly IConfigService configService;

        /// <summary>
        /// This value is used to determine how far back to go to load job status.
        /// </summary>
        private int timeWindowToLoadJobsInMinutes = 480;

        /// <summary>
        /// This value is used to determine when to trigger manual job output status refresh.
        /// </summary>
        private int timeSinceLastUpdateToForceJobResyncInMinutes = 60;

        /// <summary>
        /// Default page size for Azure Media Services paged list APIs.
        /// </summary>
        private readonly int pageSize = 100;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mediaServiceInstanceHealthService">Service to load Azure Media Service instance health information</param>
        /// <param name="jobOutputStatusStorageService">Storage service for job output status records</param>
        /// <param name="mediaServiceInstanceFactory">Factory to create Azure Media Services instance</param>
        /// <param name="configService">Configuration container</param>
        public JobOutputStatusSyncService(IMediaServiceInstanceHealthService mediaServiceInstanceHealthService,
                                    IJobOutputStatusStorageService jobOutputStatusStorageService,
                                    IMediaServiceInstanceFactory mediaServiceInstanceFactory,
                                    IConfigService configService)
        {
            this.mediaServiceInstanceHealthService = mediaServiceInstanceHealthService ?? throw new ArgumentNullException(nameof(mediaServiceInstanceHealthService));
            this.jobOutputStatusStorageService = jobOutputStatusStorageService ?? throw new ArgumentNullException(nameof(jobOutputStatusStorageService));
            this.mediaServiceInstanceFactory = mediaServiceInstanceFactory ?? throw new ArgumentNullException(nameof(mediaServiceInstanceFactory));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
            this.timeWindowToLoadJobsInMinutes = this.configService.TimeWindowToLoadJobsInMinutes;
            this.timeSinceLastUpdateToForceJobResyncInMinutes = this.configService.TimeSinceLastUpdateToForceJobResyncInMinutes;
        }

        // <summary>
        /// EventGrid events sometimes are delayed or lost and manual re-sync is required. This method syncs job output status records between 
        /// job output status storage and Azure Media Services APIs. 
        /// </summary>
        /// <param name="currentTime">Current time, it is used to build time base criteria to load job status data.</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Task for async operation</returns>
        public async Task SyncJobOutputStatusAsync(DateTime currentTime, ILogger logger)
        {
            // Load list of all instance to sync data for
            var instances = await this.mediaServiceInstanceHealthService.ListAsync().ConfigureAwait(false);

            // each instance can be refreshed independently
            Parallel.ForEach(instances, new ParallelOptions { MaxDegreeOfParallelism = 5 }, (mediaServiceInstanceHealthModel) =>
            {
                // Load all jobs for a given time period and account name.
                // Since it is executed on separate thread, no need to async here.
                var allJobs = this.jobOutputStatusStorageService.ListByMediaServiceAccountNameAsync(mediaServiceInstanceHealthModel.MediaServiceAccountName, this.timeWindowToLoadJobsInMinutes).GetAwaiter().GetResult();
                logger.LogInformation($"JobOutputStatusSyncService::SyncJobOutputStatusAsync loaded jobs history: instanceName={mediaServiceInstanceHealthModel.MediaServiceAccountName} count={allJobs.Count()}");

                // Group all job output status records by job name.
                var aggregatedData = allJobs.GroupBy(i => i.JobName);
                var jobsToRefresh = new List<JobOutputStatusModel>();
                var uniqueJobCountPerTransform = new Dictionary<string, int>();

                // Iterates through each job
                foreach (var jobData in aggregatedData)
                {
                    var finalStateReached = false;

                    // Determine if final state is reached and no need to do any refresh
                    if (jobData.Any(j => j.JobOutputState == JobState.Finished) ||
                        jobData.Any(j => j.JobOutputState == JobState.Error) ||
                        jobData.Any(j => j.JobOutputState == JobState.Canceled))
                    {
                        finalStateReached = true;
                    }

                    // Transform name is needed to load job from API, transform name is not parsed from event grid based records. 
                    // Need to got to original record that was created as part of job creation process.
                    var jobToAdd = jobData.FirstOrDefault(j => !string.IsNullOrEmpty(j.TransformName));

                    // if job output record with transform name is not found, have to skip this record, since transform name is required field to do other processing
                    if (jobToAdd == null)
                    {
                        continue;
                    }

                    if (!finalStateReached)
                    {
                        var lastUpdate = jobData.Max(j => j.EventTime);

                        // If final state is not reached, and job was not refreshed for longer than max threshold
                        if (currentTime.AddMinutes(-this.timeSinceLastUpdateToForceJobResyncInMinutes) > lastUpdate)
                        {
                            jobsToRefresh.Add(jobToAdd);
                        }
                    }

                    if (!uniqueJobCountPerTransform.ContainsKey(jobToAdd.TransformName))
                    {
                        uniqueJobCountPerTransform.Add(jobToAdd.TransformName, 0);
                    }

                    uniqueJobCountPerTransform[jobToAdd.TransformName] += 1;

                }

                this.RefreshJobOutputStatusAsync(mediaServiceInstanceHealthModel.MediaServiceAccountName, jobsToRefresh, uniqueJobCountPerTransform, logger).GetAwaiter().GetResult();
            });
        }

        /// <summary>
        /// Determines if individual Get calls or List class should be used to refresh job output status.
        /// </summary>
        /// <param name="mediaServiceAccountName">Account name to load</param>
        /// <param name="jobOutputStatusModels">List of job output status records to re-sync</param>
        /// <param name="totalNumberOfJobs">Total number of jobs available for a given instance</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Task for async operation</returns>
        private async Task RefreshJobOutputStatusAsync(string mediaServiceAccountName, IList<JobOutputStatusModel> jobOutputStatusModels, Dictionary<string, int> totalNumberOfJobs, ILogger logger)
        {
            if (jobOutputStatusModels.Any())
            {
                // Jobs can be loaded by transform name, need to group by that and do one by one
                var aggregatedDataByTransform = jobOutputStatusModels.GroupBy(i => i.TransformName);

                // Iterate through all transforms
                foreach (var jobOutputStatusModelsPerTransform in aggregatedDataByTransform)
                {
                    var transformName = jobOutputStatusModelsPerTransform.Key;
                    var count = jobOutputStatusModelsPerTransform.Count();

                    if (count == 0)
                    {
                        continue;
                    }

                    // Overall goal is to minimize number of calls to Azure Media Services APIs. 
                    // This logic determines if it is lower number calls to do individual Get calls for each missing job status record or
                    // load all jobs using list API.

                    var numberOfCallsToLoadAllJobsUsingListOperation = totalNumberOfJobs[transformName] / this.pageSize + 1;

                    // Job List operation is 8 times more expensive than single job Get operation. This data was loaded from back-end telemetry. 
                    var getOperationVsListOperationExpenseRatio = 8;

                    // Check if it is "cheaper" to load all missing job data using individual Get operation or load all jobs using List operation
                    if (count > numberOfCallsToLoadAllJobsUsingListOperation * getOperationVsListOperationExpenseRatio)
                    {
                        await this.RefreshJobOutputStatusUsingListAsync(mediaServiceAccountName, jobOutputStatusModels, transformName, logger).ConfigureAwait(false);
                    }
                    else
                    {
                        await this.RefreshJobOutputStatusUsingGetAsync(mediaServiceAccountName, jobOutputStatusModels, transformName, logger).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// Reloads job status for each job individually
        /// </summary>
        /// <param name="mediaServiceAccountName">Account name to load</param>
        /// <param name="jobOutputStatusModels">List of job output status records to re-sync</param>
        /// <param name="transformName">transform name</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Task for async operation</returns>
        private async Task RefreshJobOutputStatusUsingGetAsync(string mediaServiceAccountName, IList<JobOutputStatusModel> jobOutputStatusModels, string transformName, ILogger logger)
        {
            var clientConfiguration = this.configService.MediaServiceInstanceConfiguration[mediaServiceAccountName];

            var clientInstance = this.mediaServiceInstanceFactory.GetMediaServiceInstance(mediaServiceAccountName, logger);
            foreach (var jobOutputStatusModel in jobOutputStatusModels)
            {
                logger.LogInformation($"JobOutputStatusSyncService::RefreshJobOutputStatusUsingGetAsync reloading job status using API: mediaServiceInstanceName={mediaServiceAccountName} oldJobOutputStatusModel={LogHelper.FormatObjectForLog(jobOutputStatusModel)}");

                // Load job using Get API
                var job = await clientInstance.Jobs.GetAsync(
                            clientConfiguration.ResourceGroup,
                            clientConfiguration.AccountName,
                            transformName,
                            jobOutputStatusModel.JobName).ConfigureAwait(false);

                logger.LogInformation($"JobOutputStatusSyncService::RefreshJobOutputStatusUsingGetAsync loaded job data from API: job={LogHelper.FormatObjectForLog(job)}");

                await this.UpdateJobOutputStatusAsync(mediaServiceAccountName, jobOutputStatusModel, job, logger).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Reloads job status using List API 
        /// </summary>
        /// <param name="mediaServiceAccountName">Account name to load</param>
        /// <param name="jobOutputStatusModels">List of job output status to re-sync</param>
        /// <param name="transformName">Transform name</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Task for async operation</returns>
        private async Task RefreshJobOutputStatusUsingListAsync(string mediaServiceAccountName, IList<JobOutputStatusModel> jobOutputStatusModels, string transformName, ILogger logger)
        {
            var clientConfiguration = this.configService.MediaServiceInstanceConfiguration[mediaServiceAccountName];

            var clientInstance = this.mediaServiceInstanceFactory.GetMediaServiceInstance(mediaServiceAccountName, logger);
            logger.LogInformation($"JobOutputStatusSyncService::RefreshJobOutputStatusUsingListAsync reloading job status using list API: mediaServiceInstanceName={mediaServiceAccountName} transformName={transformName}");

            // Need to load all jobs that were created within specific number of minutes.
            var dateFilter = DateTime.UtcNow.AddMinutes(-this.timeWindowToLoadJobsInMinutes).ToString("O", DateTimeFormatInfo.InvariantInfo);
            var odataQuery = new ODataQuery<Job>($"properties/created gt {dateFilter}");

            // Loads first page
            var firstPage = await clientInstance.Jobs.ListAsync(clientConfiguration.ResourceGroup, clientConfiguration.AccountName, transformName, odataQuery).ConfigureAwait(false);

            logger.LogInformation($"JobOutputStatusSyncService::RefreshJobOutputStatusUsingListAsync reloading job status using list API, loaded first page: mediaServiceInstanceName={mediaServiceAccountName} transformName={transformName}, count={firstPage.Count()}");

            var currentPage = firstPage;
            var everythingProcessed = false;
            while (!everythingProcessed)
            {
                // only interested in records that need to be re-sync, not every item loaded from API
                var matchedPairList = currentPage.Join(jobOutputStatusModels, (job) => job.Name, (jobOutputStatusModel) => jobOutputStatusModel.JobName, (jobOutputStatusModel, job) => (jobOutputStatusModel, job));

                foreach (var matchedPair in matchedPairList)
                {
                    await this.UpdateJobOutputStatusAsync(mediaServiceAccountName, matchedPair.job, matchedPair.jobOutputStatusModel, logger).ConfigureAwait(false);
                }

                everythingProcessed = true;
                // check if more items are available
                if (currentPage.NextPageLink != null)
                {
                    currentPage = await clientInstance.Jobs.ListNextAsync(currentPage.NextPageLink).ConfigureAwait(false);

                    logger.LogInformation($"JobOutputStatusSyncService::RefreshJobOutputStatusUsingListAsync reloading job status using list API, loaded next page: mediaServiceInstanceName={mediaServiceAccountName} transformName={transformName}, count={currentPage.Count()}");
                    // not done yet
                    everythingProcessed = false;
                }
            }
        }

        /// <summary>
        /// Stores job output status record to storage service
        /// </summary>
        /// <param name="mediaServiceAccountName">Account name to load</param>
        /// <param name="jobOutputStatusModel">Job output status to update</param>
        /// <param name="job">Job data loaded from API</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Task for async operation</returns>
        private async Task UpdateJobOutputStatusAsync(string mediaServiceAccountName, JobOutputStatusModel jobOutputStatusModel, Job job, ILogger logger)
        {
            if (job != null)
            {
                var jobOutputStatusModelFromAPI = new JobOutputStatusModel
                {
                    Id = Guid.NewGuid().ToString(),
                    EventTime = job.LastModified,
                    JobOutputState = MediaServicesHelper.GetJobOutputState(job, jobOutputStatusModel.JobOutputAssetName),
                    JobName = jobOutputStatusModel.JobName,
                    MediaServiceAccountName = mediaServiceAccountName,
                    JobOutputAssetName = jobOutputStatusModel.JobOutputAssetName,
                    TransformName = jobOutputStatusModel.TransformName
                };

                await this.jobOutputStatusStorageService.CreateOrUpdateAsync(jobOutputStatusModelFromAPI, logger).ConfigureAwait(false);
            }
        }
    }
}
