namespace HighAvailability.JobStatus
{
    using HighAvailability.Helpers;
    using HighAvailability.Services;
    using Microsoft.Azure.EventGrid.Models;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.EventGrid;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    public class JobStatusFunction
    {
        private IJobStatusService jobStatusService { get; set; }

        private IEventGridService eventGridService { get; set; }

        public JobStatusFunction(IJobStatusService jobStatusService, IEventGridService eventGridService)
        {
            this.jobStatusService = jobStatusService ?? throw new ArgumentNullException(nameof(jobStatusService));
            this.eventGridService = eventGridService ?? throw new ArgumentNullException(nameof(eventGridService));
        }

        [FunctionName("JobStatusFunction")]
        public async Task Run([EventGridTrigger]EventGridEvent eventGridEvent, ILogger logger)
        {
            try
            {
                logger.LogInformation($"JobStatusFunction::Run triggered: message={LogHelper.FormatObjectForLog(eventGridEvent)}");
                var jobStatusModel = this.eventGridService.ParseEventData(eventGridEvent, logger);
                if (jobStatusModel != null)
                {
                    var result = await this.jobStatusService.ProcessJobStatusAsync(jobStatusModel, logger).ConfigureAwait(false);
                    logger.LogInformation($"JobStatusFunction::Run completed: result={LogHelper.FormatObjectForLog(result)}");
                }
                else
                {
                    logger.LogInformation($"JobStatusFunction::Run event data skipped: result={LogHelper.FormatObjectForLog(eventGridEvent)}");
                }
            }
            catch (Exception e)
            {
                logger.LogError($"JobSchedulerFunction::Run failed: exception={e.Message} eventGridEvent={LogHelper.FormatObjectForLog(eventGridEvent)}");
                throw;
            }
        }
    }
}
