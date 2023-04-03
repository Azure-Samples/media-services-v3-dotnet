// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Media;
using Azure.ResourceManager.Media.Models;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Web;

//////////////////////////////////////////////////////////////////////////////////////
//// Azure Media Services Live streaming sample
////
//// This sample assumes that you will use OBS Studio to broadcast RTMP / to the
//// ingest endpoint. Use the following settings in OBS Studio:
////   Encoder:           NVIDIA NVENC (if available) or x264
////   Rate Control:      CBR
////   Bitrate:           2500Kbps (or something reasonable for your laptop)
////   Keyframe Interval: 2s, or 1s for low latency
////   Preset:            Low-latency Quality or Performance (NVENC) or "veryfast" using x264
////   Profile:           high
////   GPU:               0 (Auto)
////   Max B-frames:      2
////
////  The workflow for the sample and for the recommended use of the Live Events:
////  1) Create the client for Media Services.
////  2) Set up the list of allowed IP addresses for ingest and preview.
////  3) Configure the Live Event object with your settings. Choose pass-through
////     or encoding channel type and size (720p or 1080p).
////  4) Create the Live Event without starting it.
////  5) Create an Asset to be used for recording the live stream into.
////  6) Create a Live Output, which acts as the "recorder" to record into the
////     Asset (which is like the tape in the recorder).
////  7) Start the Live Event.
////  8) Get the preview endpoint to monitor in a player for DASH or HLS.
////  9) Get the ingest RTMP endpoint URL for use in OBS Studio.
////     Set up OBS Studio and start the broadcast. Monitor the stream in
////     your DASH or HLS player of choice.
//// 10) Create a new Streaming Locator on the recording Asset object from step 5.
//// 11) Get the URLs for the HLS and DASH manifest to share with your audience
////     or CMS system. This can also be created earlier after step 5 if desired.
//////////////////////////////////////////////////////////////////////////////////////

// Loading the settings from the appsettings.json file or from the command line parameters
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddCommandLine(args)
    .Build();

if (!Options.TryGetOptions(configuration, out var options))
{
    return;
}

Console.WriteLine($"Subscription ID:             {options.AZURE_SUBSCRIPTION_ID}");
Console.WriteLine($"Resource group name:         {options.AZURE_RESOURCE_GROUP}");
Console.WriteLine($"Media Services account name: {options.AZURE_MEDIA_SERVICES_ACCOUNT_NAME}");
Console.WriteLine($"Event Hub namespace:         {options.EVENT_HUB_NAMESPACE}");
Console.WriteLine($"Event Hub name:              {options.EVENT_HUB_NAME}");
Console.WriteLine($"Consumer group:              {options.EVENT_HUB_CONSUMER_GROUP_NAME}");
Console.WriteLine($"Storage account name:        {options.AZURE_STORAGE_ACCOUNT_NAME}");
Console.WriteLine($"Blob container name:         {options.AZURE_BLOB_CONTAINER_NAME}");
Console.WriteLine();

var mediaServicesResourceId = MediaServicesAccountResource.CreateResourceIdentifier(
    subscriptionId: options.AZURE_SUBSCRIPTION_ID.ToString(),
    resourceGroupName: options.AZURE_RESOURCE_GROUP,
    accountName: options.AZURE_MEDIA_SERVICES_ACCOUNT_NAME);

var credential = new DefaultAzureCredential(includeInteractiveCredentials: true);
var armClient = new ArmClient(credential);
var mediaServicesAccount = armClient.GetMediaServicesAccountResource(mediaServicesResourceId);

var uniqueness = Guid.NewGuid().ToString()[..13]; // Create a GUID for uniqueness. You can make this something static if you don't want to change RTMP ingest settings in OBS constantly.
var liveEventName = "liveevent-" + uniqueness;
var assetName = "archiveAsset" + uniqueness;
var liveOutputName = "liveOutput" + uniqueness;
var drvStreamingLocatorName = "streamingLocator" + uniqueness;
var archiveStreamingLocatorName = "fullLocator-" + uniqueness;
var dvrAssetFilterName = "filter-" + uniqueness;
var streamingLocatorName = "streamingLocator" + uniqueness;
var streamingEndpointName = "default"; // Change this to your specific streaming endpoint name if not using "default"
var manifestName = "output";

