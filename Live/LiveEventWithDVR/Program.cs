// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.ResourceManager;
using Azure.ResourceManager.Media;
using Azure.ResourceManager.Media.Models;
using Azure.Storage.Blobs;
using Common_Utils;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

//////////////////////////////////////////////////////////////////////////////////////
////  Azure Media Services Live streaming sample 

////  This sample assumes that you will use OBS Studio to broadcast RTMP
////  to the ingest endpoint. Please install OBS Studio first. 
////  Use the following settings in OBS:
////      Encoder: NVIDIA NVENC (if avail) or x264
////      Rate Control: CBR
////      Bitrate: 2500 Kbps (or something reasonable for your laptop)
////      Keyframe Interval : 2s, or 1s for low latency  
////      Preset : Low-latency Quality or Performance (NVENC) or "veryfast" using x264
////      Profile: high
////      GPU: 0 (Auto)
////      Max B-frames: 2
////      
////  The workflow for the sample and for the recommended use of the Live API:
////  1) Create the client for AMS using AAD service principal or managed ID
////  2) Set up your IP restriction allow objects for ingest and preview
////  3) Configure the Live Event object with your settings. Choose pass-through
////     or encoding channel type and size (720p or 1080p)
////  4) Create the Live Event without starting it
////  5) Create an Asset to be used for recording the live stream into
////  6) Create a Live Output, which acts as the "recorder" to record into the
////     Asset (which is like the tape in the recorder).
////  7) Start the Live Event - this can take a little bit.
////  8) Get the preview endpoint to monitor in a player for DASH or HLS.
////  9) Get the ingest RTMP endpoint URL for use in OBS Studio.
////     Set up OBS studio and start the broadcast.  Monitor the stream in 
////     your DASH or HLS player of choice. 
//// 10) Create a new Streaming Locator on the recording Asset object from step 5.
//// 11) Get the URLs for the HLS and DASH manifest to share with your audience
////     or CMS system. This can also be created earlier after step 5 if desired.
//////////////////////////////////////////////////////////////////////////////////////

// Please make sure you have set your settings in the appsettings.json file
IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
IConfigurationRoot configuration = builder.Build();

var MediaServicesResource = MediaServicesAccountResource.CreateResourceIdentifier(
    subscriptionId: configuration["AZURE_SUBSCRIPTION_ID"],
    resourceGroupName: configuration["AZURE_RESOURCE_GROUP"],
    accountName: configuration["AZURE_MEDIA_SERVICES_ACCOUNT_NAME"]);

var credential = new DefaultAzureCredential(includeInteractiveCredentials: true);
var armClient = new ArmClient(credential);
var mediaServicesAccount = armClient.GetMediaServicesAccountResource(MediaServicesResource);

string uniqueness = Guid.NewGuid().ToString().Substring(0, 13); // Create a GUID for uniqueness. You can make this something static if you dont want to change RTMP ingest settings in OBS constantly.  
string liveEventName = "liveevent-" + uniqueness; // WARNING: Be careful not to leak live events using this sample!
string assetName = "archiveAsset" + uniqueness;
string liveOutputName = "liveOutput" + uniqueness;
string drvStreamingLocatorName = "streamingLocator" + uniqueness;
string archiveStreamingLocatorName = "fullLocator-" + uniqueness;
string drvAssetFilterName = "filter-" + uniqueness;
string streamingLocatorName = "streamingLocator" + uniqueness;
string streamingEndpointName = "default"; // Change this to your specific streaming endpoint name if not using "default"
bool stopEndpoint = false;

// In this sample, we use Event Grid to listen to the notifications through an Azure Event Hub. 
// If you do not provide an Event Hub config, the sample will fall back to polling the job for status. 
// For production ready code, it is always recommended to use Event Grid instead of polling on the Job status. 

EventProcessorClient processorClient = null;
BlobContainerClient storageClient = null;
MediaServicesEventProcessor mediaEventProcessor = null;

