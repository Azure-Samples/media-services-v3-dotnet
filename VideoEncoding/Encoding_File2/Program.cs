//-----------------------------------------------------------------------
// <copyright company="Microsoft">
//     Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Media;
using Azure.ResourceManager.Media.Models;
using Azure.Storage.Blobs;

var mediaFile = @"ignite.mp4";
var resourceId = "/subscriptions/628bddd1-d701-4273-8a5c-6ae0bd476c83/resourceGroups/jopayndiag/providers/Microsoft.Media/mediaservices/jopayndiagmedia4"; 

var userCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions { ExcludeManagedIdentityCredential = true });
var mediaServices = new ArmClient(userCredential)
    .GetMediaserviceResource(new ResourceIdentifier(resourceId));

var runPrefix = DateTime.UtcNow.Ticks.ToString("x8");

Console.WriteLine("Creating the input asset...");
var inputAsset = await mediaServices.GetAssets().CreateOrUpdateAsync(
    Azure.WaitUntil.Completed,
    runPrefix + "-in",
    new AssetData());

Console.WriteLine("Getting a SAS URL for the input asset...");
var inputAssetSas = await inputAsset.Value.GetContainerSasAsync(
    new ListContainerSasContent
    {
        ExpiryOn = DateTime.UtcNow.AddDays(1),
        Permissions = AssetContainerPermission.ReadWrite
    });

Console.WriteLine("Uploading a media file...");
var inputAssetContainer = new BlobContainerClient(new Uri(inputAssetSas.Value.AssetContainerSasUrls[0]));
using (var stream = new FileStream(mediaFile, FileMode.Open))
{
    await inputAssetContainer.UploadBlobAsync(Path.GetFileName(mediaFile), stream);
}

Console.WriteLine("Creating the output asset...");
var outputAsset = await mediaServices.GetAssets().CreateOrUpdateAsync(
    Azure.WaitUntil.Completed,
    runPrefix + "-out",
    new AssetData());

Console.WriteLine("Creating a transform...");
var transform = await mediaServices.GetTransforms().CreateOrUpdateAsync(
    Azure.WaitUntil.Completed,
    runPrefix + "-transform",
    new TransformData
    {
        Outputs =
        {
            new TransformOutput(new BuiltInStandardEncoderPreset(EncoderNamedPreset.H264MultipleBitrate720P))
            {
                OnError = OnErrorType.StopProcessingJob,
                RelativePriority = Priority.Normal
            }
        }
    });

Console.WriteLine("Creating a job...");
var job = (await transform.Value.GetJobs().CreateOrUpdateAsync(
    Azure.WaitUntil.Completed,
    runPrefix + "-job",
    new JobData
    {
        Input = new JobInputAsset(inputAsset.Value.Data.Name),
        Outputs =
        {
            new JobOutputAsset(outputAsset.Value.Data.Name)
        }
    })).Value;

while (job.Data.State == JobState.Processing || job.Data.State == JobState.Queued || job.Data.State == JobState.Scheduled)
{
    Console.WriteLine($"Job is {job.Data.State}");

    await Task.Delay(10000);

    job = await transform.Value.GetJobAsync(job.Data.Name);
}

if (job.Data.State != JobState.Finished)
{
    throw new Exception();
}

Console.WriteLine("Done");