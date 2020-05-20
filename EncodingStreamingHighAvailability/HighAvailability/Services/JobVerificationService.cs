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
        private readonly IJobOutputStatusStorageService jobOutputStatusStorageService;
        private readonly IStreamProvisioningRequestStorageService streamProvisioningRequestStorageService;
        private readonly IJobVerificationRequestStorageService jobVerificationRequestStorageService;
        private readonly IConfigService configService;
        private readonly int maxNumberOfRetries = 2;

        public JobVerificationService(IMediaServiceInstanceHealthService mediaServiceInstanceHealthService,
                                    IJobOutputStatusStorageService jobOutputStatusStorageService,
                                    IStreamProvisioningRequestStorageService streamProvisioningRequestStorageService,
                                    IJobVerificationRequestStorageService jobVerificationRequestStorageService,
                                    IConfigService configService)
        {
            this.mediaServiceInstanceHealthService = mediaServiceInstanceHealthService ?? throw new ArgumentNullException(nameof(mediaServiceInstanceHealthService));
            this.jobOutputStatusStorageService = jobOutputStatusStorageService ?? throw new ArgumentNullException(nameof(jobOutputStatusStorageService));
            this.streamProvisioningRequestStorageService = streamProvisioningRequestStorageService ?? throw new ArgumentNullException(nameof(streamProvisioningRequestStorageService));
            this.jobVerificationRequestStorageService = jobVerificationRequestStorageService ?? throw new ArgumentNullException(nameof(jobVerificationRequestStorageService));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        public async Task<JobVerificationRequestModel> VerifyJobAsync(JobVerificationRequestModel jobVerificationRequestModel, ILogger logger)
        {
            logger.LogInformation($"JobVerificationService::VerifyJobAsync started: jobVerificationRequestModel={LogHelper.FormatObjectForLog(jobVerificationRequestModel)}");

            var jobOutputStatus = await this.jobOutputStatusStorageService.GetLatestJobOutputStatusAsync(jobVerificationRequestModel.JobName, jobVerificationRequestModel.JobOutputAssetName).ConfigureAwait(false);
            var jobOutputStatusLoadedFromAPI = false;

            if (jobOutputStatus?.JobOutputState != JobState.Finished && jobOutputStatus?.JobOutputState != JobState.Error && jobOutputStatus?.JobOutputState != JobState.Canceled)
            {
                var clientConfiguration = this.configService.MediaServiceInstanceConfiguration[jobVerificationRequestModel.MediaServiceAccountName];

                // AzureMediaServicesClient is not thread safe, creating new one every time for now
                using (var clientInstance = await MediaServicesHelper.CreateMediaServicesClientAsync(clientConfiguration).ConfigureAwait(false))
                {
                    logger.LogInformation($"JobVerificationService::VerifyJobAsync checking job status using API: mediaServiceInstanceName={jobVerificationRequestModel.MediaServiceAccountName}");

                    var job = await clientInstance.Jobs.GetAsync(clientConfiguration.ResourceGroup,
                        clientConfiguration.AccountName,
                        jobVerificationRequestModel.OriginalJobRequestModel.TransformName,
                        jobVerificationRequestModel.JobName).ConfigureAwait(false);

                    logger.LogInformation($"JobVerificationService::VerifyJobAsync loaded job data from API: job={LogHelper.FormatObjectForLog(job)}");

                    if (job != null)
                    {
                        jobOutputStatus = new JobOutputStatusModel
                        {
                            Id = Guid.NewGuid().ToString(),
                            EventTime = job.LastModified,
                            JobOutputState = job.State, // TBD this should be specific status from output
                            JobName = job.Name,
                            MediaServiceAccountName = jobVerificationRequestModel.MediaServiceAccountName,
                            JobOutputAssetName = jobVerificationRequestModel.JobOutputAssetName,
                            TransformName = jobVerificationRequestModel.OriginalJobRequestModel.TransformName,
                            IsSystemError = MediaServicesHelper.IsSystemError(job)
                        };

                        jobOutputStatusLoadedFromAPI = true;

                        await this.jobOutputStatusStorageService.CreateOrUpdateAsync(jobOutputStatus, logger).ConfigureAwait(false);
                    }
                }
            }

            logger.LogInformation($"JobVerificationService::VerifyJobAsync jobOutputStatus={LogHelper.FormatObjectForLog(jobOutputStatus)}");

            if (jobOutputStatus?.JobOutputState == JobState.Finished)
            {
                // if there is no status in job status storage, assumption is that job status function did not get that status from EventGrid and provisioning request needs to be created
                await this.ProcessFinishedJobAsync(jobVerificationRequestModel, jobOutputStatusLoadedFromAPI, logger).ConfigureAwait(false);

                logger.LogInformation($"JobVerificationService::VerifyJobAsync] job was completed successfully: jobOutputStatus={LogHelper.FormatObjectForLog(jobOutputStatus)}");
                return jobVerificationRequestModel;
            }

            if (jobOutputStatus?.JobOutputState == JobState.Error)
            {
                await this.ProcessFailedJob(jobVerificationRequestModel, jobOutputStatus, logger).ConfigureAwait(false);

                logger.LogInformation($"JobVerificationService::VerifyJobAsync] job failed: jobOutputStatus={LogHelper.FormatObjectForLog(jobOutputStatus)}");
                return jobVerificationRequestModel;
            }

            if (jobOutputStatus?.JobOutputState == JobState.Canceled)
            {
                logger.LogInformation($"JobVerificationService::VerifyJobAsync] job canceled: jobOutputStatus={LogHelper.FormatObjectForLog(jobOutputStatus)}");
                return jobVerificationRequestModel;
            }

            // At this point, job is stuck or there is no information about it in the system
            await this.ProcessStuckJob(jobVerificationRequestModel, logger).ConfigureAwait(false);

            logger.LogInformation($"JobVerificationService::VerifyJobAsync completed: job={LogHelper.FormatObjectForLog(jobVerificationRequestModel)}");

            return jobVerificationRequestModel;
        }

        private async Task ProcessFinishedJobAsync(JobVerificationRequestModel jobVerificationRequestModel, bool submitStreamProvisioningRequest, ILogger logger)
        {
            logger.LogInformation($"JobVerificationService::ProcessFinishedJob started: jobVerificationRequestModel={LogHelper.FormatObjectForLog(jobVerificationRequestModel)}");

            // check if stream provisioning requests needs to be submitted           
            if (submitStreamProvisioningRequest)
            {
                var streamProvisioningRequestResult = await this.streamProvisioningRequestStorageService.CreateAsync(
                   new StreamProvisioningRequestModel
                   {
                       Id = Guid.NewGuid().ToString(),
                       EncodedAssetMediaServiceAccountName = jobVerificationRequestModel.MediaServiceAccountName,
                       EncodedAssetName = jobVerificationRequestModel.JobOutputAssetName,
                       StreamingLocatorName = $"streaming-{jobVerificationRequestModel.OriginalJobRequestModel.OutputAssetName}"
                   }, logger).ConfigureAwait(false);

                logger.LogInformation($"JobVerificationService::ProcessFinishedJob stream provisioning request submitted for completed job: streamProvisioningRequestResult={LogHelper.FormatObjectForLog(streamProvisioningRequestResult)}");
            }

            logger.LogInformation($"JobVerificationService::ProcessFinishedJob completed: jobVerificationRequestModel={LogHelper.FormatObjectForLog(jobVerificationRequestModel)}");
        }

        private async Task ProcessFailedJob(JobVerificationRequestModel jobVerificationRequestModel, JobOutputStatusModel jobOutputStatusModel, ILogger logger)
        {
            logger.LogInformation($"JobVerificationService::ProcessFailedJob started: jobVerificationRequestModel={LogHelper.FormatObjectForLog(jobVerificationRequestModel)} jobOutputStatusModel={LogHelper.FormatObjectForLog(jobOutputStatusModel)}");
            // Job is resubmitted for system failures
            if (jobOutputStatusModel.IsSystemError)
            {
                // check if we need to resubmit job
                await this.ResubmitJob(jobVerificationRequestModel, logger).ConfigureAwait(false);
            }
            else
            {
                logger.LogInformation($"JobVerificationService::ProcessFailedJob job failed, not a system error, skipping retry: result={LogHelper.FormatObjectForLog(jobVerificationRequestModel)}");
            }

            logger.LogInformation($"JobVerificationService::ProcessFailedJob completed: jobVerificationRequestModel={LogHelper.FormatObjectForLog(jobVerificationRequestModel)} jobOutputStatusModel={LogHelper.FormatObjectForLog(jobOutputStatusModel)}");
        }

        private async Task ProcessStuckJob(JobVerificationRequestModel jobVerificationRequestModel, ILogger logger)
        {
            logger.LogInformation($"JobVerificationService::ProcessStuckJob started: jobVerificationRequestModel={LogHelper.FormatObjectForLog(jobVerificationRequestModel)}");

            await this.ResubmitJob(jobVerificationRequestModel, logger).ConfigureAwait(false);

            logger.LogInformation($"JobVerificationService::ProcessStuckJob completed: jobVerificationRequestModel={LogHelper.FormatObjectForLog(jobVerificationRequestModel)}");
        }

        private async Task ResubmitJob(JobVerificationRequestModel jobVerificationRequestModel, ILogger logger)
        {
            if (jobVerificationRequestModel.RetryCount < this.maxNumberOfRetries)
            {
                var selectedInstanceName = await this.mediaServiceInstanceHealthService.GetNextAvailableInstanceAsync(logger).ConfigureAwait(false);
                var clientConfiguration = this.configService.MediaServiceInstanceConfiguration[selectedInstanceName];
                using (var clientInstance = await MediaServicesHelper.CreateMediaServicesClientAsync(clientConfiguration).ConfigureAwait(false))
                {
                    jobVerificationRequestModel.RetryCount++;
                    // need new job name
                    var jobName = $"{jobVerificationRequestModel.OriginalJobRequestModel.JobName}-{jobVerificationRequestModel.RetryCount}";
                    var jobOutputAssetName = $"{jobVerificationRequestModel.OriginalJobRequestModel.OutputAssetName}-{jobVerificationRequestModel.RetryCount}";

                    var outputAsset = await clientInstance.Assets.CreateOrUpdateAsync(
                        clientConfiguration.ResourceGroup,
                        clientConfiguration.AccountName,
                        jobOutputAssetName,
                        new Asset()).ConfigureAwait(false);

                    JobOutput[] jobOutputs =
                    {
                        new JobOutputAsset(jobOutputAssetName)
                    };

                    var job = await clientInstance.Jobs.CreateAsync(
                       clientConfiguration.ResourceGroup,
                       clientConfiguration.AccountName,
                       jobVerificationRequestModel.OriginalJobRequestModel.TransformName,
                       jobName,
                       new Job
                       {
                           Input = jobVerificationRequestModel.OriginalJobRequestModel.JobInputs,
                           Outputs = jobOutputs,
                       }).ConfigureAwait(false);

                    logger.LogInformation($"JobVerificationService::ResubmitJob successfully re-submitted job: job={LogHelper.FormatObjectForLog(job)}");

                    jobVerificationRequestModel.JobId = job.Id;
                    jobVerificationRequestModel.MediaServiceAccountName = selectedInstanceName;
                    jobVerificationRequestModel.JobOutputAssetName = jobOutputAssetName;
                    jobVerificationRequestModel.JobName = job.Name;

                    var verificationDelay = new TimeSpan(0, this.configService.TimeDurationInMinutesToVerifyJobStatus * (jobVerificationRequestModel.RetryCount + 1), 0);

                    var jobVerificationResult = await this.jobVerificationRequestStorageService.CreateAsync(jobVerificationRequestModel, verificationDelay, logger).ConfigureAwait(false);
                    logger.LogInformation($"JobVerificationService::ResubmitJob successfully submitted jobVerificationModel: result={LogHelper.FormatObjectForLog(jobVerificationResult)}");

                    this.mediaServiceInstanceHealthService.RecordInstanceUsage(selectedInstanceName, logger);
                }
            }
            else
            {
                logger.LogInformation($"JobVerificationService::ResubmitJob max number of retries reached, skipping request: result={LogHelper.FormatObjectForLog(jobVerificationRequestModel)}");
            }
        }
    }
}
