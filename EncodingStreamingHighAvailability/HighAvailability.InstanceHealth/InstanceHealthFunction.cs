namespace HighAvailability.InstanceHealth
{
    using HighAvailability.Helpers;
    using HighAvailability.Services;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    public class InstanceHealthFunction
    {
        private IMediaServiceInstanceHealthService mediaServiceInstanceHealthService { get; set; }

        private IJobStatusSyncService jobStatusSyncService { get; set; }

        public InstanceHealthFunction(IMediaServiceInstanceHealthService mediaServiceInstanceHealthService, IJobStatusSyncService jobStatusSyncService)
        {
            this.mediaServiceInstanceHealthService = mediaServiceInstanceHealthService ?? throw new ArgumentNullException(nameof(mediaServiceInstanceHealthService));
            this.jobStatusSyncService = jobStatusSyncService ?? throw new ArgumentNullException(nameof(jobStatusSyncService));
        }

        [FunctionName("InstanceHealthFunction")]
        public async Task Run([TimerTrigger("0 */5 * * * *")]TimerInfo timerInfo, ILogger logger)
        {
            try
            {
                logger.LogInformation($"InstanceHealthFunction::Run triggered timerInfo={LogHelper.FormatObjectForLog(timerInfo)}");

                var result = await this.mediaServiceInstanceHealthService.ReEvaluateMediaServicesHealthAsync(logger).ConfigureAwait(false);

                logger.LogInformation($"InstanceHealthFunction::Run completed, result={LogHelper.FormatObjectForLog(result)}");
            }
            catch (Exception e)
            {
                logger.LogError($"InstanceHealthFunction::Run failed, exception={e.Message}");
                throw;
            }
        }

        [FunctionName("JobStatusSyncFunction")]
        public async Task JobStatusSyncRun([TimerTrigger("0 */2 * * * *")]TimerInfo timerInfo, ILogger logger)
        {
            try
            {
                logger.LogInformation($"JobStatusSyncRun::Run triggered timerInfo={LogHelper.FormatObjectForLog(timerInfo)}");

                await this.jobStatusSyncService.SyncJobStatusAsync(DateTime.UtcNow, logger).ConfigureAwait(false);

                logger.LogInformation($"JobStatusSyncRun::Run completed");
            }
            catch (Exception e)
            {
                logger.LogError($"JobStatusSyncRun::Run failed, exception={e.Message}");
                throw;
            }
        }
    }
}
