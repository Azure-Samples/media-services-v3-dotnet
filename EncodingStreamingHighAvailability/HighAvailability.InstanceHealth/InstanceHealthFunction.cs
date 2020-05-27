namespace HighAvailability.InstanceHealth
{
    using HighAvailability.Helpers;
    using HighAvailability.Interfaces;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Implements instance health Azure function
    /// </summary>
    public class InstanceHealthFunction
    {
        /// <summary>
        /// Media Services Instance Health Service is used to determine next healthy Azure Media Services instance to submit a new job.
        /// </summary>
        private IMediaServiceInstanceHealthService mediaServiceInstanceHealthService { get; set; }

        /// <summary>
        /// This service is used to sync job output status from Azure Media Services API to job output status storage.
        /// </summary>
        private IJobOutputStatusSyncService jobOutputStatusSyncService { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mediaServiceInstanceHealthService">Media Services Instance Health Service</param>
        /// <param name="jobOutputStatusSyncService">Job output status sync service</param>
        public InstanceHealthFunction(IMediaServiceInstanceHealthService mediaServiceInstanceHealthService, IJobOutputStatusSyncService jobOutputStatusSyncService)
        {
            this.mediaServiceInstanceHealthService = mediaServiceInstanceHealthService ?? throw new ArgumentNullException(nameof(mediaServiceInstanceHealthService));
            this.jobOutputStatusSyncService = jobOutputStatusSyncService ?? throw new ArgumentNullException(nameof(jobOutputStatusSyncService));
        }

        /// <summary>
        /// Runs Azure Media Services Instance health re-evaluation logic. 
        /// This function is triggered every 5 minutes, may need to be adjusted to trigger less often for busy environments.
        /// Exception is thrown from this function, it marks function as failed.
        /// </summary>
        /// <param name="timerInfo">timer info</param>
        /// <param name="logger">Logger to log data to App Insights</param>
        /// <returns>Task for async operation</returns>
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

        /// <summary>
        /// Runs job output status re-sync logic. 
        /// EventGrid events sometimes are lost and manual resync is required. This method triggers job output status sync between 
        /// job output status storage and Azure Media Services APIs. 
        /// This function is triggered every 10 minutes, may need to be adjusted to trigger less often for busy environments.
        /// Exception is thrown from this function, it marks function as failed.
        /// </summary>
        /// <param name="timerInfo">timer info</param>
        /// <param name="logger">Logger to log data to App Insights</param>
        /// <returns>Task for async operation</returns>
        [FunctionName("JobOutputStatusSyncFunction")]
        public async Task JobOutputStatusSyncRun([TimerTrigger("0 */10 * * * *")] TimerInfo timerInfo, ILogger logger)
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
