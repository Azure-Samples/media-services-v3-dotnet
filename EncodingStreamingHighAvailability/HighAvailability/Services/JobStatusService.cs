namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    public class JobStatusService : IJobStatusService
    {
        private readonly IJobStatusStorageService jobStatusStorageService;
        private readonly IStreamProvisioningRequestStorageService streamProvisioningRequestStorageService;

        public JobStatusService(IJobStatusStorageService jobStatusStorageService,
                                   IStreamProvisioningRequestStorageService streamProvisioningRequestStorageService)
        {
            this.jobStatusStorageService = jobStatusStorageService ?? throw new ArgumentNullException(nameof(jobStatusStorageService));
            this.streamProvisioningRequestStorageService = streamProvisioningRequestStorageService ?? throw new ArgumentNullException(nameof(streamProvisioningRequestStorageService));
        }

        public async Task<JobStatusModel> ProcessJobStatusAsync(JobStatusModel jobStatusModel, ILogger logger)
        {
            logger.LogInformation($"JobStatusService::ProcessJobStatusAsync started: jobStatusModel={LogHelper.FormatObjectForLog(jobStatusModel)}");

            if (jobStatusModel.JobState == JobState.Finished)
            {
                var streamProvisioningRequestResult = await this.streamProvisioningRequestStorageService.CreateAsync(
                    new StreamProvisioningRequestModel
                    {
                        Id = Guid.NewGuid().ToString(),
                        EncodedAssetMediaServiceAccountName = jobStatusModel.MediaServiceAccountName,
                        EncodedAssetName = jobStatusModel.JobOutputAssetName,
                        StreamingLocatorName = $"streaming-{jobStatusModel.JobOutputAssetName}"
                    }, logger).ConfigureAwait(false);

                logger.LogInformation($"JobStatusService::ProcessJobStatusAsync created stream provisioning request: result={LogHelper.FormatObjectForLog(streamProvisioningRequestResult)}");

            }

            var jobStatusResult = await this.jobStatusStorageService.CreateOrUpdateAsync(jobStatusModel, logger).ConfigureAwait(false);
            logger.LogInformation($"JobStatusService::ProcessJobStatusAsync completed: jobStatusResult={LogHelper.FormatObjectForLog(jobStatusResult)}");

            return jobStatusResult;
        }
    }
}
