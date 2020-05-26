namespace HighAvailability.JobOutputStatus
{
    using HighAvailability.Helpers;
    using HighAvailability.Interfaces;
    using Microsoft.Azure.EventGrid;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.EventGrid;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public class JobOutputStatusFunction
    {
        private IJobOutputStatusService jobOutputStatusService { get; set; }

        private IEventGridService eventGridService { get; set; }

        public JobOutputStatusFunction(IJobOutputStatusService jobOutputStatusService, IEventGridService eventGridService)
        {
            this.jobOutputStatusService = jobOutputStatusService ?? throw new ArgumentNullException(nameof(jobOutputStatusService));
            this.eventGridService = eventGridService ?? throw new ArgumentNullException(nameof(eventGridService));
        }

        [FunctionName("JobOutputStatusFunction")]
        public async Task Run([EventGridTrigger] string eventGridEvent, ILogger logger)
        {
            try
            {
                logger.LogInformation($"JobOutputStatusFunction::Run triggered: message={LogHelper.FormatObjectForLog(eventGridEvent)}");

                var eventGridSubscriber = new EventGridSubscriber();
                var parsedEventGridEvents = eventGridSubscriber.DeserializeEventGridEvents($"[{eventGridEvent}]");

                var jobOutputStatusModel = this.eventGridService.ParseEventData(parsedEventGridEvents.FirstOrDefault(), logger);
                if (jobOutputStatusModel != null)
                {
                    var result = await this.jobOutputStatusService.ProcessJobOutputStatusAsync(jobOutputStatusModel, logger).ConfigureAwait(false);
                    logger.LogInformation($"JobOutputStatusFunction::Run completed: result={LogHelper.FormatObjectForLog(result)}");
                }
                else
                {
                    logger.LogInformation($"JobOutputStatusFunction::Run event data skipped: result={LogHelper.FormatObjectForLog(eventGridEvent)}");
                }
            }
            catch (Exception e)
            {
                logger.LogError($"JobOutputStatusFunction::Run failed: exception={e.Message} eventGridEvent={LogHelper.FormatObjectForLog(eventGridEvent)}");
                throw;
            }
        }
    }
}
