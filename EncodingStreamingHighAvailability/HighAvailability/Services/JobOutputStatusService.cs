namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Interfaces;
    using HighAvailability.Models;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    public class JobOutputStatusService : IJobOutputStatusService
    {
        private readonly IJobOutputStatusStorageService jobOutputStatusStorageService;
        private readonly IProvisioningRequestStorageService provisioningRequestStorageService;

        public JobOutputStatusService(IJobOutputStatusStorageService jobOutputStatusStorageService,
                                   IProvisioningRequestStorageService provisioningRequestStorageService)
        {
            this.jobOutputStatusStorageService = jobOutputStatusStorageService ?? throw new ArgumentNullException(nameof(jobOutputStatusStorageService));
            this.provisioningRequestStorageService = provisioningRequestStorageService ?? throw new ArgumentNullException(nameof(provisioningRequestStorageService));
        }

        public async Task<JobOutputStatusModel> ProcessJobOutputStatusAsync(JobOutputStatusModel jobOutputStatusModel, ILogger logger)
        {
            logger.LogInformation($"JobOutputStatusService::ProcessJobOutputStatusAsync started: jobOutputStatusModel={LogHelper.FormatObjectForLog(jobOutputStatusModel)}");

            if (jobOutputStatusModel.JobOutputState == JobState.Finished)
            {
                var provisioningRequestResult = await this.provisioningRequestStorageService.CreateAsync(
                    new ProvisioningRequestModel
                    {
                        Id = Guid.NewGuid().ToString(),
                        EncodedAssetMediaServiceAccountName = jobOutputStatusModel.MediaServiceAccountName,
                        EncodedAssetName = jobOutputStatusModel.JobOutputAssetName,
                        StreamingLocatorName = $"streaming-{jobOutputStatusModel.JobOutputAssetName}"
                    }, logger).ConfigureAwait(false);

                logger.LogInformation($"JobOutputStatusService::ProcessJobOutputStatusAsync created stream provisioning request: result={LogHelper.FormatObjectForLog(provisioningRequestResult)}");

            }

            var jobOutputStatusResult = await this.jobOutputStatusStorageService.CreateOrUpdateAsync(jobOutputStatusModel, logger).ConfigureAwait(false);
            logger.LogInformation($"JobOutputStatusService::ProcessJobOutputStatusAsync completed: jobOutputStatusResult={LogHelper.FormatObjectForLog(jobOutputStatusResult)}");

            return jobOutputStatusResult;
        }
    }
}
