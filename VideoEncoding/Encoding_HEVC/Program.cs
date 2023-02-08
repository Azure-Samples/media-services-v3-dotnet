// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Media;
using Azure.ResourceManager.Media.Models;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

const string OutputFolder = "Output";
const string CustomTransform = "Custom_HEVC_3_layers";
const string InputMP4FileName = "ignite.mp4";
const string DefaultStreamingEndpointName = "default";   // Change this to your Streaming Endpoint name

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
Console.WriteLine();

var mediaServicesResourceId = MediaServicesAccountResource.CreateResourceIdentifier(
    subscriptionId: options.AZURE_SUBSCRIPTION_ID.ToString(),
    resourceGroupName: options.AZURE_RESOURCE_GROUP,
    accountName: options.AZURE_MEDIA_SERVICES_ACCOUNT_NAME);

var credential = new DefaultAzureCredential(includeInteractiveCredentials: true);
var armClient = new ArmClient(credential);
var mediaServicesAccount = armClient.GetMediaServicesAccountResource(mediaServicesResourceId);

// Creating a unique suffix so that we don't have name collisions if you run the sample
// multiple times without cleaning up.
string uniqueness = Guid.NewGuid().ToString()[..13];
string jobName = $"job-{uniqueness}";
string locatorName = $"locator-{uniqueness}";
string inputAssetName = $"input-{uniqueness}";
string outputAssetName = $"output-{uniqueness}";
bool stopStreamingEndpoint = false;

// Ensure that you have customized encoding Transform. This is a one-time setup operation.
var transform = await CreateTransformAsync(mediaServicesAccount, CustomTransform);

// Create a new input Asset and upload the specified local video file into it.
var inputAsset = await CreateInputAssetAsync(mediaServicesAccount, inputAssetName, InputMP4FileName);

// Output from the Job must be written to an Asset, so let's create one.
var outputAsset = await CreateOutputAssetAsync(mediaServicesAccount, outputAssetName);

var job = await SubmitJobAsync(transform, jobName, inputAsset, outputAsset);

Console.WriteLine("Polling Job status...");
job = await WaitForJobToFinishAsync(job);

if (job.Data.State == MediaJobState.Error)
{
    Console.WriteLine($"ERROR: Job finished with error message: {job.Data.Outputs[0].Error.Message}");
    Console.WriteLine($"ERROR:                   error details: {job.Data.Outputs[0].Error.Details[0].Message}");
    await CleanUpAsync(transform, job, inputAsset, outputAsset, null, stopStreamingEndpoint, null);
    return;
}

Console.WriteLine("Job finished.");
Directory.CreateDirectory(OutputFolder);

await DownloadResultsAsync(outputAsset, OutputFolder);

var streamingLocator = await CreateStreamingLocatorAsync(mediaServicesAccount, outputAsset.Data.Name, locatorName);

var streamingEndpoint = (await mediaServicesAccount.GetStreamingEndpoints().GetAsync(DefaultStreamingEndpointName)).Value;

if (streamingEndpoint.Data.ResourceState != StreamingEndpointResourceState.Running)
{
    Console.WriteLine("Streaming Endpoint is not running, starting now...");
    await streamingEndpoint.StartAsync(WaitUntil.Completed);

    // Since we started the endpoint, we should stop it in cleanup.
    stopStreamingEndpoint = true;
}

Console.WriteLine();
Console.WriteLine("Getting the streaming manifest URLs for HLS and DASH:");
await PrintStreamingUrlsAsync(streamingLocator, streamingEndpoint);

Console.WriteLine("To try streaming, copy and paste the streaming URL into the Azure Media Player at 'http://aka.ms/azuremediaplayer'.");
Console.WriteLine("When finished, press ENTER to cleanup.");
Console.WriteLine();
Console.ReadLine();

await CleanUpAsync(transform, job, inputAsset, outputAsset, streamingLocator, stopStreamingEndpoint, streamingEndpoint);

