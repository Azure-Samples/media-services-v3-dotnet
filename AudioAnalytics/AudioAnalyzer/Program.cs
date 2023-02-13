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
const string CustomTransform = "MyAudioAnalyzerTransform_2";
const string InputMP4FileName = "ignite.mp4";

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
string inputAssetName = $"input-{uniqueness}";
string outputAssetName = $"output-{uniqueness}";

// Ensure that you have customized encoding Transform. This is a one-time setup operation.
var transform = await CreateTransformAsync(mediaServicesAccount, CustomTransform);

// Create a new input Asset and upload the specified local video file into it.
var inputAsset = await CreateInputAssetAsync(mediaServicesAccount, inputAssetName, InputMP4FileName);

// Output from the Job must be written to an Asset, so let's create one.
var outputAsset = await CreateOutputAssetAsync(mediaServicesAccount, outputAssetName);

// A preset override can change the language or mode for a Job. Above we created a Transform with a
// preset that was set to a specific audio language and mode. If we want to change that language or
// mode before submitting the job, we can modify it using the PresetOverride property on the
// JobOutput.
var presetOverride = new AudioAnalyzerPreset
{
    AudioLanguage = "en-US",
    Mode = AudioAnalysisMode.Basic // Switch this job to use Basic mode instead of standard
};

var job = await SubmitJobAsync(transform, jobName, inputAsset, outputAsset, presetOverride);

Console.WriteLine("Polling Job status...");
job = await WaitForJobToFinishAsync(job);

if (job.Data.State == MediaJobState.Error)
{
    Console.WriteLine($"ERROR: Job finished with error message: {job.Data.Outputs[0].Error.Message}");
    Console.WriteLine($"ERROR:                   error details: {job.Data.Outputs[0].Error.Details[0].Message}");
    await CleanUpAsync(transform, job, inputAsset, outputAsset);
    return;
}

Console.WriteLine("Job finished.");
Directory.CreateDirectory(OutputFolder);

await DownloadResultsAsync(outputAsset, OutputFolder);

await CleanUpAsync(transform, job, inputAsset, outputAsset);

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

    var transformData = new MediaTransformData
    {
        Outputs =
            {
                // Create an AudioAnalyzer preset with audio insights and Basic audio mode.
                new MediaTransformOutput(
                    preset: new AudioAnalyzerPreset
                    {
                        AudioLanguage = "en-US",
                       
                        //
                        // There are two modes available, Basic and Standard
                        // Basic : This mode performs speech-to-text transcription and generation of a VTT subtitle/caption file. 
                        //         The output of this mode includes an Insights JSON file including only the keywords, transcription,and timing information. 
                        //         Automatic language detection and speaker diarization are not included in this mode.
                        // Standard : Performs all operations included in the Basic mode, additionally performing language detection and speaker diarization.
                        //
                        Mode = AudioAnalysisMode.Standard
                    })
                {
                    OnError = MediaTransformOnErrorType.StopProcessingJob,
                    RelativePriority = MediaJobPriority.Normal
                }
            }
    };

    // Create the custom Transform with the outputs defined above
    // Does a Transform already exist with the desired name? This method will just overwrite (Update) the Transform if it exists already. 
    // In production code, you may want to be cautious about that. It really depends on your scenario.
    var transform = await mediaServicesAccount.GetMediaTransforms().CreateOrUpdateAsync(
        WaitUntil.Completed,
        transformName,
        transformData);

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
/// <param name="inputAsset">The input Asset.</param>
/// <param name="outputAsset">The output Asset that will store the result of the encoding Job.</param>
/// <param name="presetOverride">Options to override the preset settings.</param>
static async Task<MediaJobResource> SubmitJobAsync(
    MediaTransformResource transform,
    string jobName,
    MediaAssetResource inputAsset,
    MediaAssetResource outputAsset,
    MediaTransformPreset presetOverride)
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
                {
                    PresetOverride = presetOverride
                }
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
    var container = new BlobContainerClient(sasUri);
    BlobClient blob = container.GetBlobClient(Path.GetFileName(fileToUpload));

    // Use Storage API to upload the file into the container in storage.
    Console.WriteLine("Uploading a media file to the Asset...");
    await blob.UploadAsync(fileToUpload);

    return asset;
}

/// <summary>
/// Downloads the specified output Asset.
/// </summary>
/// <param name="asset">The Asset to download from.</param>
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
/// Delete the resources that were created.
/// </summary>
/// <param name="transform">The transform.</param>
/// <param name="job">The Job.</param>
/// <param name="inputAsset">The input Asset.</param>
/// <param name="outputAsset">The output Asset.</param>
/// <returns>A task.</returns>
static async Task CleanUpAsync(
    MediaTransformResource transform,
    MediaJobResource job,
    MediaAssetResource? inputAsset,
    MediaAssetResource outputAsset)
{
    await job.DeleteAsync(WaitUntil.Completed);
    await transform.DeleteAsync(WaitUntil.Completed);

    if (inputAsset != null)
    {
        await inputAsset.DeleteAsync(WaitUntil.Completed);
    }

    await outputAsset.DeleteAsync(WaitUntil.Completed);
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
