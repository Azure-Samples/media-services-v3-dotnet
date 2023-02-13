# Encode with a custom Transform and overlay an image onto the video

This sample shows how to overlay an image onto video using a custom encoding Transform settings. It shows how to perform the following tasks:

1. Creates a custom encoding transform (with image overlay configured using a PNG file).
1. Creates an input asset and upload a media file into it.
1. Submits a job and monitoring the job using polling method or Event Grid events.
1. Downloads the output asset.
1. Prints URLs for streaming.

See the article [Create an overlay Transform](https://learn.microsoft.com/azure/media-services/latest/transform-create-overlay-how-to) for details.

## Prerequisites

Required Assemblies:

* Azure.Identity
* Azure.ResourceManager.Media
* Azure.Storage.Blobs
* System.Linq.Async

An Azure Media Services account. See the steps described in [Create a Media Services account](https://learn.microsoft.com/azure/media-services/latest/account-create-how-to).

## Build and run

Update the settings in **appsettings.json** in the root folder of the repository to match your Azure subscription, resource group and Media Services account.
Then build and run the sample in Visual Studio or VS Code.

## Next steps

* [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
* [Azure Media Services v3 Documentation](https://learn.microsoft.com/azure/media-services/latest/)
