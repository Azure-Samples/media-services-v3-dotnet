# Encode with a custom Transform HEVC

This sample shows how to create a custom encoding Transform using HEVC encoding settings. It shows how to perform the following tasks:

1. Creates a custom encoding transform using HEVC
1. Creates an input asset and upload a media file into it.
1. Submits a job and monitoring the job using polling method.
1. Downloads the output asset.
1. Prints URLs for streaming.

> [!TIP]
> The `Program.cs` file has extensive comments.

## Prerequisites

* Required Assemblies

* Azure.Identity
* Azure.ResourceManager.Media
* Azure.Storage.Blobs
* System.Linq.Async

* An Azure Media Services account. See the steps described in [Create a Media Services account](https://docs.microsoft.com/en-us/azure/media-services/latest/account-create-how-to).

## Build and run

Update the settings in **appsetting.json**. Then build and run the sample in Visual Studio or VS Code.

## Next steps

* [Streaming videos](https://docs.microsoft.com/en-us/azure/media-services/latest/stream-files-tutorial-with-api)
* [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
* [Azure Media Services v3 Documentation](https://docs.microsoft.com/azure/media-services/latest/)