MediaServicesMonitor? mediaServicesMonitor = null;

// Optionally use Event Hub to monitor the Live Event
if (!string.IsNullOrWhiteSpace(options.EVENT_HUB_NAME) && !options.EVENT_HUB_NAME.StartsWith("---"))
{
    mediaServicesMonitor = await MediaServicesMonitor.StartMonitoringAsync(
        armClient,
        mediaServicesResourceId,
        liveEventName,
        options,
        credential);
}

var liveEvent = await CreateLiveEventAsync(mediaServicesAccount, liveEventName);

var (liveOutput, asset) = await CreateLiveOutputAsync(mediaServicesAccount, liveEvent, assetName, liveOutputName, manifestName);

liveEvent = await StartLiveEventAsync(liveEvent);

PrintIngestUrls(liveEvent);

var streamingLocator = await DemoStreamingAsync(
    mediaServicesAccount,
    asset,
    streamingEndpointName,
    streamingLocatorName,
    manifestName,
    dvrAssetFilterName);

Console.WriteLine("Cleaning up resources, stopping Live Event billing, and deleting Live Event...");
await CleanupResourcesAsync(
    liveEvent,
    liveOutput,
    streamingLocator,
    asset);

if (mediaServicesMonitor != null)
{
    await mediaServicesMonitor.StopAsync();
}

static async Task<MediaLiveEventResource> CreateLiveEventAsync(
    MediaServicesAccountResource mediaServicesAccount,
    string liveEventName)
{
    // Creating the Live Event - the primary object for live streaming in Media Services.
    // See the overview - https://learn.microsoft.com/azure/media-services/latest/live-event-concept
    Console.Write($"Creating the Live Event '{liveEventName}'...".PadRight(60));
    #region CreateLiveEvent
    var liveEvent = await mediaServicesAccount.GetMediaLiveEvents().CreateOrUpdateAsync(
        WaitUntil.Completed,
        liveEventName,
        new MediaLiveEventData(mediaServicesAccount.Get().Value.Data.Location)
        {
            Description = "Sample Live Event from the .NET SDK sample",
            UseStaticHostname = true,
            // 1) Set up the input settings for the Live event...
            Input = new LiveEventInput(streamingProtocol: LiveEventInputProtocol.Rtmp)
            {
                StreamingProtocol = LiveEventInputProtocol.Rtmp,
                AccessToken = "acf7b6ef-8a37-425f-b8fc-51c2d6a5a86a", // used to make the ingest URL unique
                KeyFrameIntervalDuration = TimeSpan.FromSeconds(2),
                IPAllowedIPs =
                {
                    new IPRange
                    {
                        Name = "AllowAllIpV4Addresses",
                        Address = IPAddress.Parse("0.0.0.0"),
                        SubnetPrefixLength = 0
                    },
                    new IPRange
                    {
                        Name = "AllowAllIpV6Addresses",
                        Address = IPAddress.Parse("0::"),
                        SubnetPrefixLength = 0
                    }
                }
            },
            // 2) Set the live event to use pass-through or cloud encoding modes...
            Encoding = new LiveEventEncoding()
            {
                EncodingType = LiveEventEncodingType.PassthroughBasic
            },
            // 3) Set up the Preview endpoint for monitoring
            Preview = new LiveEventPreview
            {
                IPAllowedIPs =
                {
                    new IPRange()
                    {
                        Name = "AllowAllIpV4Addresses",
                        Address = IPAddress.Parse("0.0.0.0"),
                        SubnetPrefixLength = 0
                    },
                    new IPRange()
                    {
                        Name = "AllowAllIpV6Addresses",
                        Address = IPAddress.Parse("0::"),
                        SubnetPrefixLength = 0
                    }
                }
            },
            // 4) Set up more advanced options on the live event. Low Latency is the most common one. Set
            //    this to Default or Low Latency. When using Low Latency mode, you must configure the Azure
            //    Media Player to use the quick start heuristic profile or you won't notice the change. In
            //    the AMP player client side JS options, set -  heuristicProfile: "Low Latency Heuristic
            //    Profile". To use low latency optimally, you should tune your encoder settings down to 1
            //    second GOP size instead of 2 seconds.
            StreamOptions =
            {
                StreamOptionsFlag.LowLatency
            },
            // 5) Optionally enable live transcriptions if desired. This is only supported on
            //    PassthroughStandard, and the transcoding live event types. It is not supported on Basic
            //    pass-through type.
            // WARNING: This is extra cost, so please check pricing before enabling.
            //Transcriptions =
            //{
            //    new LiveEventTranscription
            //    {
            //        // The value should be in BCP-47 format (e.g: 'en-US'). See https://go.microsoft.com/fwlink/?linkid=2133742
            //        Language = "en-us",
            //        TrackName = "English" // set the name you want to appear in the output manifest
            //    }
            //}
        },
        autoStart: false);
    #endregion CreateLiveEvent
    Console.WriteLine("Done");

    return liveEvent.Value;
}

