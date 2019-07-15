// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Microsoft.Azure.EventGrid.Models;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.IdentityModel.Tokens;

namespace BasicPlayReady
{
    /// <summary>
    /// Implementation of IEventProcessor to handle events from Event Hub.
    /// </summary>
    class MediaServicesEventProcessor : IEventProcessor
    {
        private readonly AutoResetEvent jobWaitingEvent;
        private readonly string jobName;

        public MediaServicesEventProcessor(string jobName, AutoResetEvent jobWaitingEvent)
        {
            this.jobName = jobName;
            this.jobWaitingEvent = jobWaitingEvent;
        }

        public Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            Console.WriteLine($"Processor Shutting Down. Partition '{context.PartitionId}', Reason: '{reason}'.");
            return Task.CompletedTask;
        }

        public Task OpenAsync(PartitionContext context)
        {
            Console.WriteLine($"SimpleEventProcessor initialized. Partition: '{context.PartitionId}'");
            return Task.CompletedTask;
        }

        public Task ProcessErrorAsync(PartitionContext context, Exception error)
        {
            Console.WriteLine($"Error on Partition: {context.PartitionId}, Error: {error.Message}");
            return Task.CompletedTask;
        }

        public Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
        {
            foreach (var eventData in messages)
            {
                PrintJobEvent(eventData);
            }

            return context.CheckpointAsync();
        }

