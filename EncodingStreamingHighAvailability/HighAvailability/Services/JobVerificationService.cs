namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using Microsoft.Azure.Management.Media;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    public class JobVerificationService : IJobVerificationService
    {
        private readonly IMediaServiceInstanceHealthService mediaServiceInstanceHealthService;
        private readonly IJobStatusStorageService jobStatusStorageService;
        private readonly IStreamProvisioningRequestStorageService streamProvisioningRequestStorageService;
        private readonly IConfigService configService;

        public JobVerificationService(IMediaServiceInstanceHealthService mediaServiceInstanceHealthService,
                                    IJobStatusStorageService jobStatusStorageService,
                                    IStreamProvisioningRequestStorageService streamProvisioningRequestStorageService,
                                    IConfigService configService)
        {
            this.mediaServiceInstanceHealthService = mediaServiceInstanceHealthService ?? throw new ArgumentNullException(nameof(mediaServiceInstanceHealthService));
            this.jobStatusStorageService = jobStatusStorageService ?? throw new ArgumentNullException(nameof(jobStatusStorageService));
            this.streamProvisioningRequestStorageService = streamProvisioningRequestStorageService ?? throw new ArgumentNullException(nameof(streamProvisioningRequestStorageService));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        public async Task<JobVerificationRequestModel> VerifyJobAsync(JobVerificationRequestModel jobVerificationRequestModel, ILogger logger)
        {
            logger.LogInformation($"JobVerificationService::VerifyJobAsync started: jobVerificationRequestModel={LogHelper.FormatObjectForLog(jobVerificationRequestModel)}");

            var jobStatus = await this.jobStatusStorageService.GetLatestJobStatusAsync(jobVerificationRequestModel.JobRequest.JobName).ConfigureAwait(false);
            if (jobStatus?.JobState == JobState.Finished)
            {
                logger.LogInformation($"JobVerificationService::VerifyJobAsync] job was completed successfully: jobStatus={LogHelper.FormatObjectForLog(jobStatus)}");
                return jobVerificationRequestModel;
            }

            if (jobStatus?.JobState == JobState.Error)
            {
                await this.ProcessFailedJob(jobVerificationRequestModel, logger).ConfigureAwait(false);
                return jobVerificationRequestModel;
            }

            logger.LogInformation($"JobVerificationService::VerifyJobAsync job state from JobStatusStorageService: jobStatus={jobStatus}");

            // if status does not exists or it is not Finished/Error, let's pull latest status, just to make sure that job is truly stuck
            var clientConfiguration = this.configService.MediaServiceInstanceConfiguration[jobVerificationRequestModel.MediaServiceAccountName];

            // AzureMediaServicesClient is not thread safe, creating new one every time for now
            using (var clientInstance = await MediaServicesHelper.CreateMediaServicesClientAsync(clientConfiguration).ConfigureAwait(false))
            {
                logger.LogInformation($"JobVerificationService::VerifyJobAsync checking job status using API: mediaServiceInstanceName={jobVerificationRequestModel.MediaServiceAccountName}");

                var job = await clientInstance.Jobs.GetAsync(clientConfiguration.ResourceGroup,
                    clientConfiguration.AccountName,
                    jobVerificationRequestModel.JobRequest.TransformName,
                    jobVerificationRequestModel.JobRequest.JobName).ConfigureAwait(false);

                logger.LogInformation($"JobVerificationService::VerifyJobAsync loaded job data from API: job={LogHelper.FormatObjectForLog(job)}");

                if (job?.State == JobState.Error)
                {
                    await this.ProcessFailedJob(jobVerificationRequestModel, logger).ConfigureAwait(false);
                    return jobVerificationRequestModel;
                }

                if (job?.State == JobState.Finished)
                {
                    // job is finished, we need to submit request to process stream provisioning
                    await this.ProcessFinishedJobAsync(jobVerificationRequestModel, logger).ConfigureAwait(false);
                    return jobVerificationRequestModel;
                }

                logger.LogInformation($"[Info] Job state from Media Services API: JobState={job?.State}");
                // At this point, job is either stuck or we could not find it at all
                await this.ProcessStuckJob(jobVerificationRequestModel, logger).ConfigureAwait(false);

                logger.LogInformation($"JobVerificationService::VerifyJobAsync completed: job={LogHelper.FormatObjectForLog(jobVerificationRequestModel)}");

                return jobVerificationRequestModel;
            }
        }

        private async Task ProcessFinishedJobAsync(JobVerificationRequestModel jobVerificationRequestModel, ILogger logger)
        {
            logger.LogInformation($"JobVerificationService::ProcessFinishedJob started: jobVerificationRequestModel={LogHelper.FormatObjectForLog(jobVerificationRequestModel)}");

            var streamProvisioningRequestResult = await this.streamProvisioningRequestStorageService.CreateAsync(
                new StreamProvisioningRequestModel
                {
                    Id = Guid.NewGuid().ToString(),
                    EncodedAssetMediaServiceAccountName = jobVerificationRequestModel.MediaServiceAccountName,
                    EncodedAssetName = jobVerificationRequestModel.JobRequest.OutputAssetName,
                    StreamingLocatorName = $"streaming-{jobVerificationRequestModel.JobRequest.OutputAssetName}"
                }, logger).ConfigureAwait(false);

            logger.LogInformation($"JobVerificationService::ProcessFinishedJob stream provisioning request submitted for completed job: streamProvisioningRequestResult={LogHelper.FormatObjectForLog(streamProvisioningRequestResult)}");

        }

        private async Task ProcessFailedJob(JobVerificationRequestModel jobVerificationRequestModel, ILogger logger)
        {
            // For now, let's just mark instances as unhealthy, later we may want to do some kind of other notification as well. 
            // It all depends on scenario, out of scope for v1
            logger.LogWarning($"JobVerificationService::ProcessFailedJob job failed, marking instance unhealthy: jobVerificationRequestModel={LogHelper.FormatObjectForLog(jobVerificationRequestModel)}");

            // var updateHealthStateResult = await this.mediaServiceInstanceHealthService.UpdateHealthStateAsync(jobVerificationRequestModel.MediaServiceAccountName, false, DateTime.UtcNow).ConfigureAwait(false);
            // logger.LogInformation($"JobVerificationService::ProcessFailedJob job failed, marked instance unhealthy: updateHealthStateResult={LogHelper.FormatObjectForLog(updateHealthStateResult)}");
        }

        private async Task ProcessStuckJob(JobVerificationRequestModel jobVerificationRequestModel, ILogger logger)
        {
            // For now, let's just mark instances as unhealthy, later we may want to do some kind of other notification as well. 
            // We could resubmit job to healthy region
            // It all depends on scenario, out of scope for v1
            logger.LogWarning($"JobVerificationService::ProcessStuckJob job got stuck, marking instance unhealthy: jobVerificationRequestModel={LogHelper.FormatObjectForLog(jobVerificationRequestModel)}");

            // var updateHealthStateResult = await this.mediaServiceInstanceHealthService.UpdateHealthStateAsync(jobVerificationRequestModel.MediaServiceAccountName, false, DateTime.UtcNow).ConfigureAwait(false);
            // logger.LogInformation($"JobVerificationService::ProcessStuckJob job failed, marked instance unhealthy: updateHealthStateResult={LogHelper.FormatObjectForLog(updateHealthStateResult)}");
        }
    }
}
