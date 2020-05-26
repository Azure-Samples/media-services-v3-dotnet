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

    public class JobOutputStatusSyncService : IJobOutputStatusSyncService
    {
        private readonly IMediaServiceInstanceHealthService mediaServiceInstanceHealthService;
        private readonly IJobOutputStatusStorageService jobOutputStatusStorageService;
        private readonly IMediaServiceInstanceFactory mediaServiceInstanceFactory;
        private readonly IConfigService configService;
        private int timeWindowToLoadJobsInMinutes = 480;
        private int timeSinceLastUpdateToForceJobResyncInMinutes = 60;
        private readonly int pageSize = 100;

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

        public async Task SyncJobOutputStatusAsync(DateTime currentTime, ILogger logger)
        {
            var instances = await this.mediaServiceInstanceHealthService.ListAsync().ConfigureAwait(false);

            Parallel.ForEach(instances, new ParallelOptions { MaxDegreeOfParallelism = 5 }, (mediaServiceInstanceHealthModel) =>
            {
                var allJobs = this.jobOutputStatusStorageService.ListByMediaServiceAccountNameAsync(mediaServiceInstanceHealthModel.MediaServiceAccountName, this.timeWindowToLoadJobsInMinutes).GetAwaiter().GetResult();
                logger.LogInformation($"JobOutputStatusSyncService::SyncJobOutputStatusAsync loaded jobs history: instanceName={mediaServiceInstanceHealthModel.MediaServiceAccountName} count={allJobs.Count()}");
                var aggregatedData = allJobs.GroupBy(i => i.JobName);
                var jobsToRefresh = new List<JobOutputStatusModel>();
                var uniqueJobCount = 0;

                foreach (var jobData in aggregatedData)
                {
                    var finalStateReached = false;

                    if (jobData.Any(j => j.JobOutputState == JobState.Finished) ||
                        jobData.Any(j => j.JobOutputState == JobState.Error) ||
                        jobData.Any(j => j.JobOutputState == JobState.Canceled))
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

                this.RefreshJobOutputStatusAsync(mediaServiceInstanceHealthModel.MediaServiceAccountName, jobsToRefresh, uniqueJobCount, logger).GetAwaiter().GetResult();
            });
        }

        private async Task RefreshJobOutputStatusAsync(string mediaServiceAccountName, IList<JobOutputStatusModel> jobOutputStatusModels, int totalNumberOfJobs, ILogger logger)
        {
            if (jobOutputStatusModels.Any())
            {
                var aggregatedDataByTransform = jobOutputStatusModels.GroupBy(i => i.TransformName);
                foreach (var jobOutputStatusModelsPerTransform in aggregatedDataByTransform)
                {
                    var transformName = jobOutputStatusModelsPerTransform.Key;
                    var count = jobOutputStatusModelsPerTransform.Count();

                    if (count == 0)
                    {
                        continue;
                    }

                    if (count > totalNumberOfJobs / this.pageSize)
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

        private async Task RefreshJobOutputStatusUsingGetAsync(string mediaServiceAccountName, IList<JobOutputStatusModel> jobOutputStatusModels, string transformName, ILogger logger)
        {
            var clientConfiguration = this.configService.MediaServiceInstanceConfiguration[mediaServiceAccountName];

            var clientInstance = await this.mediaServiceInstanceFactory.GetMediaServiceInstanceAsync(mediaServiceAccountName).ConfigureAwait(false);
            foreach (var jobOutputStatusModel in jobOutputStatusModels)
            {
                logger.LogInformation($"JobOutputStatusSyncService::RefreshJobOutputStatusUsingGetAsync reloading job status using API: mediaServiceInstanceName={mediaServiceAccountName} oldJobOutputStatusModel={LogHelper.FormatObjectForLog(jobOutputStatusModel)}");

                var job = await clientInstance.Jobs.GetAsync(clientConfiguration.ResourceGroup,
                    clientConfiguration.AccountName,
                    transformName,
                    jobOutputStatusModel.JobName).ConfigureAwait(false);

                logger.LogInformation($"JobOutputStatusSyncService::RefreshJobOutputStatusUsingGetAsync loaded job data from API: job={LogHelper.FormatObjectForLog(job)}");

                await this.UpdateJobOutputStatusAsync(mediaServiceAccountName, jobOutputStatusModel, job, logger).ConfigureAwait(false);
            }
        }

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

        private async Task RefreshJobOutputStatusUsingListAsync(string mediaServiceAccountName, IList<JobOutputStatusModel> jobOutputStatusModels, string transformName, ILogger logger)
        {
            var clientConfiguration = this.configService.MediaServiceInstanceConfiguration[mediaServiceAccountName];

            var clientInstance = await this.mediaServiceInstanceFactory.GetMediaServiceInstanceAsync(mediaServiceAccountName).ConfigureAwait(false);
            logger.LogInformation($"JobOutputStatusSyncService::RefreshJobOutputStatusUsingListAsync reloading job status using list API: mediaServiceInstanceName={mediaServiceAccountName} transformName={transformName}");

            var dateFilter = DateTime.UtcNow.AddMinutes(-this.timeWindowToLoadJobsInMinutes).ToString("O", DateTimeFormatInfo.InvariantInfo);
            var odataQuery = new ODataQuery<Job>($"properties/created gt {dateFilter}");
            var firstPage = await clientInstance.Jobs.ListAsync(clientConfiguration.ResourceGroup, clientConfiguration.AccountName, transformName, odataQuery).ConfigureAwait(false);
            logger.LogInformation($"JobOutputStatusSyncService::RefreshJobOutputStatusUsingListAsync reloading job status using list API, loaded first page: mediaServiceInstanceName={mediaServiceAccountName} transformName={transformName}, count={firstPage.Count()}");

            var currentPage = firstPage;
            var everythingProcessed = false;
            while (!everythingProcessed)
            {
                var matchedPairList = currentPage.Join(jobOutputStatusModels, (job) => job.Name, (jobOutputStatusModel) => jobOutputStatusModel.JobName, (jobOutputStatusModel, job) => (jobOutputStatusModel, job));

                foreach (var matchedPair in matchedPairList)
                {
                    await this.UpdateJobOutputStatusAsync(mediaServiceAccountName, matchedPair.job, matchedPair.jobOutputStatusModel, logger).ConfigureAwait(false);
                }

                everythingProcessed = true;
                if (currentPage.NextPageLink != null)
                {
                    currentPage = await clientInstance.Jobs.ListNextAsync(currentPage.NextPageLink).ConfigureAwait(false);
                    logger.LogInformation($"JobOutputStatusSyncService::RefreshJobOutputStatusUsingListAsync reloading job status using list API, loaded next page: mediaServiceInstanceName={mediaServiceAccountName} transformName={transformName}, count={currentPage.Count()}");
                    everythingProcessed = false;
                }
            }
        }
    }
}