static async Task<(MediaLiveOutputResource, MediaAssetResource)> CreateLiveOutputAsync(
    MediaServicesAccountResource mediaServicesAccount,
    MediaLiveEventResource liveEvent,
    string assetName,
    string liveOutputName,
    string manifestName)
{
    #region CreateAsset
    // Create an Asset for the Live Output to use. Think of this as the "tape" that will be recorded
    // to. The asset entity points to a folder/container in your Azure Storage account
    Console.Write($"Creating the output Asset '{assetName}'...".PadRight(60));
    var asset = (await mediaServicesAccount.GetMediaAssets().CreateOrUpdateAsync(
        WaitUntil.Completed,
        assetName,
        new MediaAssetData
        {
            Description = "My video description"
        })).Value;
    Console.WriteLine("Done");
    #endregion CreateAsset

    #region CreateLiveOutput
    // Create the Live Output - think of this as the "tape recorder for the live event". Live
    // outputs are optional, but are required if you want to archive the event to storage, use the
    // asset for on-demand playback later, or if you want to enable cloud DVR time-shifting. We will
    // use the asset created above for the "tape" to record to.
    Console.Write($"Creating Live Output...".PadRight(60));
    var liveOutput = (await liveEvent.GetMediaLiveOutputs().CreateOrUpdateAsync(
        WaitUntil.Completed,
        liveOutputName,
        new MediaLiveOutputData
        {
            AssetName = asset.Data.Name,
            // The HLS and DASH manifest file name. This is recommended to
            // set if you want a deterministic manifest path up front.
            // archive window can be set from 3 minutes to 25 hours.
            // Content that falls outside of ArchiveWindowLength is
            // continuously discarded from storage and is non-recoverable.
            // For a full event archive, set to the maximum, 25 hours.
            ManifestName = manifestName,
            ArchiveWindowLength = TimeSpan.FromHours(1)
        })).Value;
    Console.WriteLine("Done");
    #endregion CreateLiveOutput

    return (liveOutput, asset);
}

static async Task<MediaLiveEventResource> StartLiveEventAsync(MediaLiveEventResource liveEvent)
{
    Console.Write("Starting the Live Event...".PadRight(60));
    await liveEvent.StartAsync(WaitUntil.Completed);
    Console.WriteLine("Done");

    // Refresh the liveEvent object's settings after starting it...
    return await liveEvent.GetAsync();
}