try
{
    #region CreateLiveEvent
    Console.WriteLine($"Creating a live event named {liveEventName}");
    Console.WriteLine();

    // Creating the LiveEvent - the primary object for live streaming in AMS. 
    // See the overview - https://docs.microsoft.com/azure/media-services/latest/live-streaming-overview

    // Create the LiveEvent

    // Understand the concepts of what a live event and a live output is in AMS first!
    // Read the following - https://docs.microsoft.com/azure/media-services/latest/live-events-outputs-concept
    // 1) Understand the billing implications for the various states
    // 2) Understand the different live event types, pass-through and encoding
    // 3) Understand how to use long-running async operations 
    // 4) Understand the available Standby mode and how it differs from the Running Mode. 
    // 5) Understand the differences between a LiveOutput and the Asset that it records to.  They are two different concepts.
    //    A live output can be considered as the "tape recorder" and the Asset is the tape that is inserted into it for recording.
    // 6) Understand the advanced options such as low latency, and live transcription/captioning support. 
    //    Live Transcription - https://docs.microsoft.com/en-us/azure/media-services/latest/live-transcription
    //    Low Latency - https://docs.microsoft.com/en-us/azure/media-services/latest/live-event-latency

    // When broadcasting to a live event, please use one of the verified on-premises live streaming encoders.
    // While operating this tutorial, it is recommended to start out using OBS Studio before moving to another encoder. 

    // Note: When creating a LiveEvent, you can specify allowed IP addresses in one of the following formats:                 
    //      IpV4 address with 4 numbers
    //      CIDR address range  

    IPRange allAllowIpRange = new IPRange
    {
        Name = "AllowAll",
        Address = IPAddress.Parse("0.0.0.0"),
        SubnetPrefixLength = 0
    };

    // Create the LiveEvent Preview IP access control object. 
    // This will restrict which clients can view the preview endpoint
    LiveEventPreview liveEventPreview = new LiveEventPreview();
    liveEventPreview.IPAllowedIPs.Add(allAllowIpRange);

    #region NewLiveEvent
    MediaLiveEventData liveEventData = new MediaLiveEventData(mediaServicesAccount.Get().Value.Data.Location)
    {
        Description = "Sample LiveEvent from .NET SDK sample",
        UseStaticHostname = true,
        // 1) Set up the input settings for the Live event...
        Input = new LiveEventInput(streamingProtocol: LiveEventInputProtocol.Rtmp)
        {
            StreamingProtocol = LiveEventInputProtocol.Rtmp,
            AccessToken = "acf7b6ef-8a37-425f-b8fc-51c2d6a5a86a",
            KeyFrameIntervalDuration = TimeSpan.FromSeconds(2)
        },
        // 2) Set the live event to use pass-through or cloud encoding modes...
        Encoding = new LiveEventEncoding()
        {
            EncodingType = LiveEventEncodingType.PassthroughStandard
        },
        // 3) Set up the Preview endpoint for monitoring based on the settings above we already set.
        Preview = liveEventPreview,
    };

    // 4) Set up more advanced options on the live event. Low Latency is the most common one.
    // Set this to Default or Low Latency
    // When using Low Latency mode, you must configure the Azure Media Player to use the 
    // quick start heuristic profile or you won't notice the change. 
    // In the AMP player client side JS options, set -  heuristicProfile: "Low Latency Heuristic Profile". 
    // To use low latency optimally, you should tune your encoder settings down to 1 second GOP size instead of 2 seconds.
    liveEventData.StreamOptions.Add(StreamOptionsFlag.LowLatency);
    // re-use the same range here for the sample, but in production you can lock this
    // down to the ip range for your on-premises live encoder, laptop, or device that is sending
    // the live stream
    liveEventData.Input.IPAllowedIPs.Add(allAllowIpRange);

    // 5) Optionally enable live transcriptions if desired. This is only supported on PassthroughStandard, and the transcoding live event types. It is not supported on Basic pass-through type.
    // WARNING : This is extra cost ($$$), so please check pricing before enabling.
    /*liveEventData.Transcriptions.Add(new LiveEventTranscription
    {
        // The value should be in BCP-47 format (e.g: 'en-US'). See https://go.microsoft.com/fwlink/?linkid=2133742
        Language = "en-us",
        TrackName = "English" // set the name you want to appear in the output manifest
    });*/
    #endregion NewLiveEvent


    // Start monitoring LiveEvent events using Event Grid and Event Hub
    try
    {
        // Please refer README for Event Hub and storage settings.
        // A storage account is required to process the Event Hub events from the Event Grid subscription in this sample.

        // Create a new host to process events from an Event Hub.
        Console.WriteLine("Creating a new client to process events from an Event Hub...");

        var storageConnectionString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}",
                           configuration["AZURE_STORAGE_ACCOUNT_NAME"], configuration["AZURE_STORAGE_ACCOUNT_KEY"]);


        storageClient = new BlobContainerClient(storageConnectionString, configuration["AZURE_BLOB_CONTAINER_NAME"]);
        processorClient = new EventProcessorClient(storageClient, configuration["AZURE_CONSUMER_GROUP"], configuration["AZURE_EVENT_HUBS_CONNECTION_STRING"], configuration["AZURE_EVENT_HUB_NAME"]);
        mediaEventProcessor = new MediaServicesEventProcessor(null, null, liveEventName);

        processorClient.ProcessEventAsync += mediaEventProcessor.ProcessEventsAsync;
        processorClient.ProcessErrorAsync += mediaEventProcessor.ProcessErrorAsync;

        await processorClient.StartProcessingAsync();
    }
    catch (Exception e)
    {
        Console.WriteLine("Failed to connect to Event Hub, please refer README for Event Hub and storage settings. Skipping event monitoring...");
        Console.WriteLine(e.Message);
    }

    Console.WriteLine("Creating the LiveEvent, please be patient as this can take time to complete async.");
    Console.WriteLine("Live Event creation is an async operation in Azure and timing can depend on resources available.");

    // When autostart is set to true, the Live Event will be started after creation. 
    // That means, the billing starts as soon as the Live Event starts running. 
    // You must explicitly call Stop on the Live Event resource to halt further billing.
    // The following operation can sometimes take awhile. Be patient.
    // On optional workflow is to first call allocate() instead of create. 
    // https://docs.microsoft.com/en-us/rest/api/media/liveevents/allocate 
    // This allows you to allocate the resources and place the live event into a "Standby" mode until 
    // you are ready to transition to "Running". This is useful when you want to pool resources in a warm "Standby" state at a reduced cost.
    // The transition from Standby to "Running" is much faster than cold creation to "Running" using the autostart property.
    // Returns a long running operation polling object that can be used to poll until completion.

    Stopwatch watch = Stopwatch.StartNew();
    MediaLiveEventResource liveEvent = (await mediaServicesAccount
        .GetMediaLiveEvents()
        .CreateOrUpdateAsync(
            WaitUntil.Completed,
            liveEventName,
            liveEventData,
            // When autostart is set to true, you should "await" this method operation to complete. 
            // The Live Event will be started after creation. 
            // You may choose not to do this, but create the object, and then start it using the standby state to 
            // keep the resources "warm" and billing at a lower cost until you are ready to go live. 
            // That increases the speed of startup when you are ready to go live. 
            autoStart: false)).Value;

    watch.Stop();
    string elapsedTime = String.Format(":{0:00}.{1:00}", watch.Elapsed.Seconds, watch.Elapsed.Milliseconds / 10);
    Console.WriteLine($"Create Live Event run time : {elapsedTime}");
    #endregion



    #region CreateAsset
    // Create an Asset for the LiveOutput to use. Think of this as the "tape" that will be recorded to. 
    // The asset entity points to a folder/container in your Azure Storage account
    Console.WriteLine($"Creating an asset named {assetName}");
    Console.WriteLine();
    MediaAssetResource asset = (await mediaServicesAccount.GetMediaAssets()
        .CreateOrUpdateAsync(
            WaitUntil.Completed,
            assetName: assetName,
            new MediaAssetData
            {
                Description = "My video descriptor",
                AlternateId = "12345",
                Container = "my-custom-name-" + Guid.NewGuid()
            })).Value;
    #endregion


    #region CreateLiveOutput
    // Create the Live Output - think of this as the "tape recorder for the live event". 
    // Live outputs are optional, but are required if you want to archive the event to storage,
    // use the asset for on-demand playback later, or if you want to enable cloud DVR time-shifting.
    // We will use the asset created above for the "tape" to record to. 
    string manifestName = "output";
    Console.WriteLine($"Creating a live output named {liveOutputName}");
    Console.WriteLine();

    watch = Stopwatch.StartNew();
    // See the REST API for details on each of the settings on Live Output
    // https://docs.microsoft.com/rest/api/media/liveoutputs/create
    var liveOutput = new MediaLiveOutputData
    {
        AssetName = asset.Data.Name,
        ManifestName = manifestName, // The HLS and DASH manifest file name. This is recommended to set if you want a deterministic manifest path up front.
                                     // archive window can be set from 3 minutes to 25 hours. Content that falls outside of ArchiveWindowLength
                                     // is continuously discarded from storage and is non-recoverable. For a full event archive, set to the maximum, 25 hours.
        ArchiveWindowLength = TimeSpan.FromHours(1)
    };
    await liveEvent.GetMediaLiveOutputs()
        .CreateOrUpdateAsync(WaitUntil.Completed, liveOutputName, liveOutput);
    elapsedTime = String.Format(":{0:00}.{1:00}", watch.Elapsed.Seconds, watch.Elapsed.Milliseconds / 10);
    Console.WriteLine($"Create Live Output run time : {elapsedTime}");
    Console.WriteLine();
    #endregion

    Console.WriteLine("Starting the Live Event now... please stand by as this can take time...");
    watch = Stopwatch.StartNew();
    // Start the Live Event - this will take some time...
    await mediaServicesAccount.GetMediaLiveEvent(liveEventName: liveEventName)
        .Value.StartAsync(WaitUntil.Completed);
    elapsedTime = String.Format(":{0:00}.{1:00}", watch.Elapsed.Seconds, watch.Elapsed.Milliseconds / 10);
    Console.WriteLine($"Start Live Event run time : {elapsedTime}");
    Console.WriteLine();

    // Refresh the liveEvent object's settings after starting it...
    liveEvent = await mediaServicesAccount.GetMediaLiveEventAsync(liveEventName);


    #region GetIngestUrl
    // Get the RTMP ingest URL to configure in OBS Studio. 
    // The endpoints is a collection of RTMP primary and secondary, and RTMPS primary and secondary URLs. 
    // to get the primary secure RTMPS, it is usually going to be index 3, but you could add a loop here to confirm...
    string ingestUrl = liveEvent.Data.Input.Endpoints.First().Uri.ToString();
    Console.WriteLine($"The RTMP ingest URL to enter into OBS Studio is:");
    Console.WriteLine($"\t{ingestUrl}");
    Console.WriteLine("Make sure to enter a Stream Key into the OBS studio settings. It can be any value or you can repeat the accessToken used in the ingest URL path.");
    Console.WriteLine();
    #endregion

    #region GetPreviewURLs
    // Use the previewEndpoint to preview and verify
    // that the input from the encoder is actually being received
    // The preview endpoint URL also support the addition of various format strings for HLS (format=m3u8-cmaf) and DASH (format=mpd-time-cmaf) for example.
    // The default manifest is Smooth. 
    string previewEndpoint = liveEvent.Data.Preview.Endpoints.First().Uri.ToString();
    Console.WriteLine($"The preview url is:");
    Console.WriteLine($"\t{previewEndpoint}");
    Console.WriteLine();

    Console.WriteLine($"Open the live preview in your browser and use the Azure Media Player to monitor the preview playback:");
    Console.WriteLine($"\thttps://ampdemo.azureedge.net/?url={previewEndpoint}&heuristicprofile=lowlatency");
    Console.WriteLine();
    #endregion

    Console.WriteLine("Start the live stream now, sending the input to the ingest url and verify that it is arriving with the preview url.");
    Console.WriteLine("IMPORTANT TIP!: Make ABSOLUTLEY CERTAIN that the video is flowing to the Preview URL before continuing!");
    Console.WriteLine("Press enter to continue...");

    Console.Out.Flush();
    var ignoredInput = Console.ReadLine();


    MediaAssetFilterData drvAssetFilter = new MediaAssetFilterData
    {
        PresentationTimeRange = new PresentationTimeRange
        {
            ForceEndTimestamp = false,
            // 10 minute (600) seconds sliding window
            PresentationWindowDuration = 6000000000L,
            // This value defines the latest live position that a client can seek back to 2 seconds, must be smaller than sliding window.
            LiveBackoffDuration = 100000000L
        }
    };
    await mediaServicesAccount.GetMediaAsset(assetName)
        .Value
        .GetMediaAssetFilters()
        .CreateOrUpdateAsync(WaitUntil.Completed, drvAssetFilterName, drvAssetFilter);



    // Create the Streaming Locator URL for playback of the contents in the Live Output recording
    #region CreateStreamingLocator
    Console.WriteLine($"Creating a streaming locator named {streamingLocatorName}");
    Console.WriteLine();

    var streamingLocatorData = new StreamingLocatorData
    {
        AssetName = asset.Data.Name,
        StreamingPolicyName = "Predefined_ClearStreamingOnly",
    };
    streamingLocatorData.Filters.Add(drvAssetFilterName);  // Associate the dvr filter with StreamingLocator.

    StreamingLocatorResource locator = (await mediaServicesAccount
        .GetStreamingLocators()
        .CreateOrUpdateAsync(
            WaitUntil.Completed,
            streamingLocatorName: drvStreamingLocatorName,
            new StreamingLocatorData
            {
                AssetName = assetName,
                StreamingPolicyName = "Predefined_ClearStreamingOnly",
            })).Value;

    // Get the default Streaming Endpoint on the account
    StreamingEndpointResource streamingEndpoint = (await mediaServicesAccount
        .GetStreamingEndpoints()
        .GetAsync(streamingEndpointName)).Value;

    // If it's not running, Start it. 
    if (streamingEndpoint.Data.ResourceState != StreamingEndpointResourceState.Running)
    {
        Console.WriteLine("Streaming Endpoint was Stopped, restarting now..");
        await streamingEndpoint.StartAsync(WaitUntil.Completed);
        stopEndpoint = true;
    }
    #endregion

    Console.WriteLine("The urls to stream the output from a client:");
    Console.WriteLine();

    // The next method "bulidManifestPaths" is a helper to list the streaming manifests for HLS and DASH. 
    // The paths are only available after the live streaming source has connected. 
    // If you wish to get the streaming manifest ahead of time, make sure to set the manifest name in the LiveOutput as done above.
    // This allows you to have a deterministic manifest path. <streaming endpoint hostname>/<streaming locator ID>/manifestName.ism/manifest(<format string>)

    var hostname = streamingEndpoint.Data.HostName;
    var scheme = "https";
    List<string> manifests = BuildManifestPaths(scheme, hostname, locator.Data.StreamingLocatorId.ToString(), manifestName);

    Console.WriteLine($"The HLS (MP4) manifest for the Live stream  : {manifests[0]}");
    Console.WriteLine("Open the following URL to playback the live stream in an HLS compliant player (HLS.js, Shaka, ExoPlayer) or directly in an iOS device");
    Console.WriteLine($"{manifests[0]}");
    Console.WriteLine();
    Console.WriteLine($"The DASH manifest for the Live stream is : {manifests[1]}");
    Console.WriteLine("Open the following URL to playback the live stream from the LiveOutput in the Azure Media Player");
    Console.WriteLine($"https://ampdemo.azureedge.net/?url={manifests[1]}&heuristicprofile=lowlatency");
    Console.WriteLine();
    Console.WriteLine("Continue experimenting with the stream until you are ready to finish.");
    Console.WriteLine("Press enter to stop the LiveOutput...");
    Console.Out.Flush();
    ignoredInput = Console.ReadLine();

    // If we started the endpoint, we'll stop it. Otherwise, we'll keep the endpoint running and print urls
    // that can be played even after this sample ends.
    if (!stopEndpoint)
    {
        StreamingLocatorResource archiveLocator = (await mediaServicesAccount
            .GetStreamingLocators()
            .CreateOrUpdateAsync(
                WaitUntil.Completed,
                archiveStreamingLocatorName,
                new StreamingLocatorData
                {
                    AssetName = assetName,
                    StreamingPolicyName = "Predefined_ClearStreamingOnly"
                })).Value;
        Console.WriteLine("To playback the archived on-demand asset, Use the following urls:");
        manifests = BuildManifestPaths(scheme, hostname, archiveLocator.Data.StreamingLocatorId.ToString(), manifestName);
        Console.WriteLine($"The HLS (MP4) manifest for the archived asset without a DVR filter is : {manifests[0]}");
        Console.WriteLine("Open the following URL to playback the live stream in an HLS compliant player (HLS.js, Shaka, ExoPlayer) or directly in an iOS device");
        Console.WriteLine($"{manifests[0]}");
        Console.WriteLine();
        Console.WriteLine($"The DASH manifest URL for the archived asset without a DVR filter  : {manifests[1]}");
        Console.WriteLine("Open the following URL to playback the live stream from the LiveOutput in the Azure Media Player");
        Console.WriteLine($"https://ampdemo.azureedge.net/?url={manifests[1]}&heuristicprofile=lowlatency");
        Console.WriteLine();
        Console.WriteLine("Continue experimenting with the stream until you are ready to finish.");
        Console.WriteLine("Press enter to stop the LiveOutput...");
        Console.Out.Flush();
        ignoredInput = Console.ReadLine();

        Console.WriteLine("Experiment with playback of the live archive showing the full asset duration with the filter removed from the Streaming Locator");
        Console.WriteLine("Press enter to stop and cleanup the sample...");
        Console.Out.Flush();
        ignoredInput = Console.ReadLine();
    }
}
catch (RequestFailedException e)
{
    Console.WriteLine("Hit ErrorResponseException");
    Console.WriteLine($"{e.Message}");
    Console.WriteLine();
    Console.WriteLine("Exiting, cleanup may be necessary...");
    Console.ReadLine();
}
finally
{
    Console.WriteLine("Cleaning up resources, stopping Live Event billing, and deleting live Event...");
    Console.WriteLine("CRITICAL WARNING ($$$$) DON'T WASTE MONEY!: - Wait here for the All Clear - this takes a few minutes sometimes to clean up. DO NOT STOP DEBUGGER yet or you will leak billable resources!");

    await CleanupLiveEventAndOutputAsync(mediaServicesAccount, liveEventName, liveOutputName);
    await CleanupLocatorandAssetAsync(mediaServicesAccount, streamingLocatorName, assetName);

    // Stop event monitoring.
    if (processorClient != null)
    {
        Console.WriteLine("Job final state received, Stopping the event processor...");
        await processorClient.StopProcessingAsync();
        Console.WriteLine();

        // It is encouraged that you unregister your handlers when you have
        // finished using the Event Processor to ensure proper cleanup.  This
        // is especially important when using lambda expressions or handlers
        // in any form that may contain closure scopes or hold other references.
        processorClient.ProcessEventAsync -= mediaEventProcessor.ProcessEventsAsync;
        processorClient.ProcessErrorAsync -= mediaEventProcessor.ProcessErrorAsync;
    }

    if (stopEndpoint)
    {
        // Because we started the endpoint, we'll stop it.
        StreamingEndpointResource streamingEndpoint = (await mediaServicesAccount
            .GetStreamingEndpoints()
            .GetAsync(streamingEndpointName)).Value;
        await streamingEndpoint.StopAsync(WaitUntil.Completed);
    }
    else
    {
        // We will keep the endpoint running because it was not started by us. There are costs to keep it running.
        // Please refer https://azure.microsoft.com/en-us/pricing/details/media-services/ for pricing. 
        Console.WriteLine($"The endpoint {streamingEndpointName} is running. To halt further billing on the endpoint, please stop it in Azure portal or AMS Explorer.");
    }

    Console.WriteLine("The LiveOutput and LiveEvent are now deleted.  The event is available as an archive and can still be streamed.");
    Console.WriteLine("All Clear, and all cleaned up. Please double check in the portal to make sure you have not leaked any Live Events, or left any Running still which would result in unwanted billing.");
}




