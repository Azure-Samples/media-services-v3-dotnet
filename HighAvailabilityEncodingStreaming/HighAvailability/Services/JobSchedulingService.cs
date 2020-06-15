namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Interfaces;
    using HighAvailability.Models;
    using Microsoft.Azure.Management.Media;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Implements logic to submit new job to Azure Media Services.
    /// </summary>
    public class JobSchedulingService : IJobSchedulingService
    {
        /// <summary>
        /// How far in future to trigger job verification logic. This time should be longer than expected job duration.
        /// </summary>
        private readonly TimeSpan verificationDelay;

        /// <summary>
        /// Media Services Instance Health Service is used to determine next healthy Azure Media Services instance to submit a new job.
        /// </summary>
        private readonly IMediaServiceInstanceHealthService mediaServiceInstanceHealthService;

        /// <summary>
        /// Job verification requests is persisted in job verification request storage service to run job verification logic in future.
        /// </summary>
        private readonly IJobVerificationRequestStorageService jobVerificationRequestStorageService;

        /// <summary>
        /// Factory to get Azure Media Service instance client
        /// </summary>
        private readonly IMediaServiceInstanceFactory mediaServiceInstanceFactory;

        /// <summary>
        /// Configuration container
        /// </summary>
        private readonly IConfigService configService;

        /// <summary>
        /// Job output status service to persist job output status after initial job submission
        /// </summary>
        private readonly IJobOutputStatusStorageService jobOutputStatusStorageService;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mediaServiceInstanceHealthService">Media services instance health service</param>
        /// <param name="jobVerificationRequestStorageService">Job verification requests storage service </param>
        /// <param name="jobOutputStatusStorageService">Job output status service to persist job output status after initial job submission</param>
        /// <param name="mediaServiceInstanceFactory">Factory to get Azure Media Service instance client</param>
        /// <param name="configService">Configuration container</param>
        public JobSchedulingService(IMediaServiceInstanceHealthService mediaServiceInstanceHealthService,
                                    IJobVerificationRequestStorageService jobVerificationRequestStorageService,
                                    IJobOutputStatusStorageService jobOutputStatusStorageService,
                                    IMediaServiceInstanceFactory mediaServiceInstanceFactory,
                                    IConfigService configService)
        {
            this.mediaServiceInstanceHealthService = mediaServiceInstanceHealthService ?? throw new ArgumentNullException(nameof(mediaServiceInstanceHealthService));
            this.jobVerificationRequestStorageService = jobVerificationRequestStorageService ?? throw new ArgumentNullException(nameof(jobVerificationRequestStorageService));
            this.jobOutputStatusStorageService = jobOutputStatusStorageService ?? throw new ArgumentNullException(nameof(jobOutputStatusStorageService));
            this.mediaServiceInstanceFactory = mediaServiceInstanceFactory ?? throw new ArgumentNullException(nameof(mediaServiceInstanceFactory));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
            this.verificationDelay = new TimeSpan(0, this.configService.TimeDurationInMinutesToVerifyJobStatus, 0);
        }

        /// <summary>
        /// Submits job to Azure Media Services.
        /// </summary>
        /// <param name="jobRequestModel">Job to submit.</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Submitted job</returns>
        public async Task<Job> SubmitJobAsync(JobRequestModel jobRequestModel, ILogger logger)
        {
            logger.LogInformation($"JobSchedulingService::SubmitJobAsync started: jobRequestModel={LogHelper.FormatObjectForLog(jobRequestModel)}");

            // Get next available Azure Media Services instance
            var selectedInstanceName = await this.mediaServiceInstanceHealthService.GetNextAvailableInstanceAsync(logger).ConfigureAwait(false);
            logger.LogInformation($"JobSchedulingService::SubmitJobAsync selected healthy instance: MediaServiceAccountName={selectedInstanceName} jobRequestModel={LogHelper.FormatObjectForLog(jobRequestModel)}");

            // load configuration for specific instance
            var clientConfiguration = this.configService.MediaServiceInstanceConfiguration[selectedInstanceName];

            // get client
            var clientInstance = await this.mediaServiceInstanceFactory.GetMediaServiceInstanceAsync(selectedInstanceName, logger).ConfigureAwait(false);

            // In order to submit a new job, output asset has to be created first
            var asset = await clientInstance.Assets.CreateOrUpdateAsync(
                        clientConfiguration.ResourceGroup,
                        clientConfiguration.AccountName,
                        jobRequestModel.OutputAssetName,
                        new Asset()).ConfigureAwait(false);

            JobOutput[] jobOutputs = { new JobOutputAsset(jobRequestModel.OutputAssetName) };

            // submit new job
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

            logger.LogInformation($"JobSchedulingService::SubmitJobAsync successfully created job: job={LogHelper.FormatObjectForLog(job)}");

            // create job verification request
            var jobVerificationRequestModel = new JobVerificationRequestModel
            {
                Id = Guid.NewGuid().ToString(),
                JobId = job.Id,
                OriginalJobRequestModel = jobRequestModel,
                MediaServiceAccountName = selectedInstanceName,
                JobOutputAssetName = jobRequestModel.OutputAssetName,
                JobName = job.Name,
                RetryCount = 0  // initial submission, only certain number of retries are performed before skipping job verification retry, 
                                // see job verification service for more details
            };

            //create job output status record
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

            // in order to round robin among all healthy services, health service needs to know which instance has been used last
            // data is persisted in memory only for current process
            this.mediaServiceInstanceHealthService.RecordInstanceUsage(selectedInstanceName, logger);

            var retryCount = 3;
            var retryTimeOut = 1000;
            // Job is submitted at this point, failing to do any calls after this point would result in reprocessing this job request and submitting duplicate one.
            // It is OK to retry and ignore exception at the end. In current implementation based on Azure storage, it is very unlikely to fail in any of the below calls.
            do
            {
                try
                {
                    // persist initial job output status
                    await this.jobOutputStatusStorageService.CreateOrUpdateAsync(jobOutputStatusModel, logger).ConfigureAwait(false);

                    // persist job verification request. It is used to trigger logic to verify that job was completed and not stuck sometime in future.
                    var jobVerificationResult = await this.jobVerificationRequestStorageService.CreateAsync(jobVerificationRequestModel, this.verificationDelay, logger).ConfigureAwait(false);
                    logger.LogInformation($"JobSchedulingService::SubmitJobAsync successfully submitted jobVerificationModel: result={LogHelper.FormatObjectForLog(jobVerificationResult)}");

                    // no exception happened, let's break.
                    break;
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    logger.LogError($"JobSchedulingService::SubmitJobAsync got exception calling jobVerificationRequestStorageService.CreateAsync: retryCount={retryCount} message={e.Message} job={LogHelper.FormatObjectForLog(job)}");
                    retryCount--;
                    await Task.Delay(retryTimeOut).ConfigureAwait(false);
                }
            }
            while (retryCount > 0);

            logger.LogInformation($"JobSchedulingService::SubmitJobAsync completed: job={LogHelper.FormatObjectForLog(job)}");

            return job;
        }
    }
}
