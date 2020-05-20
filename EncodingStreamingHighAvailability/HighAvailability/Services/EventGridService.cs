namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Models;
    using Microsoft.Azure.EventGrid;
    using Microsoft.Azure.EventGrid.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Text.RegularExpressions;

    public class EventGridService : IEventGridService
    {
        public JobOutputStatusModel ParseEventData(EventGridEvent eventGridEvent, ILogger logger)
        {
            var eventId = eventGridEvent.Id;
            var eventType = eventGridEvent.EventType;
            var amsAccountResourceId = eventGridEvent.Topic;
            var jobId = eventGridEvent.Subject;
            var eventTime = eventGridEvent.EventTime;

            var jobName = string.Empty;
            var match = Regex.Match(jobId, @".+/(?<jobname>.+)");
            if (match.Success)
            {
                jobName = match.Groups["jobname"].ToString();
            }
            if (string.IsNullOrEmpty(jobName))
            {
                logger.LogError($"EventGridService::ParseEventData failed to parse job name, eventGridEvent={LogHelper.FormatObjectForLog(eventGridEvent)}");
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
                logger.LogError($"EventGridService::ParseEventData failed to parse MSA account name, eventGridEvent={LogHelper.FormatObjectForLog(eventGridEvent)}");
                return null;
            }

            MediaJobOutputAsset asset;
            if (eventType.Equals(EventTypes.MediaJobOutputFinishedEvent, StringComparison.InvariantCultureIgnoreCase))
            {
                var mediaJobOutputFinishedEventData = (MediaJobOutputFinishedEventData)eventGridEvent.Data;
                asset = (MediaJobOutputAsset)mediaJobOutputFinishedEventData.Output;
            }
            else if (eventType.Equals(EventTypes.MediaJobOutputErroredEvent, StringComparison.InvariantCultureIgnoreCase))
            {
                var mediaJobOutputErroredEventData = (MediaJobOutputErroredEventData)eventGridEvent.Data;
                asset = (MediaJobOutputAsset)mediaJobOutputErroredEventData.Output;
            }
            else if (eventType.Equals(EventTypes.MediaJobOutputCanceledEvent, StringComparison.InvariantCultureIgnoreCase))
            {
                var mediaJobOutputCanceledEventData = (MediaJobOutputCanceledEventData)eventGridEvent.Data;
                asset = (MediaJobOutputAsset)mediaJobOutputCanceledEventData.Output;
            }
            else
            {
                logger.LogInformation($"EventGridService::ParseEventData eventType is not {EventTypes.MediaJobOutputFinishedEvent} or {EventTypes.MediaJobOutputErroredEvent} or {EventTypes.MediaJobOutputCanceledEvent} , eventGridEvent={LogHelper.FormatObjectForLog(eventGridEvent)}");
                return null;
            }

            var jobOutputStatusModel = new JobOutputStatusModel
            {
                Id = eventId,
                JobName = jobName,
                JobOutputAssetName = asset.AssetName,
                JobOutputState = asset.State.ToString(),
                EventTime = eventTime,
                MediaServiceAccountName = amsAccountName,
                IsSystemError = MediaServicesHelper.IsSystemError(asset)
            };
            logger.LogInformation($"EventGridService::ParseEventData successfully parsed, jobOutputStatusModel={LogHelper.FormatObjectForLog(jobOutputStatusModel)}");

            return jobOutputStatusModel;
        }
    }
}
