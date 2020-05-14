namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using Microsoft.Azure.Management.Media;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class JobStatusSyncService : IJobStatusSyncService
    {
        private readonly IMediaServiceInstanceHealthService mediaServiceInstanceHealthService;
        private readonly IJobStatusStorageService jobStatusStorageService;
        private readonly IConfigService configService;
        private int timeWindowToLoadJobsInMinutes = 480;
        private int timeSinceLastUpdateToForceJobResyncInMinutes = 1;

        public JobStatusSyncService(IMediaServiceInstanceHealthService mediaServiceInstanceHealthService,
                                    IJobStatusStorageService jobStatusStorageService,
                                    IConfigService configService)
        {
            this.mediaServiceInstanceHealthService = mediaServiceInstanceHealthService ?? throw new ArgumentNullException(nameof(mediaServiceInstanceHealthService));
            this.jobStatusStorageService = jobStatusStorageService ?? throw new ArgumentNullException(nameof(jobStatusStorageService));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
            this.timeWindowToLoadJobsInMinutes = this.configService.TimeWindowInMinutesToLoadJobs;
        }

        public async Task SyncJobStatusAsync(DateTime currentTime, ILogger logger)
        {
            var instances = await mediaServiceInstanceHealthService.ListAsync().ConfigureAwait(false);

            Parallel.ForEach(instances, new ParallelOptions { MaxDegreeOfParallelism = 5 }, (mediaServiceInstanceHealthModel) =>
            {
                var allJobs = this.jobStatusStorageService.ListByMediaServiceAccountNameAsync(mediaServiceInstanceHealthModel.MediaServiceAccountName, this.timeWindowToLoadJobsInMinutes).GetAwaiter().GetResult();
                logger.LogInformation($"JobStatusSyncService::SyncJobStatusAsync loaded jobs history: instanceName={mediaServiceInstanceHealthModel.MediaServiceAccountName} count={allJobs.Count()}");
                var aggregatedData = allJobs.GroupBy(i => i.JobName);
                var jobsToRefresh = new List<JobStatusModel>();

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
                }

                this.RefreshJobStatusAsync(jobsToRefresh, allJobs.Count(), logger).GetAwaiter().GetResult();
            });
        }

        private async Task RefreshJobStatusAsync(IList<JobStatusModel> jobStatusModels, int totalNumberOfJobs, ILogger logger)
        {
            if (jobStatusModels.Any())
            {
                await this.RefreshJobStatusUsingGetAsync(jobStatusModels, logger).ConfigureAwait(false);
            }
        }

        private async Task RefreshJobStatusUsingGetAsync(IList<JobStatusModel> jobStatusModels, ILogger logger)
        {
            var mediaServiceAccountName = jobStatusModels.FirstOrDefault().MediaServiceAccountName;
            var clientConfiguration = this.configService.MediaServiceInstanceConfiguration[mediaServiceAccountName];

            using (var clientInstance = await MediaServicesHelper.CreateMediaServicesClientAsync(clientConfiguration).ConfigureAwait(false))
            {
                foreach (var jobStatusModel in jobStatusModels)
                {
                    logger.LogInformation($"JobStatusSyncService::RefreshJobStatusUsingGetAsync reloading job status using API: mediaServiceInstanceName={mediaServiceAccountName} oldJobStatusModel={LogHelper.FormatObjectForLog(jobStatusModel)}");

                    var job = await clientInstance.Jobs.GetAsync(clientConfiguration.ResourceGroup,
                        clientConfiguration.AccountName,
                        jobStatusModel.TransformName,
                        jobStatusModel.JobName).ConfigureAwait(false);

                    logger.LogInformation($"JobStatusSyncService::RefreshJobStatusUsingGetAsync loaded job data from API: job={LogHelper.FormatObjectForLog(job)}");

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
            }
        }

        private void RefreshJobStatusUsingList(IList<JobStatusModel> jobStatusModels)
        {

        }

    }
}