List<string> BuildManifestPaths(string scheme, string hostname, string streamingLocatorId, string manifestName)
{
    const string hlsFormat = "format=m3u8-cmaf";
    const string dashFormat = "format=mpd-time-cmaf";

    List<string> manifests = new();

    var manifestBase = $"{scheme}://{hostname}/{streamingLocatorId}/{manifestName}.ism/manifest";
    var hlsManifest = $"{manifestBase}({hlsFormat})";
    manifests.Add(hlsManifest);

    var dashManifest = $"{manifestBase}({dashFormat})";
    manifests.Add(dashManifest);

    return manifests;
}


// <CleanupLiveEventAndOutput>
async Task CleanupLiveEventAndOutputAsync(MediaServicesAccountResource mediaServicesAccountient, string liveEventName, string liveOutputName)
{
    try
    {
        MediaLiveEventResource liveEvent = await mediaServicesAccountient.GetMediaLiveEventAsync(liveEventName);

        Console.WriteLine("Deleting Live Output");
        Stopwatch watch = Stopwatch.StartNew();

        await (await liveEvent.GetMediaLiveOutputAsync(liveOutputName))
            .Value.DeleteAsync(WaitUntil.Completed);

        String elapsedTime = String.Format(":{0:00}.{1:00}", watch.Elapsed.Seconds, watch.Elapsed.Milliseconds / 10);
        Console.WriteLine($"Delete Live Output run time : {elapsedTime}");

        if (liveEvent != null)
        {
            if (liveEvent.Data.ResourceState == LiveEventResourceState.Running)
            {
                watch = Stopwatch.StartNew();
                // If the LiveEvent is running, stop it and have it remove any LiveOutputs
                await liveEvent.StopAsync(WaitUntil.Completed, new LiveEventActionContent() { RemoveOutputsOnStop = true });
                elapsedTime = String.Format(":{0:00}.{1:00}", watch.Elapsed.Seconds, watch.Elapsed.Milliseconds / 10);
                Console.WriteLine($"Stop Live Event run time : {elapsedTime}");
            }

            // Delete the LiveEvent
            await liveEvent.DeleteAsync(WaitUntil.Completed);
        }
    }
    catch (RequestFailedException e)
    {

        Console.WriteLine("CleanupLiveEventAndOutputAsync -- Hit ErrorResponseException");
        Console.WriteLine($"{e.Message}");
        Console.WriteLine();

    }

}
// </CleanupLiveEventAndOutput>

// <CleanupLocatorAssetAndStreamingEndpoint>
static async Task CleanupLocatorandAssetAsync(MediaServicesAccountResource mediaServicesAccount, string streamingLocatorName, string assetName)
{
    try
    {
        // Delete the Streaming Locator
        await (await mediaServicesAccount.GetStreamingLocatorAsync(streamingLocatorName))
            .Value.DeleteAsync(WaitUntil.Completed);
        // Delete the Archive Asset
        await (await mediaServicesAccount.GetMediaAssetAsync(assetName))
            .Value.DeleteAsync(WaitUntil.Completed);
    }
    catch (RequestFailedException e)
    {
        Console.WriteLine("CleanupLocatorandAssetAsync -- Hit ErrorResponseException");
        Console.WriteLine($"{e.Message}");
        Console.WriteLine();
    }

}