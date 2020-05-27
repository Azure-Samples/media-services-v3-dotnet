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

    /// <summary>
    /// Implements job output status Azure function. It is triggered by EventGrid events
    /// </summary>
    public class JobOutputStatusFunction
    {
        /// <summary>
        /// Service to process job output status events
        /// </summary>
        private IJobOutputStatusService jobOutputStatusService { get; set; }

        /// <summary>
        /// Service to to parse EvetGridEvents
        /// </summary>
        private IEventGridService eventGridService { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="jobOutputStatusService">Service to process job output status events</param>
        /// <param name="eventGridService">Service to to parse EvetGridEvents</param>
        public JobOutputStatusFunction(IJobOutputStatusService jobOutputStatusService, IEventGridService eventGridService)
        {
            this.jobOutputStatusService = jobOutputStatusService ?? throw new ArgumentNullException(nameof(jobOutputStatusService));
            this.eventGridService = eventGridService ?? throw new ArgumentNullException(nameof(eventGridService));
        }

        /// <summary>
        /// This function is triggered for events coming from EventGrid, it is registered for following events:
        /// Microsoft.Media.JobOutputFinished
        /// Microsoft.Media.JobOutputErrored
        /// Microsoft.Media.JobOutputCanceled
        /// Microsoft.Media.JobOutputProcessing
        /// Microsoft.Media.JobOutputScheduled
        /// Microsoft.Media.JobOutputCanceling
        /// See ARMDeployemnt/eventgridsetup.json for registration script
        /// </summary>
        /// <param name="eventGridEvent">Event data</param>
        /// <param name="logger">Logger to log data to App Insights</param>
        /// <returns></returns>
        [FunctionName("JobOutputStatusFunction")]
        public async Task Run([EventGridTrigger] string eventGridEvent, ILogger logger)
        {
            try
            {
                logger.LogInformation($"JobOutputStatusFunction::Run triggered: message={LogHelper.FormatObjectForLog(eventGridEvent)}");

                // Events use inheritance and in order to correctly deserialize them, EventGridSubscriber should be used
                var eventGridSubscriber = new EventGridSubscriber();

                // TBD 
                // as a work around, json is modified to have a list of events
                var parsedEventGridEvents = eventGridSubscriber.DeserializeEventGridEvents($"[{eventGridEvent}]");

                // there should be only one event triggered for EventGridTrigger
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
                // in case of error, exception is thrown in order to trigger reprocess. 
                throw;
            }
        }
    }
}
