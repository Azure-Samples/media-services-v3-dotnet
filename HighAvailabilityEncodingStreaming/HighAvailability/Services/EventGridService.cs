// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace HighAvailability.Services
{
    using HighAvailability.Helpers;
    using HighAvailability.Interfaces;
    using HighAvailability.Models;
    using Microsoft.Azure.EventGrid.Models;
    using Microsoft.Extensions.Logging;
    using System.Text.RegularExpressions;

    /// <summary>
    /// This class implements EventGrid specific logic
    /// </summary>
    public class EventGridService : IEventGridService
    {
        /// <summary>
        /// Parses data from EventGridEvent and creates JobOutputStatusModel.
        /// </summary>
        /// <param name="eventGridEvent">Data to parse</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns>Parsed job output status model</returns>
        public JobOutputStatusModel ParseEventData(EventGridEvent eventGridEvent, ILogger logger)
        {
            var eventId = eventGridEvent.Id;
            var amsAccountResourceId = eventGridEvent.Topic;
            var jobId = eventGridEvent.Subject;
            var eventTime = eventGridEvent.EventTime;

            // jobName is parsed out of event subject.
            // Event subject example "transforms/TestTransform/jobs/jobName-5-691d0385-cfe3
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

            // account name is parsed from topic
            // topic example /subscriptions/<subscriptionId>/resourceGroups/<groupName>/providers/Microsoft.Media/mediaservices/<accountName>
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

            var mediaJobOutputStateChangeEventData = (MediaJobOutputStateChangeEventData)eventGridEvent.Data;
            var asset = (MediaJobOutputAsset)mediaJobOutputStateChangeEventData.Output;

            if (asset == null)
            {
                logger.LogInformation($"EventGridService::ParseEventData asset is null");
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
                IsSystemError = MediaServicesHelper.HasRetriableError(asset)
            };
            logger.LogInformation($"EventGridService::ParseEventData successfully parsed, jobOutputStatusModel={LogHelper.FormatObjectForLog(jobOutputStatusModel)}");

            return jobOutputStatusModel;
        }
    }
}
