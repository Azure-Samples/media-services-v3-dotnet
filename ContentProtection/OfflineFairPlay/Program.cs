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

const string AdaptiveStreamingTransformName = "MyTransformWithAdaptiveStreamingPreset";
const string DefaultStreamingEndpointName = "default";   // Change this to your Streaming Endpoint name
byte[] TokenSigningKey = new byte[40];
const string SourceUri = "https://nimbuscdn-nimbuspm.streaming.mediaservices.windows.net/2b533311-b215-4409-80af-529c3e853622/Ignite-short.mp4";
const string FairPlayStreamingPolicyName = "FairPlayCustomStreamingPolicyName";

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

var mediaServiceAccountId = MediaServicesAccountResource.CreateResourceIdentifier(
   subscriptionId: options.AZURE_SUBSCRIPTION_ID.ToString(),
   resourceGroupName: options.AZURE_RESOURCE_GROUP,
   accountName: options.AZURE_MEDIA_SERVICES_ACCOUNT_NAME);

var credential = new DefaultAzureCredential(includeInteractiveCredentials: true);
var armClient = new ArmClient(credential);

var mediaServicesAccount = armClient.GetMediaServicesAccountResource(mediaServiceAccountId);

// Creating a unique suffix so that we don't have name collisions if you run the sample
// multiple times without cleaning up.
string uniqueness = Guid.NewGuid().ToString()[..13];
string jobName = $"job-{uniqueness}";
string locatorName = $"locator-{uniqueness}";
string inputAssetName = $"input-{uniqueness}";
string outputAssetName = $"output-{uniqueness}";
// Ideally, the content key policy should be reused. We don't do it in the sample as the signing key
// is generated at each run
string contentKeyPolicyName = $"contentkeypolicy-{uniqueness}";
bool stopStreamingEndpoint = false;

// Ensure that you have customized encoding Transform. This is a one-time setup operation.
var transform = await CreateTransformAsync(mediaServicesAccount, AdaptiveStreamingTransformName);

// Output from the Job must be written to an Asset, so let's create one.
var outputAsset = await CreateOutputAssetAsync(mediaServicesAccount, outputAssetName);

var job = await SubmitJobAsync(transform, jobName, new Uri(SourceUri), outputAsset);

Console.WriteLine("Polling Job status...");
job = await WaitForJobToFinishAsync(job);

if (job.Data.State == MediaJobState.Error)
{
    Console.WriteLine($"ERROR: Job finished with error message: {job.Data.Outputs[0].Error.Message}");
    Console.WriteLine($"ERROR:                   error details: {job.Data.Outputs[0].Error.Details[0].Message}");
    await CleanUpAsync(transform, job, null, outputAsset, null, stopStreamingEndpoint, null, null);
    return;
}

Console.WriteLine("Job finished.");

// Create the content key policy that configures how the content key is delivered to end clients
// via the Key Delivery component of Azure Media Services.
var contentKeyPolicy = await GetOrCreateContentKeyPolicyAsync(mediaServicesAccount, contentKeyPolicyName, options);

var customStreamingPolicy = await GetOrCreateCustomStreamingPolicyForFairPlayAsync(mediaServicesAccount, FairPlayStreamingPolicyName);

var streamingLocator = await CreateStreamingLocatorAsync(mediaServicesAccount, outputAsset, locatorName, contentKeyPolicy.Data.Name, customStreamingPolicy.Data.Name);

var streamingEndpoint = (await mediaServicesAccount.GetStreamingEndpoints().GetAsync(DefaultStreamingEndpointName)).Value;

if (streamingEndpoint.Data.ResourceState != StreamingEndpointResourceState.Running)
{
    Console.WriteLine("Streaming Endpoint is not running, starting now...");
    await streamingEndpoint.StartAsync(WaitUntil.Completed);

    // Since we started the endpoint, we should stop it in cleanup.
    stopStreamingEndpoint = true;
}

Console.WriteLine();
Console.WriteLine("Getting the streaming manifest URLs for HLS:");
await PrintHlsStreamingUrlsAsync(streamingLocator, streamingEndpoint);
Console.WriteLine();
Console.WriteLine("When finished, press ENTER to cleanup.");
Console.WriteLine();
Console.ReadLine();

await CleanUpAsync(transform, job, null, outputAsset, streamingLocator, stopStreamingEndpoint, streamingEndpoint, contentKeyPolicy);

