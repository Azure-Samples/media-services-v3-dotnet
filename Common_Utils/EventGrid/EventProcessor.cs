// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Messaging.EventGrid.SystemEvents;
using Azure.Messaging.EventHubs.Processor;
using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Common_Utils
{
    /// <summary>
    /// Implementation of IEventProcessor to handle events from Event Hub.
    /// </summary>
    public class MediaServicesEventProcessor
    {
        private readonly AutoResetEvent jobWaitingEvent;
        private readonly string jobName;
        private readonly string liveEventName;

        public MediaServicesEventProcessor(string jobName, AutoResetEvent jobWaitingEvent, string liveEventName)
        {
            this.jobName = jobName;
            this.jobWaitingEvent = jobWaitingEvent;
            this.liveEventName = liveEventName;
        }

        public Task ProcessErrorAsync(ProcessErrorEventArgs args)
        {
            try
            {
                Console.WriteLine("Error in the EventProcessorClient");
                Console.WriteLine($"\tOperation: {args.Operation}");
                Console.WriteLine($"\tException: {args.Exception}");
                Console.WriteLine("");
            }
            catch
            {
                // It is very important that you always guard against
                // exceptions in your handler code; the processor does
                // not have enough understanding of your code to
                // determine the correct action to take.  Any
                // exceptions from your handlers go uncaught by
                // the processor and will NOT be handled in any
                // way.

            }

            return Task.CompletedTask;
        }

        public Task ProcessEventsAsync(ProcessEventArgs args)
        {
            if (args.HasEvent)
            {
                PrintJobEvent(args.Data);
            }

            return args.UpdateCheckpointAsync();
        }

        /// <summary>
        /// Parse and print Media Services events.
        /// </summary>
        /// <param name="eventData">Event Hub event data.</param>
        private void PrintJobEvent(Azure.Messaging.EventHubs.EventData eventData)
        {
            // data = Encoding.UTF8.GetString(eventData.EventBody);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var jsonArray = JsonSerializer.Deserialize<Azure.Messaging.EventGrid.EventGridEvent[]>(eventData.EventBody.ToString(), options);

            foreach (Azure.Messaging.EventGrid.EventGridEvent e in jsonArray)
            {
                var subject = e.Subject;
                var topic = e.Topic;
                var eventType = e.EventType;
                var eventTime = e.EventTime;
                string eventSourceName = Regex.Replace(subject, @"^.*/", "");


                if (eventSourceName != jobName && eventSourceName != liveEventName)
                {
                    return;
                }

                // Log the time and type of event
                Console.WriteLine($"{eventTime}   EventType: {eventType}");

                switch (eventType)
                {
                    // Job state change events
                    case "Microsoft.Media.JobStateChange":
                    case "Microsoft.Media.JobScheduled":
                    case "Microsoft.Media.JobProcessing":
                    case "Microsoft.Media.JobCanceling":
                    case "Microsoft.Media.JobFinished":
                    case "Microsoft.Media.JobCanceled":
                    case "Microsoft.Media.JobErrored":
                        {
                            MediaJobStateChangeEventData jobEventData = JsonSerializer.Deserialize<MediaJobStateChangeEventData>(e.Data.ToString(), options);

                            Console.WriteLine($"Job state changed for JobId: {eventSourceName} PreviousState: {jobEventData.PreviousState} State: {jobEventData.State}");

                            // For final states, send a message to notify that the job has finished.
                            if (eventType == "Microsoft.Media.JobFinished" || eventType == "Microsoft.Media.JobCanceled" || eventType == "Microsoft.Media.JobErrored")
                            {
                                // Job finished, send a message.
                                if (jobWaitingEvent != null)
                                {
                                    jobWaitingEvent.Set();
                                }
                            }
                        }
                        break;

                    // Job output state change events
                    case "Microsoft.Media.JobOutputStateChange":
                    case "Microsoft.Media.JobOutputScheduled":
                    case "Microsoft.Media.JobOutputProcessing":
                    case "Microsoft.Media.JobOutputCanceling":
                    case "Microsoft.Media.JobOutputFinished":
                    case "Microsoft.Media.JobOutputCanceled":
                    case "Microsoft.Media.JobOutputErrored":
                        {
                            MediaJobOutputStateChangeEventData jobEventData = JsonSerializer.Deserialize<MediaJobOutputStateChangeEventData>(e.Data.ToString(), options);

                            Console.WriteLine($"Job output state changed for JobId: {eventSourceName} PreviousState: {jobEventData.PreviousState} " +
                                $"State: {jobEventData.Output.State} Progress: {jobEventData.Output.Progress}%");
                        }
                        break;

                    // Job output progress event
                    case "Microsoft.Media.JobOutputProgress":
                        {
                            MediaJobOutputProgressEventData jobEventData = JsonSerializer.Deserialize<MediaJobOutputProgressEventData>(e.Data.ToString(), options);

                            Console.WriteLine($"Job output progress changed for JobId: {eventSourceName} Progress: {jobEventData.Progress}%");
                        }
                        break;

                    // LiveEvent Stream-level events
                    // See the following documentation for updated schemas  - https://docs.microsoft.com/azure/media-services/latest/monitoring/media-services-event-schemas#live-event-types
                    case "Microsoft.Media.LiveEventConnectionRejected":
                        {
                            MediaLiveEventConnectionRejectedEventData liveEventData = JsonSerializer.Deserialize<MediaLiveEventConnectionRejectedEventData>(e.Data.ToString(), options);
                            Console.WriteLine($"LiveEvent connection rejected. IngestUrl: {liveEventData.IngestUrl} StreamId: {liveEventData.StreamId} " +
                                $"EncoderIp: {liveEventData.EncoderIp} EncoderPort: {liveEventData.EncoderPort}");
                        }
                        break;
                    case "Microsoft.Media.LiveEventEncoderConnected":
                        {
                            MediaLiveEventEncoderConnectedEventData liveEventData = JsonSerializer.Deserialize<MediaLiveEventEncoderConnectedEventData>(e.Data.ToString(), options);
                            Console.WriteLine($"LiveEvent encoder connected. IngestUrl: {liveEventData.IngestUrl} StreamId: {liveEventData.StreamId} " +
                                $"EncoderIp: {liveEventData.EncoderIp} EncoderPort: {liveEventData.EncoderPort}");
                        }
                        break;
                    case "Microsoft.Media.LiveEventEncoderDisconnected":
                        {
                            MediaLiveEventEncoderDisconnectedEventData liveEventData = JsonSerializer.Deserialize<MediaLiveEventEncoderDisconnectedEventData>(e.Data.ToString(), options);
                            Console.WriteLine($"LiveEvent encoder disconnected. IngestUrl: {liveEventData.IngestUrl} StreamId: {liveEventData.StreamId} " +
                                $"EncoderIp: {liveEventData.EncoderIp} EncoderPort: {liveEventData.EncoderPort}");
                        }
                        break;

                    // LiveEvent Track-level events
                    // See the following documentation for updated schemas - https://docs.microsoft.com/azure/media-services/latest/monitoring/media-services-event-schemas#live-event-types
                    case "Microsoft.Media.LiveEventIncomingDataChunkDropped":
                        {
                            MediaLiveEventIncomingDataChunkDroppedEventData liveEventData = JsonSerializer.Deserialize<MediaLiveEventIncomingDataChunkDroppedEventData>(e.Data.ToString(), options);
                            Console.WriteLine($"LiveEvent data chunk dropped. LiveEventId: {eventSourceName} ResultCode: {liveEventData.ResultCode}");
                        }
                        break;
                    case "Microsoft.Media.LiveEventIncomingStreamReceived":
                        {
                            MediaLiveEventIncomingStreamReceivedEventData liveEventData = JsonSerializer.Deserialize<MediaLiveEventIncomingStreamReceivedEventData>(e.Data.ToString(), options);
                            Console.WriteLine($"LiveEvent incoming stream received. IngestUrl: {liveEventData.IngestUrl} EncoderIp: {liveEventData.EncoderIp} " +
                                $"EncoderPort: {liveEventData.EncoderPort}");
                        }
                        break;
                    case "Microsoft.Media.LiveEventIncomingStreamsOutOfSync":
                        {
                            //MediaLiveEventIncomingStreamsOutOfSyncEventData eventData = JsonSerializer.Deserialize<MediaLiveEventIncomingStreamsOutOfSyncEventData>(e.Data.ToString(), options);;
                            Console.WriteLine($"LiveEvent incoming audio and video streams are out of sync. LiveEventId: {eventSourceName}");
                        }
                        break;
                    case "Microsoft.Media.LiveEventIncomingVideoStreamsOutOfSync":
                        {
                            //MediaLiveEventIncomingVideoStreamsOutOfSyncEventData eventData =JsonSerializer.Deserialize<MediaLiveEventIncomingVideoStreamsOutOfSyncEventData>(e.Data.ToString(), options);;
                            Console.WriteLine($"LeveEvent incoming video streams are out of sync. LiveEventId: {eventSourceName}");
                        }
                        break;
                    case "Microsoft.Media.LiveEventIngestHeartbeat":
                        {
                            MediaLiveEventIngestHeartbeatEventData liveEventData = JsonSerializer.Deserialize<MediaLiveEventIngestHeartbeatEventData>(e.Data.ToString(), options);
                            Console.WriteLine($"LiveEvent ingest heart beat. TrackType: {liveEventData.TrackType} State: {liveEventData.State} Healthy: {liveEventData.Healthy}");
                        }
                        break;
                    case "Microsoft.Media.LiveEventTrackDiscontinuityDetected":
                        {
                            MediaLiveEventTrackDiscontinuityDetectedEventData liveEventData = JsonSerializer.Deserialize<MediaLiveEventTrackDiscontinuityDetectedEventData>(e.Data.ToString(), options);
                            Console.WriteLine($"LiveEvent discontinuity in the incoming track detected. LiveEventId: {eventSourceName} TrackType: {liveEventData.TrackType} " +
                                $"Discontinuity gap: {liveEventData.DiscontinuityGap}");
                        }
                        break;
                    case "Microsoft.Media.LiveEventChannelArchiveHeartbeatEvent":
                        {
                            Console.WriteLine($"LiveEvent archive heartbeat event detected. LiveEventId: {eventSourceName}");
                            Console.WriteLine(e.Data.ToString());

                        }
                        break;


                }
            }

        }
    }
}
