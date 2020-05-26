namespace HighAvailability.InstanceHealth
{
    using HighAvailability.Helpers;
    using HighAvailability.Interfaces;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    public class InstanceHealthFunction
    {
        private IMediaServiceInstanceHealthService mediaServiceInstanceHealthService { get; set; }

        private IJobOutputStatusSyncService jobOutputStatusSyncService { get; set; }

        public InstanceHealthFunction(IMediaServiceInstanceHealthService mediaServiceInstanceHealthService, IJobOutputStatusSyncService jobOutputStatusSyncService)
        {
            this.mediaServiceInstanceHealthService = mediaServiceInstanceHealthService ?? throw new ArgumentNullException(nameof(mediaServiceInstanceHealthService));
            this.jobOutputStatusSyncService = jobOutputStatusSyncService ?? throw new ArgumentNullException(nameof(jobOutputStatusSyncService));
        }

        [FunctionName("InstanceHealthFunction")]
        public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timerInfo, ILogger logger)
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

        [FunctionName("JobOutputStatusSyncFunction")]
        public async Task JobOutputStatusSyncRun([TimerTrigger("0 */2 * * * *")] TimerInfo timerInfo, ILogger logger)
        {
            try
            {
                logger.LogInformation($"JobOutputStatusSyncFunction::Run triggered timerInfo={LogHelper.FormatObjectForLog(timerInfo)}");

                await this.jobOutputStatusSyncService.SyncJobOutputStatusAsync(DateTime.UtcNow, logger).ConfigureAwait(false);

                logger.LogInformation($"JobOutputStatusSyncFunction::Run completed");
            }
            catch (Exception e)
            {
                logger.LogError($"JobOutputStatusSyncFunction::Run failed, exception={e.Message}");
                throw;
            }
        }
    }
}