/// <summary>
/// Configures FairPlay license template.
/// </summary>
/// <param name="askHex">The ASK hex string.</param>
/// <param name="fairPlayPfxPath">The path of the PFX file.</param>
/// <param name="fairPlayPfxPassword">The password for the PFX.</param>
/// <returns>ContentKeyPolicyFairPlayConfiguration</returns>
static ContentKeyPolicyFairPlayConfiguration ConfigureFairPlayLicenseTemplate(string askHex, string fairPlayPfxPath, string fairPlayPfxPassword)
{
    byte[] askBytes = Enumerable
        .Range(0, askHex.Length)
        .Where(x => x % 2 == 0)
        .Select(x => Convert.ToByte(askHex.Substring(x, 2), 16))
        .ToArray();

    byte[] buf = File.ReadAllBytes(fairPlayPfxPath);
    string appCertBase64 = Convert.ToBase64String(buf);

    var objContentKeyPolicyPlayReadyConfiguration = new ContentKeyPolicyFairPlayConfiguration(
        applicationSecretKey: askBytes,
        fairPlayPfxPassword: fairPlayPfxPassword,
        fairPlayPfx: appCertBase64,
        rentalAndLeaseKeyType: ContentKeyPolicyFairPlayRentalAndLeaseKeyType.DualExpiry,
        rentalDuration: 0) // in seconds)
    {
        OfflineRentalConfiguration = new ContentKeyPolicyFairPlayOfflineRentalConfiguration(
            playbackDurationInSeconds: 500000,
            storageDurationInSeconds: 300000)

    };
    return objContentKeyPolicyPlayReadyConfiguration;
};


/// <summary>
/// Create the content key policy that configures how the content key is delivered to end clients 
/// via the Key Delivery component of Azure Media Services.
/// </summary>
/// <param name="mediaServicesAccount">The Media Services account.</param>
/// <param name="contentKeyPolicyName">The name of the content key policy resource.</param>
/// <param name="options">The settings for the application.</param>
/// <returns></returns>
static async Task<ContentKeyPolicyResource> GetOrCreateContentKeyPolicyAsync(
    MediaServicesAccountResource mediaServicesAccount,
    string contentKeyPolicyName,
    Options options)
{
    Console.WriteLine("Creating a content key policy...");

    var policy = await mediaServicesAccount.GetContentKeyPolicies().CreateOrUpdateAsync(
        WaitUntil.Completed,
        contentKeyPolicyName,
        new ContentKeyPolicyData
        {
            Options =
            {
                 new ContentKeyPolicyOption(
                       configuration: ConfigureFairPlayLicenseTemplate(options.AskHex, options.FairPlayPfxPath, options.FairPlayPfxPassword),
                       restriction:  new ContentKeyPolicyOpenRestriction())                      
            }
        }
        );

    return policy.Value;
}

