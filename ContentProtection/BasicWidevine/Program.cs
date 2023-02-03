// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Media;
using Azure.ResourceManager.Media.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using WidevineConfig;

const string AdaptiveStreamingTransformName = "MyTransformWithAdaptiveStreamingPreset";
const string Issuer = "myIssuer";
const string Audience = "myAudience";
const string DefaultStreamingEndpointName = "default";   // Change this to your Streaming Endpoint name
byte[] TokenSigningKey = new byte[40];
const string SourceUri = "https://nimbuscdn-nimbuspm.streaming.mediaservices.windows.net/2b533311-b215-4409-80af-529c3e853622/Ignite-short.mp4";


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

// Generate a new random token signing key to use
RandomNumberGenerator.Create().GetBytes(TokenSigningKey);
var ckTokenSigningKey = new ContentKeyPolicySymmetricTokenKey(TokenSigningKey);

// Create the content key policy that configures how the content key is delivered to end clients
// via the Key Delivery component of Azure Media Services.
var contentKeyPolicy = await GetOrCreateContentKeyPolicyAsync(mediaServicesAccount, contentKeyPolicyName, ckTokenSigningKey);

var streamingLocator = await CreateStreamingLocatorAsync(mediaServicesAccount, outputAsset, locatorName, contentKeyPolicy.Data.Name, "Predefined_MultiDrmCencStreaming");

// We are using the ContentKeyIdentifierClaim in the ContentKeyPolicy which means that the token presented
// to the Key Delivery Component must have the identifier of the content key in it.  Since we didn't specify
// a content key when creating the StreamingLocator, the system created a random one for us.  In order to 
// generate our test token we must get the ContentKeyId to put in the ContentKeyIdentifierClaim claim.
string keyIdentifier = streamingLocator.GetContentKeys().First().Id.ToString();

string token = GetToken(Issuer, Audience, keyIdentifier, ckTokenSigningKey);

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

var smoothPath = await ReturnSmoothStreamingUrlAsync(streamingLocator, streamingEndpoint);
Console.WriteLine();
Console.WriteLine("Copy and paste the following URL in your browser to play back the file in the Azure Media Player.");
Console.WriteLine("Note, the player is set to use the Widevine token and the Bearer token is specified.");
Console.WriteLine("The token is valid 60 minutes and can be used 5 times.");
Console.WriteLine();
Console.WriteLine($"https://ampdemo.azureedge.net/?url={smoothPath}&widevine=true&widevinetoken=Bearer%20{token}");
Console.WriteLine();

Console.WriteLine("When finished, press ENTER to cleanup.");
Console.WriteLine();
Console.ReadLine();

await CleanUpAsync(transform, job, null, outputAsset, streamingLocator, stopStreamingEndpoint, streamingEndpoint, contentKeyPolicy);

/// <summary>
/// Configures Widevine license template.
/// </summary>
/// <returns></returns>
static ContentKeyPolicyWidevineConfiguration ConfigureWidevineLicenseTemplate()
{
    var template = new WidevineTemplate()
    {
        AllowedTrackTypes = "SD_HD",
        ContentKeySpecs = new ContentKeySpec[]
        {
                    new ContentKeySpec()
                    {
                        TrackType = "SD",
                        SecurityLevel = 1,
                        RequiredOutputProtection = new OutputProtection()
                        {
                            HDCP = "HDCP_NONE"
                            // NOTE: the policy should be set to "HDCP_V1" (or greater) if you need to disable screen capture. The Widevine desktop
                            // browser CDM module only blocks screen capture when HDCP is enabled and the screen capture application is using
                            // Chromes screen capture APIs. 
                        }
                    }
        },
        PolicyOverrides = new PolicyOverrides()
        {
            CanPlay = true,
            CanPersist = false,
            CanRenew = false,
            RentalDurationSeconds = 2592000,
            PlaybackDurationSeconds = 10800,
            LicenseDurationSeconds = 604800,
        }
    };
    return new ContentKeyPolicyWidevineConfiguration(Newtonsoft.Json.JsonConvert.SerializeObject(template));
}

/// <summary>
/// Create the content key policy that configures how the content key is delivered to end clients 
/// via the Key Delivery component of Azure Media Services.
/// </summary>
/// <param name="mediaServicesAccount">The Media Services account.</param>
/// <param name="contentKeyPolicyName">The name of the content key policy resource.</param>
/// <param name="ckTokenKey">The primary verification key.</param>
/// <returns></returns>
static async Task<ContentKeyPolicyResource> GetOrCreateContentKeyPolicyAsync(
    MediaServicesAccountResource mediaServicesAccount,
    string contentKeyPolicyName,
    ContentKeyPolicySymmetricTokenKey ckTokenKey)
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
                       configuration: ConfigureWidevineLicenseTemplate(),
                       restriction:  new ContentKeyPolicyTokenRestriction(
                          issuer: Issuer,
                          audience : Audience,
                          primaryVerificationKey: ckTokenKey,
                          restrictionTokenType: ContentKeyPolicyRestrictionTokenType.Jwt)
                       {
                           RequiredClaims = { new ContentKeyPolicyTokenClaim{ ClaimType ="urn:microsoft:azure:mediaservices:contentkeyidentifier" } },
                       }
                       )
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
/// Create a token that will be used to protect your stream.
/// Only authorized clients would be able to play the video.  
/// </summary>
/// <param name="issuer">The issuer is the secure token service that issues the token. </param>
/// <param name="audience">The audience, sometimes called scope, describes the intent of the token or the resource the token authorizes access to. </param>
/// <param name="keyIdentifier">The content key ID.</param>
/// <param name="ckTokenKey">Contains the key that the token was signed with. </param>
/// <returns></returns>
// <GetToken>
static string GetToken(string issuer, string audience, string keyIdentifier, ContentKeyPolicySymmetricTokenKey ckTokenKey)
{
    var tokenSigningKey = new SymmetricSecurityKey(ckTokenKey.KeyValue);

    var cred = new SigningCredentials(
        tokenSigningKey,
        // Use the  HmacSha256 and not the HmacSha256Signature option, or the token will not work!
        SecurityAlgorithms.HmacSha256,
        SecurityAlgorithms.Sha256Digest);

    var claims = new List<Claim>()
    {
        new Claim("urn:microsoft:azure:mediaservices:contentkeyidentifier", keyIdentifier),
        new Claim("urn:microsoft:azure:mediaservices:maxuses", "5")
    };

    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        notBefore: DateTime.Now.AddMinutes(-5),
        expires: DateTime.Now.AddMinutes(60),
        signingCredentials: cred);

    var handler = new JwtSecurityTokenHandler();

    return handler.WriteToken(token);
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
/// Gets the smooth streaming Url.
/// </summary>
/// <param name="locator">The streaming locator.</param>
/// <param name="streamingEndpoint">The streaming endpoint.</param>
static async Task<Uri> ReturnSmoothStreamingUrlAsync(
    StreamingLocatorResource locator,
    StreamingEndpointResource streamingEndpoint)
{
    var paths = await locator.GetStreamingPathsAsync();

    var smooth = paths.Value.StreamingPaths.Where(p => p.StreamingProtocol == StreamingPolicyStreamingProtocol.SmoothStreaming).First();

    var urib = new UriBuilder()
    {
        Scheme = "https",
        Host = streamingEndpoint.Data.HostName,
        Path = smooth.Paths[0]
    };

    return urib.Uri;
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