        /// <summary>
        /// Parse and print Media Services events.
        /// </summary>
        /// <param name="eventData">Event Hub event data.</param>
        private void PrintJobEvent(EventData eventData)
        {
            var data = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
            JArray jArr = JsonConvert.DeserializeObject<JArray>(data);
            foreach (JObject jObj in jArr)
            {
                string eventType = (string)jObj.GetValue("eventType");

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
                            MediaJobStateChangeEventData jobEventData = jObj.GetValue("data").ToObject<MediaJobStateChangeEventData>();
                            string subject = (string)jObj.GetValue("subject");
                            string eventJobName = Regex.Replace(subject, @"^transforms.*/", "");
                            if (eventJobName != jobName)
                            {
                                break;
                            }

                            Console.WriteLine($"Job state changed for JobId: {eventJobName} PreviousState: {jobEventData.PreviousState} State: {jobEventData.State}");
                            
                            // For final states, send a message to notify that the job has finished.
                            if (eventType == "Microsoft.Media.JobFinished" || eventType == "Microsoft.Media.JobCanceled" || eventType == "Microsoft.Media.JobErrored")
                            {
                                // Job finished, send a message.
                                jobWaitingEvent.Set();
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
                            MediaJobOutputStateChangeEventData jobEventData = jObj.GetValue("data").ToObject<MediaJobOutputStateChangeEventData>();
                            string subject = (string)jObj.GetValue("subject");
                            string eventJobName = Regex.Replace(subject, @"^transforms.*/", "");
                            if (eventJobName != jobName)
                            {
                                break;
                            }

                            Console.WriteLine($"Job output state changed for JobId: {eventJobName} PreviousState: {jobEventData.PreviousState} " +
                                $"State: {jobEventData.Output.State} Progress: {jobEventData.Output.Progress}");
                        }
                        break;

                    // Job output progress event
                    case "Microsoft.Media.JobOutputProgress":
                        {
                            MediaJobOutputProgressEventData jobEventData = jObj.GetValue("data").ToObject<MediaJobOutputProgressEventData>();
                            string subject = (string)jObj.GetValue("subject");
                            string eventJobName = Regex.Replace(subject, @"^transforms.*/", "");
                            if (eventJobName != jobName)
                            {
                                break;
                            }

                            Console.WriteLine($"Job output progress changed for JobId: {eventJobName} Progress: {jobEventData.Progress}");
                        }
                        break;

                    // LiveEvent Stream-level events
                    case "Microsoft.Media.LiveEventConnectionRejected":
                        {
                            MediaLiveEventConnectionRejectedEventData liveEventData = jObj.GetValue("data").ToObject<MediaLiveEventConnectionRejectedEventData>();
                            Console.WriteLine($"LiveEvent connection rejected. IngestUrl: {liveEventData.IngestUrl} StreamId: {liveEventData.StreamId} " +
                                $"EncoderIp: {liveEventData.EncoderIp} EncoderPort: {liveEventData.EncoderPort}");
                        }
                        break;
                    case "Microsoft.Media.LiveEventEncoderConnected":
                        {
                            MediaLiveEventEncoderConnectedEventData liveEventData = jObj.GetValue("data").ToObject<MediaLiveEventEncoderConnectedEventData>();
                            Console.WriteLine($"LiveEvent encoder connected. IngestUrl: {liveEventData.IngestUrl} StreamId: {liveEventData.StreamId} " +
                                $"EncoderIp: {liveEventData.EncoderIp} EncoderPort: {liveEventData.EncoderPort}");
                        }
                        break;
                    case "Microsoft.Media.LiveEventEncoderDisconnected":
                        {
                            MediaLiveEventEncoderDisconnectedEventData liveEventData = jObj.GetValue("data").ToObject<MediaLiveEventEncoderDisconnectedEventData>();
                            Console.WriteLine($"LiveEvent encoder disconnected. IngestUrl: {liveEventData.IngestUrl} StreamId: {liveEventData.StreamId} " +
                                $"EncoderIp: {liveEventData.EncoderIp} EncoderPort: {liveEventData.EncoderPort}");
                        }
                        break;

                    // LiveEvent Track-level events
                    case "Microsoft.Media.LiveEventIncomingDataChunkDropped":
                        {
                            MediaLiveEventIncomingDataChunkDroppedEventData liveEventData = jObj.GetValue("data").ToObject<MediaLiveEventIncomingDataChunkDroppedEventData>();
                            string subject = (string)jObj.GetValue("subject");
                            string liveEventId = Regex.Replace(subject, @"^transforms.*/", "");
                            Console.WriteLine($"LiveEvent data chunk dropped. LiveEventId: {liveEventId} ResultCode: {liveEventData.ResultCode}");
                        }
                        break;
                    case "Microsoft.Media.LiveEventIncomingStreamReceived":
                        {
                            MediaLiveEventIncomingStreamReceivedEventData liveEventData = jObj.GetValue("data").ToObject<MediaLiveEventIncomingStreamReceivedEventData>();
                            Console.WriteLine($"LiveEvent incoming stream received. IngestUrl: {liveEventData.IngestUrl} EncoderIp: {liveEventData.EncoderIp} " +
                                $"EncoderPort: {liveEventData.EncoderPort}");
                        }
                        break;
                    case "Microsoft.Media.LiveEventIncomingStreamsOutOfSync":
                        {
                            //MediaLiveEventIncomingStreamsOutOfSyncEventData eventData = jObj.GetValue("data").ToObject<MediaLiveEventIncomingStreamsOutOfSyncEventData>();
                            string subject = (string)jObj.GetValue("subject");
                            string liveEventId = Regex.Replace(subject, @"^transforms.*/", "");
                            Console.WriteLine($"LiveEvent incoming audio and video streams are out of sync. LiveEventId: {liveEventId}");
                        }
                        break;
                    case "Microsoft.Media.LiveEventIncomingVideoStreamsOutOfSync":
                        {
                            //MediaLiveEventIncomingVideoStreamsOutOfSyncEventData eventData =jObj.GetValue("data").ToObject<MediaLiveEventIncomingVideoStreamsOutOfSyncEventData>();
                            string subject = (string)jObj.GetValue("subject");
                            string liveEventId = Regex.Replace(subject, @"^transforms.*/", "");
                            Console.WriteLine($"LeveEvent incoming video streams are out of sync. LiveEventId: {liveEventId}");
                        }
                        break;
                    case "Microsoft.Media.LiveEventIngestHeartbeat":
                        {
                            MediaLiveEventIngestHeartbeatEventData liveEventData = jObj.GetValue("data").ToObject<MediaLiveEventIngestHeartbeatEventData>();
                            Console.WriteLine($"LiveEvent ingest heart beat. TrackType: {liveEventData.TrackType} State: {liveEventData.State} Healthy: {liveEventData.Healthy}");
                        }
                        break;
                    case "Microsoft.Media.LiveEventTrackDiscontinuityDetected":
                        {
                            MediaLiveEventTrackDiscontinuityDetectedEventData liveEventData = jObj.GetValue("data").ToObject<MediaLiveEventTrackDiscontinuityDetectedEventData>();
                            string subject = (string)jObj.GetValue("subject");
                            string liveEventId = Regex.Replace(subject, @"^transforms.*/", "");
                            Console.WriteLine($"LiveEvent discontinuity in the incoming track detected. LiveEventId: {liveEventId} TrackType: {liveEventData.TrackType} " +
                                $"Discontinuity gap: {liveEventData.DiscontinuityGap}");
                        }
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Factory class for creating custom EventProcessor.
    /// </summary>
    class MediaServicesEventProcessorFactory : IEventProcessorFactory
    {
        private readonly AutoResetEvent jobWaitingEven;
        private readonly string jobName;
        public MediaServicesEventProcessorFactory(string jobName, AutoResetEvent jobWaitingEvent)
        {
            this.jobName = jobName;
            this.jobWaitingEven = jobWaitingEvent;
        }

        IEventProcessor IEventProcessorFactory.CreateEventProcessor(PartitionContext context)
        {
            return new MediaServicesEventProcessor(jobName, jobWaitingEven);
        }
    }
}
