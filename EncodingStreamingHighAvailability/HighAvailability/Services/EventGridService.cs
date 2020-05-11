namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using Microsoft.Azure.EventGrid.Models;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System;
    using System.Text.RegularExpressions;

    public class EventGridService : IEventGridService
    {
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
        private readonly ILogger logger;

        public EventGridService(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public JobStatusModel? ParseEventData(EventGridEvent eventGridEvent)
        {
            if (eventGridEvent == null)
            {
                throw new ArgumentNullException(nameof(eventGridEvent));
            }

            var eventId = eventGridEvent.Id;
            var eventType = eventGridEvent.EventType;
            var amsAccountResourceId = eventGridEvent.Topic;
            var jobId = eventGridEvent.Subject;
            var eventDataStr = eventGridEvent.Data.ToString();
            var eventTime = eventGridEvent.EventTime;

            JobStatusModel? jobStatusModel = null;

            if (eventType.Equals("Microsoft.Media.JobOutputStateChange", StringComparison.InvariantCultureIgnoreCase))
            {
                var eventData = JsonConvert.DeserializeObject<JobOutputStateChangeEventDataModel>(eventDataStr, jsonSettings);

                var jobName = string.Empty;
                var match = Regex.Match(jobId, @".+/(?<jobname>.+)");
                if (match.Success)
                {
                    jobName = match.Groups["jobname"].ToString();
                }
                if (string.IsNullOrEmpty(jobName))
                {
                    this.logger.LogError($"EventGridService::ParseEventData failed to parse job name, eventGridEvent={LogHelper.FormatObjectForLog(eventGridEvent)}");
                    return null;
                }

                var amsAccountName = "";
                var matchAccount = Regex.Match(amsAccountResourceId, @".+/(?<accountname>.+)");
                if (matchAccount.Success)
                {
                    amsAccountName = matchAccount.Groups["accountname"].ToString();
                }
                if (string.IsNullOrEmpty(amsAccountName))
                {
                    this.logger.LogError($"EventGridService::ParseEventData failed to parse MSA account name, eventGridEvent={LogHelper.FormatObjectForLog(eventGridEvent)}");
                    return null;
                }

                jobStatusModel = new JobStatusModel
                {
                    Id = eventId,
                    JobName = jobName,
                    JobOutputAssetName = eventData.JobOutput.OutputAssetName,
                    JobState = eventData.JobOutput.State.ToString(),
                    EventTime = eventTime,
                    MediaServiceAccountName = amsAccountName
                };
                this.logger.LogError($"EventGridService::ParseEventData successfully parsed, jobStatusMode={LogHelper.FormatObjectForLog(jobStatusModel)}");
            }
            else
            {
                this.logger.LogInformation($"EventGridService::ParseEventData eventType is not Microsoft.Media.JobOutputStateChange, eventGridEvent={LogHelper.FormatObjectForLog(eventGridEvent)}");
            }

            return jobStatusModel;
        }
    }
}