static void PrintIngestUrls(MediaLiveEventResource liveEvent)
{
    #region GetIngestUrl
    // Get the RTMP ingest URL. The endpoints is a collection of RTMP primary and secondary,
    // and RTMPS primary and secondary URLs.
    Console.WriteLine($"The RTMP ingest URL to enter into OBS Studio is:");
    Console.WriteLine(liveEvent.Data.Input.Endpoints.First(x => x.Uri.Scheme == "rtmps").Uri);
    Console.WriteLine("Make sure to enter a Stream Key into the OBS Studio settings. It can be");
    Console.WriteLine("any value or you can repeat the accessToken used in the ingest URL path.");
    Console.WriteLine();
    #endregion GetIngestUrls

    #region GetPreviewUrls
    // Use the previewEndpoint to preview and verify that the input from the encoder is actually
    // being received The preview endpoint URL also support the addition of various format strings
    // for HLS (format=m3u8-cmaf) and DASH (format=mpd-time-cmaf) for example. The default manifest
    // is Smooth.
    string previewEndpoint = liveEvent.Data.Preview.Endpoints.First().Uri.ToString();
    Console.WriteLine($"The preview URL is:");
    Console.WriteLine(previewEndpoint);
    Console.WriteLine();
    Console.WriteLine($"Open the live preview in your browser and use the Azure Media Player to monitor the preview playback:");
    Console.WriteLine($"https://ampdemo.azureedge.net/?url={HttpUtility.UrlEncode(previewEndpoint)}&heuristicprofile=lowlatency");
    Console.WriteLine();
    Console.WriteLine("Start the live stream now, sending the input to the ingest URL and verify");
    Console.WriteLine("that it is arriving with the preview URL.");
    Console.WriteLine("IMPORTANT: Make sure that the video is flowing to the Preview URL before continuing!");
    Console.WriteLine("Press enter to continue...");
    Console.ReadLine();
    #endregion GetPreviewUrls
}

static async Task<StreamingLocatorResource> DemoStreamingAsync(
    MediaServicesAccountResource mediaServicesAccount,
    MediaAssetResource asset,
    string streamingEndpointName,
    string streamingLocatorName,
    string manifestName,
    string dvrAssetFilterName)
{
    var filter = (await asset.GetMediaAssetFilters().CreateOrUpdateAsync(
        WaitUntil.Completed,
        dvrAssetFilterName,
        new MediaAssetFilterData
        {
            PresentationTimeRange = new PresentationTimeRange
            {
                ForceEndTimestamp = false,
                // 10 minute (600) seconds sliding window
                PresentationWindowDuration = 6000000000L,
                // This value defines the latest live position that a client can seek back to 2 seconds, must be smaller than sliding window.
                LiveBackoffDuration = 100000000L
            }
        })).Value;

    #region CreateStreamingLocator
    var streamingLocator = (await mediaServicesAccount.GetStreamingLocators().CreateOrUpdateAsync(
        WaitUntil.Completed,
        streamingLocatorName,
        new StreamingLocatorData
        {
            AssetName = asset.Data.Name,
            StreamingPolicyName = "Predefined_ClearStreamingOnly",
            Filters =
            {
                filter.Data.Name
            }
        })).Value;
    #endregion CreateStreamingLocator

    // Get the Streaming Endpoint
    var streamingEndpoint = (await mediaServicesAccount.GetStreamingEndpoints().GetAsync(streamingEndpointName)).Value;

    // If it's not running, start it
    var stopStreamingEndpoint = false;
    if (streamingEndpoint.Data.ResourceState != StreamingEndpointResourceState.Running)
    {
        Console.WriteLine("Streaming Endpoint is Stopped, starting it now...");
        await streamingEndpoint.StartAsync(WaitUntil.Completed);
        stopStreamingEndpoint = true;
    }

    // The next method "buildManifestPaths" is a helper to list the streaming manifests for HLS and
    // DASH. The paths are only available after the live streaming source has connected. If you wish
    // to get the streaming manifest ahead of time, make sure to set the manifest name in the
    // LiveOutput as done above. This allows you to have a deterministic manifest path. <streaming
    // endpoint hostname>/<streaming locator ID>/manifestName.ism/manifest(<format string>).

    var paths = BuildManifestPaths(
        streamingEndpoint.Data.HostName,
        streamingLocator.Data.StreamingLocatorId!.Value,
        manifestName);

    Console.WriteLine("The URLs to stream the output from a client:");
    Console.WriteLine($"The HLS (MP4) manifest for the live stream: {paths[0]}");
    Console.WriteLine("Open the following URL to playback the live stream in an HLS compliant player (HLS.js, Shaka, ExoPlayer) or directly in an iOS device");
    Console.WriteLine($"{paths[0]}");
    Console.WriteLine();
    Console.WriteLine($"The DASH manifest for the Live stream is: {paths[1]}");
    Console.WriteLine("Open the following URL to playback the live stream from the LiveOutput in the Azure Media Player");
    Console.WriteLine($"https://ampdemo.azureedge.net/?url={HttpUtility.UrlEncode(paths[1])}&heuristicprofile=lowlatency");
    Console.WriteLine();
    Console.WriteLine("Continue experimenting with the stream until you are ready to finish.");
    Console.WriteLine("Press enter to stop the Live Output...");
    Console.ReadLine();

    // If we started the endpoint, we'll stop it. Otherwise, we'll keep the endpoint running and print URLs
    // that can be played even after this sample ends.
    if (stopStreamingEndpoint)
    {
        await streamingEndpoint.StopAsync(WaitUntil.Completed);
    }

    return streamingLocator;
}

