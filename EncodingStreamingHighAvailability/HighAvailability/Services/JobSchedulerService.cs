namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using Microsoft.Azure.Management.Media;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    public class JobSchedulerService : IJobSchedulerService
    {
        private readonly string transformName = "AdaptiveBitrate";
        // 10 minutes, very short for this test, should be longer for prod
        private readonly TimeSpan verificationDelay;
        private readonly IMediaServiceInstanceHealthService mediaServiceInstanceHealthService;
        private readonly IJobVerificationRequestStorageService jobVerificationRequestStorageService;
        private readonly IConfigService configService;
        private readonly IJobOutputStatusStorageService jobOutputStatusStorageService;

        public JobSchedulerService(IMediaServiceInstanceHealthService mediaServiceInstanceHealthService,
                                    IJobVerificationRequestStorageService jobVerificationRequestStorageService,
                                    IJobOutputStatusStorageService jobOutputStatusStorageService,
                                    IConfigService configService)
        {
            this.mediaServiceInstanceHealthService = mediaServiceInstanceHealthService ?? throw new ArgumentNullException(nameof(mediaServiceInstanceHealthService));
            this.jobVerificationRequestStorageService = jobVerificationRequestStorageService ?? throw new ArgumentNullException(nameof(jobVerificationRequestStorageService));
            this.jobOutputStatusStorageService = jobOutputStatusStorageService ?? throw new ArgumentNullException(nameof(jobOutputStatusStorageService));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
            this.verificationDelay = new TimeSpan(0, this.configService.TimeDurationInMinutesToVerifyJobStatus, 0);
        }

        public async Task Initialize(ILogger logger)
        {
            foreach (var config in this.configService.MediaServiceInstanceConfiguration)
            {
                using (var client = await MediaServicesHelper.CreateMediaServicesClientAsync(config.Value).ConfigureAwait(false))
                {
                    client.LongRunningOperationRetryTimeout = 2;

                    await MediaServicesHelper.EnsureTransformExists(
                        client,
                        config.Value.ResourceGroup,
                        config.Value.AccountName,
                        this.transformName,
                        new BuiltInStandardEncoderPreset(EncoderNamedPreset.AdaptiveStreaming)).ConfigureAwait(false);

                    await this.mediaServiceInstanceHealthService.CreateOrUpdateAsync(new MediaServiceInstanceHealthModel
                    {
                        MediaServiceAccountName = config.Value.AccountName,
                        HealthState = InstanceHealthState.Healthy,
                        LastUpdated = DateTime.UtcNow,
                        IsEnabled = true
                    },
                        logger).ConfigureAwait(false);
                }
            }

            logger.LogInformation($"JobSchedulerService::Initialization completed");
        }

        public async Task<Job> SubmitJobAsync(JobRequestModel jobRequestModel, ILogger logger)
        {
            logger.LogInformation($"JobSchedulerService::SubmitJobAsync started: jobRequestModel={LogHelper.FormatObjectForLog(jobRequestModel)}");

            var selectedInstanceName = await this.mediaServiceInstanceHealthService.GetNextAvailableInstanceAsync(logger).ConfigureAwait(false);
            logger.LogInformation($"JobSchedulerService::SubmitJobAsync selected healthy instance: MediaServiceAccountName={selectedInstanceName} jobRequestModel={LogHelper.FormatObjectForLog(jobRequestModel)}");

            var clientConfiguration = this.configService.MediaServiceInstanceConfiguration[selectedInstanceName];

            // AzureMediaServicesClient is not thread safe, creating new one every time for now
            using (var clientInstance = await MediaServicesHelper.CreateMediaServicesClientAsync(clientConfiguration).ConfigureAwait(false))
            {
                var outputAsset = await clientInstance.Assets.CreateOrUpdateAsync(
                    clientConfiguration.ResourceGroup,
                    clientConfiguration.AccountName,
                    jobRequestModel.OutputAssetName,
                    new Asset()).ConfigureAwait(false);

                JobOutput[] jobOutputs =
                {
                    new JobOutputAsset(jobRequestModel.OutputAssetName)
                };

                var job = await clientInstance.Jobs.CreateAsync(
                    clientConfiguration.ResourceGroup,
                    clientConfiguration.AccountName,
                    jobRequestModel.TransformName,
                    jobRequestModel.JobName,
                    new Job
                    {
                        Input = jobRequestModel.JobInputs,
                        Outputs = jobOutputs,
                    }).ConfigureAwait(false);

                logger.LogInformation($"JobSchedulerService::SubmitJobAsync successfully created job: job={LogHelper.FormatObjectForLog(job)}");

                var jobVerificationRequestModel = new JobVerificationRequestModel
                {
                    Id = Guid.NewGuid().ToString(),
                    JobId = job.Id,
                    OriginalJobRequestModel = jobRequestModel,
                    MediaServiceAccountName = selectedInstanceName,
                    JobOutputAssetName = jobRequestModel.OutputAssetName,
                    JobName = job.Name,
                    RetryCount = 0
                };

                var jobOutputStatusModel = new JobOutputStatusModel
                {
                    Id = Guid.NewGuid().ToString(),
                    EventTime = job.LastModified,
                    JobOutputState = MediaServicesHelper.GetJobOutputState(job, jobRequestModel.OutputAssetName),
                    JobName = job.Name,
                    MediaServiceAccountName = selectedInstanceName,
                    JobOutputAssetName = jobRequestModel.OutputAssetName,
                    TransformName = jobRequestModel.TransformName
                };

                this.mediaServiceInstanceHealthService.RecordInstanceUsage(selectedInstanceName, logger);

                var retryCount = 3;
                var retryTimeOut = 1000;
                // We do not want to fail whole method here, this is just status update, it is ok to retry and igonre exception at the end
                // we do not want to retry this, that will result in duplicate job submission
                do
                {
                    try
                    {
                        await this.jobOutputStatusStorageService.CreateOrUpdateAsync(jobOutputStatusModel, logger).ConfigureAwait(false);

                        var jobVerificationResult = await this.jobVerificationRequestStorageService.CreateAsync(jobVerificationRequestModel, this.verificationDelay, logger).ConfigureAwait(false);
                        logger.LogInformation($"JobSchedulerService::SubmitJobAsync successfully submitted jobVerificationModel: result={LogHelper.FormatObjectForLog(jobVerificationResult)}");
                        break;
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        logger.LogError($"JobSchedulerService::SubmitJobAsync got exception calling jobVerificationRequestStorageService.CreateAsync: retryCount={retryCount} message={e.Message} job={LogHelper.FormatObjectForLog(job)}");
                        retryCount--;
                        await Task.Delay(retryTimeOut).ConfigureAwait(false);
                    }
                }
                while (retryCount > 0);

                logger.LogInformation($"JobSchedulerService::SubmitJobAsync completed: job={LogHelper.FormatObjectForLog(job)}");

                return job;
            }
        }
    }
}
