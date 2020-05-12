namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using Microsoft.Azure.Management.Media;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public class JobSchedulerService : IJobSchedulerService
    {
        private readonly string transformName = "AdaptiveBitrate";
        // 10 minutes, very short for this test, should be longer for prod
        private readonly TimeSpan verificationDelay = new TimeSpan(0, 10, 0);
        private readonly IMediaServiceInstanceHealthService mediaServiceInstanceHealthService;
        private readonly IJobVerificationRequestStorageService jobVerificationRequestStorageService;
        private readonly IConfigService configService;

        public JobSchedulerService(IMediaServiceInstanceHealthService mediaServiceInstanceHealthService,
                                    IJobVerificationRequestStorageService jobVerificationRequestStorageService,
                                    IConfigService configService)
        {
            this.mediaServiceInstanceHealthService = mediaServiceInstanceHealthService ?? throw new ArgumentNullException(nameof(mediaServiceInstanceHealthService));
            this.jobVerificationRequestStorageService = jobVerificationRequestStorageService ?? throw new ArgumentNullException(nameof(jobVerificationRequestStorageService));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
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
                        IsHealthy = true,
                        LastFailedJob = DateTime.MinValue,
                        LastSuccessfulJob = DateTime.MinValue,
                        LastUpdated = DateTime.UtcNow,
                        LastSubmittedJob = DateTime.MinValue
                    }, logger).ConfigureAwait(false);
                }
            }

            logger.LogInformation($"JobSchedulerService::Initialization completed");
        }

        public async Task<Job> SubmitJobAsync(JobRequestModel jobRequestModel, ILogger logger)
        {
            if (jobRequestModel == null)
            {
                throw new ArgumentNullException(nameof(jobRequestModel));
            }

            logger.LogInformation($"JobSchedulerService::SubmitJobAsync started: jobRequestModel={LogHelper.FormatObjectForLog(jobRequestModel)}");

            var allInstances = await this.mediaServiceInstanceHealthService.ListAsync().ConfigureAwait(false);
            var selectedInstance = allInstances.Where(i => i.IsHealthy).OrderBy(i => i.LastSubmittedJob).FirstOrDefault();
            if (selectedInstance == null)
            {
                throw new Exception($"Could not find a healthy AMS instance, found total instance count={allInstances.Count()}");
            }

            logger.LogInformation($"JobSchedulerService::SubmitJobAsync selected healthy instance: MediaServiceAccountName={selectedInstance.MediaServiceAccountName} jobRequestModel={LogHelper.FormatObjectForLog(jobRequestModel)}");

            var clientConfiguration = this.configService.MediaServiceInstanceConfiguration[selectedInstance.MediaServiceAccountName];

            // AzureMediaServicesClient is not thread safe, creating new one every time for now
            using (var clientInstance = await MediaServicesHelper.CreateMediaServicesClientAsync(clientConfiguration).ConfigureAwait(false))
            {

                var inputAsset = await clientInstance.Assets.CreateOrUpdateAsync(
                    clientConfiguration.ResourceGroup,
                    clientConfiguration.AccountName,
                    jobRequestModel.InputAssetName,
                    new Asset()).ConfigureAwait(false);

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
                    JobRequest = jobRequestModel,
                    MediaServiceAccountName = selectedInstance.MediaServiceAccountName
                };

                var retryCount = 3;
                var retryTimeOut = 1000;
                // We do not want to fail whole method here, this is just status update, it is ok to retry and igonre exception at the end
                // we do not want to retry this, that will result in duplicate job submission
                do
                {
                    try
                    {
                        var jobVerificationResult = await this.jobVerificationRequestStorageService.CreateAsync(jobVerificationRequestModel, this.verificationDelay, logger).ConfigureAwait(false);
                        logger.LogInformation($"JobSchedulerService::SubmitJobAsync successfully submitted jobVerificationModel: result={LogHelper.FormatObjectForLog(jobVerificationResult)}");

                        var jobStatusUpdateResult = await this.mediaServiceInstanceHealthService.UpdateSubmittedJobStateAsync(selectedInstance.MediaServiceAccountName, job.Created).ConfigureAwait(false);
                        logger.LogInformation($"JobSchedulerService::SubmitJobAsync successfully submitted jobStatusUpdate: result={LogHelper.FormatObjectForLog(jobStatusUpdateResult)}");
                        break;
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        logger.LogError($"JobSchedulerService::SubmitJobAsync got exception calling jobVerificationRequestStorageService.CreateAsync or mediaServiceInstanceHealthService.UpdateSubmittedJobStateAsync: retryCount={retryCount} message={e.Message} job={LogHelper.FormatObjectForLog(job)}");
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