#region EnsureTransformExists
/// <summary>
/// If the specified transform exists, return that transform. If the it does not
/// exist, creates a new transform with the specified output. In this case, the
/// output is set to encode a video using a custom preset.
/// </summary>
/// <param name="mediaServicesAccount">The Media Services account.</param>
/// <param name="transformName">The transform name.</param>
/// <returns>The transform.</returns>
static async Task<MediaTransformResource> CreateTransformAsync(MediaServicesAccountResource mediaServicesAccount, string transformName)
{
    Console.WriteLine("Creating a Transform...");

    // Create the custom Transform with the outputs defined above
    // Does a Transform already exist with the desired name? This method will just overwrite (Update) the Transform if it exists already. 
    // In production code, you may want to be cautious about that. It really depends on your scenario.
    var transform = await mediaServicesAccount.GetMediaTransforms().CreateOrUpdateAsync(
        WaitUntil.Completed,
        transformName,
        new MediaTransformData
        {
            Outputs =
            {
                // Create a new TransformOutput with a custom Standard Encoder Preset using the HEVC (H265Layer) codec
                // This demonstrates how to create custom codec and layer output settings
                new MediaTransformOutput(
                    preset: new StandardEncoderPreset(
                        codecs: new MediaCodecBase[]
                        {
                            // Add an AAC Audio layer for the audio encoding
                            new AacAudio
                            {
                                Channels = 2,
                                SamplingRate = 48000,
                                Bitrate = 128000,
                                Profile = AacAudioProfile.AacLc
                            },
                            // Next, add a HEVC (H.265) for the video encoding
                            new H265Video
                            {
                                // Set the GOP interval to 2 seconds for all H265Layers
                                KeyFrameInterval = TimeSpan.FromSeconds(2),
                                Complexity = H265Complexity.Speed, // HEVC encoding is priced at 3 complexity levels. Speed, Balanced, and Quality
                        
                                // Add H265Layers. Assign a label that you can use for the output filename
                                Layers =
                                {
                                    new H265Layer(bitrate: 1800000)
                                    {
                                        MaxBitrate = 1800000, // unit is in bits per second and not kbps or Mbps
                                        Width = "1280",
                                        Height = "720",
                                        BFrames = 4,
                                        Label = "HD-1800kbps" // This label is used to modify the file name in the output formats
                                    },
                                    new H265Layer(bitrate: 800000)
                                    {
                                        MaxBitrate = 800000, // unit is in bits per second and not kbps or Mbps
                                        Width = "960",
                                        Height = "540",
                                        BFrames = 4,
                                        Label = "SD-800kbps" // This label is used to modify the file name in the output formats
                                    },
                                    new H265Layer(bitrate: 300000)
                                    {
                                        MaxBitrate = 300000, // unit is in bits per second and not kbps or Mbps
                                        Width = "640",
                                        Height = "360",
                                        BFrames = 4,
                                        Label = "SD-300kbps" // This label is used to modify the file name in the output formats
                                    }
                                }
                            },
                            // Also generate a set of PNG thumbnails
                            new PngImage(start: "25%")
                            {
                                Step = "25%",
                                Range = "80%",
                                Layers =
                                {
                                    new PngLayer
                                    {
                                        Width = "50%",
                                        Height = "50%"
                                    }
                                }
                            }
                        },
                        // Specify the format for the output files - one for video+audio, and another for the thumbnails
                        formats: new MediaFormatBase[]
                        {
                            // Mux the H.265 video and AAC audio into MP4 files, using basename, label, bitrate and extension macros
                            // Note that since you have multiple H265Layers defined above, you have to use a macro that produces unique names per H254Layer
                            // Either {Label} or {Bitrate} should suffice
                            new Mp4Format(filenamePattern: "Video-{Basename}-{Label}-{Bitrate}{Extension}"),
                            new PngFormat(filenamePattern: "Thumbnail-{Basename}-{Index}{Extension}")
                        }
                    )
                )
                {
                    OnError = MediaTransformOnErrorType.StopProcessingJob,
                    RelativePriority = MediaJobPriority.Normal
                }
            },
            Description = "A custom encoding transform for HEVC with 3 MP4 bitrates"
        });

    return transform.Value;
}
#endregion EnsureTransformExists

/// <summary>
/// Creates an output Asset. The output from the encoding Job must be written to an Asset.
/// </summary>
/// <param name="mediaServicesAccount">The Media Services account.</param>
/// <param name="assetName">The output Asset name.</param>
/// <returns></returns>
static async Task<MediaAssetResource> CreateOutputAssetAsync(MediaServicesAccountResource mediaServicesAccount, string assetName)
{
    Console.WriteLine("Creating an output Asset...");
    var asset = await mediaServicesAccount.GetMediaAssets().CreateOrUpdateAsync(
        WaitUntil.Completed,
        assetName,
        new MediaAssetData());

    return asset.Value;
}

/// <summary>
/// Submits a request to Media Services to apply the specified Transform to a given input video.
/// </summary>
/// <param name="transform">The media transform.</param>
/// <param name="jobName">The (unique) name of the Job.</param>
/// <param name="inputAsset">The input Asset.</param>
/// <param name="outputAsset">The output Asset that will store the result of the encoding Job.</param>
static async Task<MediaJobResource> SubmitJobAsync(
    MediaTransformResource transform,
    string jobName,
    MediaAssetResource inputAsset,
    MediaAssetResource outputAsset)
{
    // In this example, we are assuming that the Job name is unique.
    //
    // If you already have a Job with the desired name, use the Jobs.Get method
    // to get the existing Job. In Media Services v3, Get methods on entities returns ErrorResponseException 
    // if the entity doesn't exist (a case-insensitive check on the name).
    Console.WriteLine("Creating a Job...");
    var job = await transform.GetMediaJobs().CreateOrUpdateAsync(
        WaitUntil.Completed,
        jobName,
        new MediaJobData
        {
            Input = new MediaJobInputAsset(assetName: inputAsset.Data.Name),
            Outputs =
            {
                new MediaJobOutputAsset(outputAsset.Data.Name)
            }
        });

    return job.Value;
}

