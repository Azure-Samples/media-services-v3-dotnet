namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
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

    public class JobStatusSyncService : IJobStatusSyncService
    {
        private readonly IMediaServiceInstanceHealthService mediaServiceInstanceHealthService;
        private readonly IJobStatusStorageService jobStatusStorageService;
        private readonly IConfigService configService;
        private int timeWindowToLoadJobsInMinutes = 480;
        private int timeSinceLastUpdateToForceJobResyncInMinutes = 60;
        private readonly int pageSize = 100;

        public JobStatusSyncService(IMediaServiceInstanceHealthService mediaServiceInstanceHealthService,
                                    IJobStatusStorageService jobStatusStorageService,
                                    IConfigService configService)
        {
            this.mediaServiceInstanceHealthService = mediaServiceInstanceHealthService ?? throw new ArgumentNullException(nameof(mediaServiceInstanceHealthService));
            this.jobStatusStorageService = jobStatusStorageService ?? throw new ArgumentNullException(nameof(jobStatusStorageService));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
            this.timeWindowToLoadJobsInMinutes = this.configService.TimeWindowToLoadJobsInMinutes;
            this.timeSinceLastUpdateToForceJobResyncInMinutes = this.configService.TimeSinceLastUpdateToForceJobResyncInMinutes;
        }

        public async Task SyncJobStatusAsync(DateTime currentTime, ILogger logger)
        {
            var instances = await this.mediaServiceInstanceHealthService.ListAsync().ConfigureAwait(false);

            Parallel.ForEach(instances, new ParallelOptions { MaxDegreeOfParallelism = 5 }, (mediaServiceInstanceHealthModel) =>
            {
                var allJobs = this.jobStatusStorageService.ListByMediaServiceAccountNameAsync(mediaServiceInstanceHealthModel.MediaServiceAccountName, this.timeWindowToLoadJobsInMinutes).GetAwaiter().GetResult();
                logger.LogInformation($"JobStatusSyncService::SyncJobStatusAsync loaded jobs history: instanceName={mediaServiceInstanceHealthModel.MediaServiceAccountName} count={allJobs.Count()}");
                var aggregatedData = allJobs.GroupBy(i => i.JobName);
                var jobsToRefresh = new List<JobStatusModel>();
                var uniqueJobCount = 0;

                foreach (var jobData in aggregatedData)
                {
                    var finalStateReached = false;

                    if (jobData.Any(j => j.JobState == JobState.Finished) ||
                        jobData.Any(j => j.JobState == JobState.Error) ||
                        jobData.Any(j => j.JobState == JobState.Canceled))
                    {
                        finalStateReached = true;
                    }

                    if (!finalStateReached)
                    {
                        var lastUpdate = jobData.Max(j => j.EventTime);

                        // if duration below max threshold, 
                        if (currentTime.AddMinutes(-this.timeSinceLastUpdateToForceJobResyncInMinutes) > lastUpdate)
                        {
                            var jobToAdd = jobData.FirstOrDefault(j => !string.IsNullOrEmpty(j.TransformName));
                            if (jobToAdd != null)
                            {
                                jobsToRefresh.Add(jobToAdd);
                            }
                        }
                    }
                    uniqueJobCount++;
                }

                this.RefreshJobStatusAsync(mediaServiceInstanceHealthModel.MediaServiceAccountName, jobsToRefresh, uniqueJobCount, logger).GetAwaiter().GetResult();
            });
        }

        private async Task RefreshJobStatusAsync(string mediaServiceAccountName, IList<JobStatusModel> jobStatusModels, int totalNumberOfJobs, ILogger logger)
        {
            if (jobStatusModels.Any())
            {
                var aggregatedDataByTransform = jobStatusModels.GroupBy(i => i.TransformName);
                foreach (var jobStatusModelsPerTransform in aggregatedDataByTransform)
                {
                    var transformName = jobStatusModelsPerTransform.Key;
                    var count = jobStatusModelsPerTransform.Count();

                    if (count == 0)
                    {
                        continue;
                    }

                    if (count > totalNumberOfJobs / this.pageSize)
                    {
                        await this.RefreshJobStatusUsingList(mediaServiceAccountName, jobStatusModels, transformName, logger).ConfigureAwait(false);
                    }
                    else
                    {
                        await this.RefreshJobStatusUsingGetAsync(mediaServiceAccountName, jobStatusModels, transformName, logger).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task RefreshJobStatusUsingGetAsync(string mediaServiceAccountName, IList<JobStatusModel> jobStatusModels, string transformName, ILogger logger)
        {
            var clientConfiguration = this.configService.MediaServiceInstanceConfiguration[mediaServiceAccountName];

            using (var clientInstance = await MediaServicesHelper.CreateMediaServicesClientAsync(clientConfiguration).ConfigureAwait(false))
            {
                foreach (var jobStatusModel in jobStatusModels)
                {
                    logger.LogInformation($"JobStatusSyncService::RefreshJobStatusUsingGetAsync reloading job status using API: mediaServiceInstanceName={mediaServiceAccountName} oldJobStatusModel={LogHelper.FormatObjectForLog(jobStatusModel)}");

                    var job = await clientInstance.Jobs.GetAsync(clientConfiguration.ResourceGroup,
                        clientConfiguration.AccountName,
                        transformName,
                        jobStatusModel.JobName).ConfigureAwait(false);

                    logger.LogInformation($"JobStatusSyncService::RefreshJobStatusUsingGetAsync loaded job data from API: job={LogHelper.FormatObjectForLog(job)}");

                    await this.UpdateJobStatusAsync(mediaServiceAccountName, jobStatusModel, job, logger).ConfigureAwait(false);
                }
            }
        }

        private async Task UpdateJobStatusAsync(string mediaServiceAccountName, JobStatusModel jobStatusModel, Job job, ILogger logger)
        {
            if (job != null)
            {
                var jobStatusModelFromAPI = new JobStatusModel
                {
                    Id = Guid.NewGuid().ToString(),
                    EventTime = job.LastModified,
                    JobState = job.State,
                    JobName = jobStatusModel.JobName,
                    MediaServiceAccountName = mediaServiceAccountName,
                    JobOutputAssetName = jobStatusModel.JobOutputAssetName,
                    TransformName = jobStatusModel.TransformName
                };

                await this.jobStatusStorageService.CreateOrUpdateAsync(jobStatusModelFromAPI, logger).ConfigureAwait(false);
            }
        }

        private async Task RefreshJobStatusUsingList(string mediaServiceAccountName, IList<JobStatusModel> jobStatusModels, string transformName, ILogger logger)
        {
            var clientConfiguration = this.configService.MediaServiceInstanceConfiguration[mediaServiceAccountName];

            using (var clientInstance = await MediaServicesHelper.CreateMediaServicesClientAsync(clientConfiguration).ConfigureAwait(false))
            {
                logger.LogInformation($"JobStatusSyncService::RefreshJobStatusUsingList reloading job status using list API: mediaServiceInstanceName={mediaServiceAccountName} transformName={transformName}");
                var dateFilter = DateTime.UtcNow.AddMinutes(-this.timeWindowToLoadJobsInMinutes).ToString("O", DateTimeFormatInfo.InvariantInfo);
                var odataQuery = new ODataQuery<Job>($"properties/created gt {dateFilter}");
                var firstPage = await clientInstance.Jobs.ListAsync(clientConfiguration.ResourceGroup, clientConfiguration.AccountName, transformName, odataQuery).ConfigureAwait(false);
                logger.LogInformation($"JobStatusSyncService::RefreshJobStatusUsingList reloading job status using list API, loaded first page: mediaServiceInstanceName={mediaServiceAccountName} transformName={transformName}, count={firstPage.Count()}");
                var currentPage = firstPage;
                var everythingProcessed = false;
                while (!everythingProcessed)
                {
                    var matchedPairList = currentPage.Join(jobStatusModels, (job) => job.Name, (jobStatusModel) => jobStatusModel.JobName, (jobStatusModel, job) => (jobStatusModel, job));

                    foreach (var matchedPair in matchedPairList)
                    {
                        await this.UpdateJobStatusAsync(mediaServiceAccountName, matchedPair.job, matchedPair.jobStatusModel, logger).ConfigureAwait(false);
                    }

                    everythingProcessed = true;
                    if (currentPage.NextPageLink != null)
                    {
                        currentPage = await clientInstance.Jobs.ListNextAsync(currentPage.NextPageLink).ConfigureAwait(false);
                        logger.LogInformation($"JobStatusSyncService::RefreshJobStatusUsingList reloading job status using list API, loaded next page: mediaServiceInstanceName={mediaServiceAccountName} transformName={transformName}, count={currentPage.Count()}");
                        everythingProcessed = false;
                    }
                }
            }
        }
    }
}
