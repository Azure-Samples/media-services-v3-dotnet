// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using Azure.ResourceManager;
using Azure.ResourceManager.EventGrid;
using Azure.ResourceManager.Storage;
using Azure.Storage.Blobs;

public class MediaServicesMonitor
{
    public static async Task<MediaServicesMonitor?> StartMonitoringAsync(
        ArmClient armClient,
        ResourceIdentifier mediaServicesResourceId,
        string liveEventName,
        Options options,
        TokenCredential credential)
    {
        Console.Write($"Getting Event Subscriptions...".PadRight(60));
        var eventSubscriptions = await armClient.GetEventSubscriptions(mediaServicesResourceId).ToListAsync();
        Console.WriteLine("Done");

        if (!eventSubscriptions.Any())
        {
            Console.Write("No Event Subscriptions found, skipping monitoring");
            return null;
        }

        var storageAccountResourceId = StorageAccountResource.CreateResourceIdentifier(
            subscriptionId: options.AZURE_SUBSCRIPTION_ID.ToString(),
            resourceGroupName: options.AZURE_RESOURCE_GROUP,
            accountName: options.AZURE_STORAGE_ACCOUNT_NAME);

        Console.Write($"Getting storage account details...".PadRight(60));
        var storageAccount = await armClient.GetStorageAccountResource(storageAccountResourceId).GetAsync();
        Console.WriteLine("Done");

        var blobServiceClient = new BlobServiceClient(
            storageAccount.Value.Data.PrimaryEndpoints.BlobUri,
            credential);

        var containerClient = blobServiceClient.GetBlobContainerClient(options.AZURE_BLOB_CONTAINER_NAME);

        Console.Write($"Creating storage container for Event Hub processor...".PadRight(60));
        await containerClient.CreateIfNotExistsAsync();
        Console.WriteLine("Done");

        var processorClient = new EventProcessorClient(
            containerClient,
            options.EVENT_HUB_CONSUMER_GROUP_NAME,
            options.EVENT_HUB_NAMESPACE,
            options.EVENT_HUB_NAME,
            credential);
        
        var mediaEventProcessor = new MediaServicesMonitor(liveEventName, processorClient);

        Console.Write($"Starting Event Processor Client...".PadRight(60));
        await processorClient.StartProcessingAsync();
        Console.WriteLine("Done");

        return mediaEventProcessor;
    }

    private readonly string _liveEventName;
    private readonly EventProcessorClient _processorClient;

    private MediaServicesMonitor(string liveEventName, EventProcessorClient processorClient)
    {
        _liveEventName = liveEventName;
        _processorClient = processorClient;
        _processorClient.ProcessEventAsync += ProcessEventsAsync;
        _processorClient.ProcessErrorAsync += ProcessErrorAsync;
    }

    public async Task StopAsync()
    {
        await _processorClient.StopProcessingAsync();
        _processorClient.ProcessEventAsync -= ProcessEventsAsync;
        _processorClient.ProcessErrorAsync -= ProcessErrorAsync;
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
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
            // Don't propagate the error to EventProcessorClient
        }

        return Task.CompletedTask;
    }

    private async Task ProcessEventsAsync(ProcessEventArgs args)
    {
        try
        {
            if (args.HasEvent)
            {
                var events = EventGridEvent.ParseMany(args.Data.EventBody);

                foreach (var eventDetails in events)
                {
                    if (eventDetails.Subject != $"liveEvent/{_liveEventName}")
                    {
                        continue;
                    }

                    // Workaround formatting issue with heartbeat events
                    if (eventDetails.EventType == "Microsoft.Media.LiveEventIngestHeartbeat")
                    {
                        eventDetails.Data = new BinaryData(
                            eventDetails.Data.ToString().Replace("\"lastFragmentArrivalTime\":\"\"", "\"lastFragmentArrivalTime\":null"));
                    }

                    if (eventDetails.TryGetSystemEventData(out var eventData))
                    {
                        PrintEvent(eventDetails, eventData);
                    }
                    else
                    {
                        Console.WriteLine($"ERROR: Failed to parse {eventDetails.EventType}");
                    }
                }
            }

            await args.UpdateCheckpointAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing event: {ex.Message}");
        }
    }

    /// <summary>
    /// Parse and print Media Services events.
    /// </summary>
    /// <param name="eventData">Event Hub event data.</param>
    private static void PrintEvent(EventGridEvent eventDetails, object eventData)
    {
        Console.WriteLine($"Event at {eventDetails.EventTime}, type: {eventDetails.EventType}");

        switch (eventData)
        {
            case MediaLiveEventEncoderConnectedEventData d:
                Console.WriteLine(
                    $"\tLive Event encoder connected.\n" +
                    $"\tIngest URL: {d.IngestUrl}\n" +
                    $"\tStream ID: {d.StreamId}\n" +
                    $"\tEncoder IP: {d.EncoderIp}\n" +
                    $"\tEncoder Port: {d.EncoderPort}");
                break;

            case MediaLiveEventEncoderDisconnectedEventData d:
                Console.WriteLine(
                    $"\tLive Event encoder disconnected.\n" +
                    $"\tIngest URL: {d.IngestUrl}\n" +
                    $"\tStream ID: {d.StreamId}\n" +
                    $"\tEncoder IP: {d.EncoderIp}\n" +
                    $"\tEncoder Port: {d.EncoderPort}");
                break;

            case MediaLiveEventConnectionRejectedEventData d:
                Console.WriteLine(
                    $"\tLive Event encoder connection rejected.\n" +
                    $"\tIngest URL: {d.IngestUrl}\n" +
                    $"\tStream ID: {d.StreamId}\n" +
                    $"\tEncoder IP: {d.EncoderIp}\n" +
                    $"\tEncoder Port: {d.EncoderPort}");
                break;

            case MediaLiveEventIncomingStreamReceivedEventData d:
                Console.WriteLine(
                    $"\tLive Event incoming stream received.\n" +
                    $"\tIngest URL: {d.IngestUrl}\n" +
                    $"\tEncoder IP: {d.EncoderIp}\n" +
                    $"\tEncoder Port: {d.EncoderPort}\n" +
                    $"\tTrack Name: {d.TrackName}\n" +
                    $"\tTrack Type: {d.TrackType}\n" +
                    $"\tBitrate: {d.Bitrate}");
                break;

            case MediaLiveEventIngestHeartbeatEventData d:
                Console.WriteLine(
                    $"\tLive Event ingest heartbeat.\n" +
                    $"\tHealthy: {d.Healthy}\n" +
                    $"\tDiscontinuity Count: {d.DiscontinuityCount}\n" +
                    $"\tBitrate: {d.Bitrate}");
                break;

            case MediaLiveEventIncomingDataChunkDroppedEventData d:
                Console.WriteLine(
                    $"\tLive Event ingest fragment dropped.\n" +
                    $"\tTrack Name: {d.TrackName}");
                break;

            case MediaLiveEventIncomingStreamsOutOfSyncEventData:
                Console.WriteLine(
                    $"\tLive Event incoming streams out of sync.\n");
                break;

            case MediaLiveEventIncomingVideoStreamsOutOfSyncEventData:
                Console.WriteLine(
                    $"\tLive Event incoming video stream out of sync.\n");
                break;

            case MediaLiveEventTrackDiscontinuityDetectedEventData:
                Console.WriteLine(
                    $"\tLive Event ingest track discontinuity detected.\n");
                break;

            default:
                Console.WriteLine(eventData);
                break;
        }
    }
}