/// <summary>
/// If the specified transform exists, return that transform. If the it does not
/// exist, creates a new transform with the specified output. In this case, the
/// output is set to encode a video using a built-in encoding preset.
/// </summary>
/// <param name="mediaServicesAccount">The Media Services account.</param>
/// <param name="transformName">The transform name.</param>
/// <returns></returns>
static async Task<MediaTransformResource> CreateTransformAsync(MediaServicesAccountResource mediaServicesAccount, string transformName)
{
    var transform = await mediaServicesAccount.GetMediaTransforms().CreateOrUpdateAsync(
       WaitUntil.Completed,
       transformName,
       new MediaTransformData
       {
           Outputs =
           {
               new MediaTransformOutput
               (
                   // The preset for the Transform is set to one of Media Services built-in sample presets.
                   // You can  customize the encoding settings by changing this to use "StandardEncoderPreset" class.
                   preset: new BuiltInStandardEncoderPreset(
                       // This sample uses the built-in encoding preset for Adaptive Bit-rate Streaming.
                       presetName: EncoderNamedPreset.AdaptiveStreaming
                       )
                   )
           }
       }
       );

    return transform.Value;
}

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
/// <param name="sourceUri">The input source Uri.</param>
/// <param name="outputAsset">The output Asset that will store the result of the encoding Job.</param>
static async Task<MediaJobResource> SubmitJobAsync(
    MediaTransformResource transform,
    string jobName,
    Uri sourceUri,
    MediaAssetResource outputAsset)
{
    // In this example, we are assuming that the Job name is unique.
    //
    // If you already have a Job with the desired name, use the Jobs.Get method
    // to get the existing Job. In Media Services v3, Get methods on entities returns ErrorResponseException 
    // if the entity doesn't exist (a case-insensitive check on the name).
    Console.WriteLine("Creating a Job...");

    var baseUri = new Uri(sourceUri.GetLeftPart(UriPartial.Authority));
    var file = sourceUri.AbsolutePath;

    var job = await transform.GetMediaJobs().CreateOrUpdateAsync(
        WaitUntil.Completed,
        jobName,
        new MediaJobData
        {
            Input = new MediaJobInputHttp()
            {
                BaseUri = baseUri,
                Files = { file }
            },
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
    MediaJobState state;

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
/// Creates a StreamingLocator for the specified Asset and with the specified streaming policy name.
/// Once the StreamingLocator is created the output Asset is available to clients for playback.
/// </summary>
/// <param name="mediaServicesAccount">The Media Services client.</param>
/// <param name="asset">The asset.</param>
/// <param name="locatorName">The name of the locator to create.</param>
/// <param name="contentPolicyName">The content key policy name.</param>
/// <param name="streamingPolicyName">The streaming policy name.</param>
/// <returns></returns>
static async Task<StreamingLocatorResource> CreateStreamingLocatorAsync(
    MediaServicesAccountResource mediaServicesAccount,
    MediaAssetResource asset,
    string locatorName,
    string contentPolicyName,
    string streamingPolicyName)
{
    Console.WriteLine("Creating a streaming locator...");

    var locator = await mediaServicesAccount.GetStreamingLocators().CreateOrUpdateAsync(
        WaitUntil.Completed,
        locatorName,
        new StreamingLocatorData
        {
            AssetName = asset.Data.Name,
            StreamingPolicyName = streamingPolicyName,
            DefaultContentKeyPolicyName = contentPolicyName
        });

    return locator.Value;
}


/// <summary>
/// Get or create a custom streaming policy for FairPlay.
/// </summary>
/// <param name="mediaServicesAccount">The Media Services client.</param>
/// <param name="streamingPolicyName">The streaming policy name.</param>
/// <returns>StreamingPolicyResource</returns>
static async Task<StreamingPolicyResource> GetOrCreateCustomStreamingPolicyForFairPlayAsync(MediaServicesAccountResource mediaServicesAccount, string streamingPolicyName)
{
    StreamingPolicyResource streamingPolicy;

    try
    {
        streamingPolicy = (await mediaServicesAccount.GetStreamingPolicyAsync(streamingPolicyName)).Value;
        Console.WriteLine($"Warning: The streaming policy named {streamingPolicyName} already exists.");
    }

    catch (RequestFailedException ex) when (ex.Status == ((int)System.Net.HttpStatusCode.NotFound))
    {
        // Content key policy does not exist
        var streamingPolicyData = new StreamingPolicyData()
        {
            CommonEncryptionCbcs = new CommonEncryptionCbcs()
            {
                Drm = new CbcsDrmConfiguration()
                {
                    FairPlay = new StreamingPolicyFairPlayConfiguration(
                      allowPersistentLicense: true)  // this enables offline mode
                },
                EnabledProtocols = new MediaEnabledProtocols(
                   isDownloadEnabled: false,
                   isHlsEnabled: true,
                   isDashEnabled: true, //Even though DASH under CBCS is not supported for either CSF or CMAF, HLS-CMAF-CBCS uses DASH-CBCS fragments in its HLS playlist
                   isSmoothStreamingEnabled: false),
                ContentKeys = new StreamingPolicyContentKeys()
                {
                    //Default key must be specified if keyToTrackMappings is present
                    DefaultKey = new EncryptionSchemeDefaultKey()
                    {
                        Label = "CBCS_DefaultKeyLabel"
                    }
                }
            }
        };
        streamingPolicy = (await mediaServicesAccount.GetStreamingPolicies().CreateOrUpdateAsync(WaitUntil.Completed, streamingPolicyName, streamingPolicyData)).Value;
    }

    return streamingPolicy;
}

/// <summary>
/// Prints the streaming URLs for HLS.
/// </summary>
/// <param name="locator">The streaming locator.</param>
/// <param name="streamingEndpoint">The streaming endpoint.</param>
static async Task PrintHlsStreamingUrlsAsync(
    StreamingLocatorResource locator,
    StreamingEndpointResource streamingEndpoint)
{
    var paths = await locator.GetStreamingPathsAsync();

    foreach (StreamingPath path in paths.Value.StreamingPaths)
    {
        if (path.StreamingProtocol == StreamingPolicyStreamingProtocol.Hls)
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
/// <param name="contentKeyPolicy">The content key policyt.</param>
/// <returns>A task.</returns>
static async Task CleanUpAsync(
    MediaTransformResource transform,
    MediaJobResource job,
    MediaAssetResource? inputAsset,
    MediaAssetResource outputAsset,
    StreamingLocatorResource? streamingLocator,
    bool stopEndpoint,
    StreamingEndpointResource? streamingEndpoint,
    ContentKeyPolicyResource? contentKeyPolicy)
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

    if (contentKeyPolicy != null)
    {
        await contentKeyPolicy.DeleteAsync(WaitUntil.Completed);
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

    [Required]
    public string? AskHex { get; set; }

    [Required]
    public string? FairPlayPfxPath { get; set; }

    [Required]
    public string? FairPlayPfxPassword { get; set; }

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
