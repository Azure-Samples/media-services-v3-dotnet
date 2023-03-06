# Copy a live archive to MP4 file format

This sample demonstrates how to use the archived output from a live event and extract only the top highest bitrate video track to be packaged into an MP4 file for export to social media platforms, or for use with Video Indexer.
The key concept in this sample is the use of an input definition on the Job InputAsset to specify a VideoTrackDescriptor.
The SelectVideoTrackByAttribute allows you to select a single track from the live archive by using the bitrate attribute, and filtering by the "Top" video bitrate track in the live archive.

Sample shows how to perform the following tasks:

1. Creates a custom encoding transform with copy codec
1. From an existing live archive, creates a job input asset and select top bitrate (with optional time trimming).
1. Submits a job and monitoring the job using polling method.
1. Downloads the output asset.
1. Prints URLs for streaming.

## Prerequisites

Required Assemblies:

* Azure.Identity
* Azure.ResourceManager.Media
* Azure.Storage.Blobs
* System.Linq.Async

An Azure Media Services account. See the steps described in [Create a Media Services account](https://learn.microsoft.com/azure/media-services/latest/account-create-how-to).

## Build and run

Update the settings in **appsettings.json** in the root folder of the repository to match your Azure subscription, resource group and Media Services account.

In Program.cs, change the following line to provide the name of your live archive asset :

```csharp
const string InputArchiveName = "archiveAsset-3009"; 
```

Then build and run the sample in Visual Studio or VS Code.

## Next steps

* [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
* [Azure Media Services v3 Documentation](https://learn.microsoft.com/azure/media-services/latest/)