static string[] BuildManifestPaths(string hostname, Guid streamingLocatorId, string manifestName)
{
    const string hlsFormat = "format=m3u8-cmaf";
    const string dashFormat = "format=mpd-time-cmaf";

    return new string[]
    {
        $"https://{hostname}/{streamingLocatorId}/{manifestName}.ism/manifest({hlsFormat})",
        $"https://{hostname}/{streamingLocatorId}/{manifestName}.ism/manifest({dashFormat})"
    };
}

static async Task CleanupResourcesAsync(
    MediaLiveEventResource liveEvent,
    MediaLiveOutputResource liveOutput,
    StreamingLocatorResource streamingLocator,
    MediaAssetResource asset)
{
    #region Cleanup
    if (liveOutput != null)
    {
        Console.Write("Deleting the Live Output...".PadRight(60));
        await liveOutput.DeleteAsync(WaitUntil.Completed);
        Console.WriteLine("Done");
    }

    if (liveEvent?.Data.ResourceState == LiveEventResourceState.Running)
    {
        Console.Write("Stopping the Live Event...".PadRight(60));
        await liveEvent.StopAsync(WaitUntil.Completed, new LiveEventActionContent() { RemoveOutputsOnStop = true });
        Console.WriteLine("Done");
    }

    if (liveEvent != null)
    {
        Console.Write("Deleting the Live Event...".PadRight(60));
        await liveEvent.DeleteAsync(WaitUntil.Completed);
        Console.WriteLine("Done");
    }

    if (streamingLocator != null)
    {
        Console.Write("Deleting the Streaming Locator...".PadRight(60));
        await streamingLocator.DeleteAsync(WaitUntil.Completed);
        Console.WriteLine("Done");
    }

    if (asset != null)
    {
        Console.Write("Deleting the Asset...".PadRight(60));
        await asset.DeleteAsync(WaitUntil.Completed);
        Console.WriteLine("Done");
    }
    #endregion Cleanup
}

/// <summary>
/// Class to manage the settings which come from appsettings.json or command line parameters.
/// </summary>
public class Options
{
    [Required]
    public Guid? AZURE_SUBSCRIPTION_ID { get; set; }

    [Required]
    public string? AZURE_RESOURCE_GROUP { get; set; }

    [Required]
    public string? AZURE_MEDIA_SERVICES_ACCOUNT_NAME { get; set; }

    public string? AZURE_STORAGE_ACCOUNT_NAME { get; set; }

    public string? AZURE_BLOB_CONTAINER_NAME { get; set; }

    public string? EVENT_HUB_NAMESPACE { get; set; }

    public string? EVENT_HUB_NAME { get; set; }

    public string? EVENT_HUB_CONSUMER_GROUP_NAME { get; set; }

    static public bool TryGetOptions(IConfiguration configuration, [NotNullWhen(returnValue: true)] out Options? options)
    {
        try
        {
            options = configuration.Get<Options>() ?? throw new Exception("No configuration found. Configuration can be set in appsettings.json or using command line options.");
            Validator.ValidateObject(options, new ValidationContext(options), true);
            return true;
        }
        catch (Exception ex)
        {
            options = null;
            Console.WriteLine(ex.Message);
            return false;
        }
    }
}