/// <summary>
/// Polls Media Services for the status of the Job.
/// </summary>
/// <param name="job">The Job.</param>
/// <returns>The updated Job.</returns>
static async Task<MediaJobResource> WaitForJobToFinishAsync(MediaJobResource job)
{
    var sleepInterval = TimeSpan.FromSeconds(30);
    MediaJobState? state;

    do
    {
        job = await job.GetAsync();
        state = job.Data.State.GetValueOrDefault();

        Console.WriteLine($"Job is '{state}'.");
        for (int i = 0; i < job.Data.Outputs.Count; i++)
        {
            var output = job.Data.Outputs[i];
            Console.Write($"\tJobOutput[{i}] is '{output.State}'.");
            if (output.State == MediaJobState.Processing)
            {
                Console.Write($"  Progress: '{output.Progress}'.");
            }

            Console.WriteLine();
        }

        if (state != MediaJobState.Finished && state != MediaJobState.Error && state != MediaJobState.Canceled)
        {
            await Task.Delay(sleepInterval);
        }
    }
    while (state != MediaJobState.Finished && state != MediaJobState.Error && state != MediaJobState.Canceled);

    return job;
}

/// <summary>
/// Creates a new input Asset and uploads the specified local video file into it.
/// </summary>
/// <param name="mediaServicesAccount">The Media Services client.</param>
/// <param name="assetName">The Asset name.</param>
/// <param name="fileToUpload">The file you want to upload into the Asset.</param>
/// <returns></returns>
static async Task<MediaAssetResource> CreateInputAssetAsync(MediaServicesAccountResource mediaServicesAccount, string assetName, string fileToUpload)
{
    // In this example, we are assuming that the Asset name is unique.
    MediaAssetResource asset;

    try
    {
        asset = await mediaServicesAccount.GetMediaAssets().GetAsync(assetName);

        // The Asset already exists and we are going to overwrite it. In your application, if you don't want to overwrite
        // an existing Asset, use an unique name.
        Console.WriteLine($"Warning: The Asset named {assetName} already exists. It will be overwritten.");
    }
    catch (RequestFailedException)
    {
        // Call Media Services API to create an Asset.
        // This method creates a container in storage for the Asset.
        // The files (blobs) associated with the Asset will be stored in this container.
        Console.WriteLine("Creating an input Asset...");
        asset = (await mediaServicesAccount.GetMediaAssets().CreateOrUpdateAsync(WaitUntil.Completed, assetName, new MediaAssetData())).Value;
    }

    // Use Media Services API to get back a response that contains
    // SAS URL for the Asset container into which to upload blobs.
    // That is where you would specify read-write permissions
    // and the expiration time for the SAS URL.
    var sasUriCollection = asset.GetStorageContainerUrisAsync(
        new MediaAssetStorageContainerSasContent
        {
            Permissions = MediaAssetContainerPermission.ReadWrite,
            ExpireOn = DateTime.UtcNow.AddHours(1)
        });

    var sasUri = await sasUriCollection.FirstOrDefaultAsync();

    // Use Storage API to get a reference to the Asset container
    // that was created by calling Asset's CreateOrUpdate method.
    var container = new BlobContainerClient (sasUri);
    BlobClient blob = container.GetBlobClient(Path.GetFileName(fileToUpload));

    // Use Storage API to upload the file into the container in storage.
    Console.WriteLine("Uploading a media file to the Asset...");
    await blob.UploadAsync(fileToUpload);

    return asset;
}

/// <summary>
/// Downloads the specified output Asset.
/// </summary>
/// <param name="assetName">The Asset to download from.</param>
/// <param name="outputFolderName">The name of the folder into which to download the results.</param>
/// <returns></returns>
async static Task DownloadResultsAsync(MediaAssetResource asset, string outputFolderName)
{
    // Use Media Service and Storage APIs to download the output files to a local folder
    var assetContainerSas = asset.GetStorageContainerUrisAsync(new MediaAssetStorageContainerSasContent
    {
        Permissions = MediaAssetContainerPermission.Read,
        ExpireOn = DateTime.UtcNow.AddHours(1)
    });

    var containerSasUrl = await assetContainerSas.FirstAsync();

    var container = new BlobContainerClient(containerSasUrl);

    string directory = Path.Combine(outputFolderName, asset.Data.Name);
    Directory.CreateDirectory(directory);

    Console.WriteLine("Downloading results to {0}.", directory);

    await foreach (var blob in container.GetBlobsAsync())
    {
        var blobClient = container.GetBlobClient(blob.Name);
        string filename = Path.Combine(directory, blob.Name);
        await blobClient.DownloadToAsync(filename);
    }

    Console.WriteLine("Download complete.");
}

/// <summary>
/// Creates a StreamingLocator for the specified Asset and with the specified streaming policy name.
/// Once the StreamingLocator is created the output Asset is available to clients for playback.
/// </summary>
/// <param name="mediaServicesAccount">The Media Services client.</param>
/// <param name="assetName">The name of the output Asset.</param>
/// <param name="locatorName">The StreamingLocator name (unique in this case).</param>
/// <returns></returns>
static async Task<StreamingLocatorResource> CreateStreamingLocatorAsync(
    MediaServicesAccountResource mediaServicesAccount,
    string assetName,
    string locatorName)
{
    var locator = await mediaServicesAccount.GetStreamingLocators().CreateOrUpdateAsync(
        WaitUntil.Completed,
        locatorName,
        new StreamingLocatorData
        {
            AssetName = assetName,
            StreamingPolicyName = "Predefined_ClearStreamingOnly"
        });

    return locator.Value;
}

/// <summary>
/// Prints the streaming URLs.
/// </summary>
/// <param name="locator">The streaming locator.</param>
/// <param name="streamingEndpoint">The streaming endpoint.</param>
static async Task PrintStreamingUrlsAsync(
    StreamingLocatorResource locator,
    StreamingEndpointResource streamingEndpoint)
{
    var paths = await locator.GetStreamingPathsAsync();

    foreach (StreamingPath path in paths.Value.StreamingPaths)
    {
        Console.WriteLine($"The following formats are available for {path.StreamingProtocol.ToString().ToUpper()}:");
        foreach (string streamingFormatPath in path.Paths)
        {
            var uriBuilder = new UriBuilder()
            {
                Scheme = "https",
                Host = streamingEndpoint.Data.HostName,
                Path = streamingFormatPath
            };
            Console.WriteLine($"\t{uriBuilder}");
        }
        Console.WriteLine();
    }
}

/// <summary>
/// Delete the resources that were created.
/// </summary>
/// <param name="transform">The transform.</param>
/// <param name="job">The Job.</param>
/// <param name="inputAsset">The input Asset.</param>
/// <param name="outputAsset">The output Asset.</param>
/// <param name="streamingLocator">The streaming locator. </param>
/// <param name="stopEndpoint">Stop endpoint if true, keep endpoint running if false.</param>
/// <param name="streamingEndpoint">The streaming endpoint.</param>
/// <returns>A task.</returns>
static async Task CleanUpAsync(
    MediaTransformResource transform,
    MediaJobResource job,
    MediaAssetResource? inputAsset,
    MediaAssetResource outputAsset,
    StreamingLocatorResource? streamingLocator,
    bool stopEndpoint,
    StreamingEndpointResource? streamingEndpoint)
{
    await job.DeleteAsync(WaitUntil.Completed);
    await transform.DeleteAsync(WaitUntil.Completed);

    if (inputAsset != null)
    {
        await inputAsset.DeleteAsync(WaitUntil.Completed);
    }

    await outputAsset.DeleteAsync(WaitUntil.Completed);

    if (streamingLocator != null)
    {
        await streamingLocator.DeleteAsync(WaitUntil.Completed);
    }

    if (streamingEndpoint != null)
    {
        if (stopEndpoint)
        {
            // Because we started the endpoint, we'll stop it.
            await streamingEndpoint.StopAsync(WaitUntil.Completed);
        }
        else
        {
            // We will keep the endpoint running because it was not started by us. There are costs to keep it running.
            // Please refer https://azure.microsoft.com/en-us/pricing/details/media-services/ for pricing. 
            Console.WriteLine($"The Streaming Endpoint '{streamingEndpoint.Data.Name}' is running. To stop further billing for the Streaming Endpoint, please stop it using the Azure portal.");
        }
    }
}

/// <summary>
/// Class to manage the settings which come from appsettings.json or command line parameters.
/// </summary>
internal class Options
{
    [Required]
    public Guid? AZURE_SUBSCRIPTION_ID { get; set; }

    [Required]
    public string? AZURE_RESOURCE_GROUP { get; set; }

    [Required]
    public string? AZURE_MEDIA_SERVICES_ACCOUNT_NAME { get; set; }

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
