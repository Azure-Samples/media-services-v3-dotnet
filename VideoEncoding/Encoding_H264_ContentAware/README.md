# Encode with Content Aware Encoding Preset for H.264

This sample shows how to create a built-in Content Aware Encoding preset with settings. It shows how to perform the following tasks:

The content-aware encoding preset extends the "adaptive bitrate streaming" mechanism, by incorporating custom logic that lets the encoder seek the optimal bitrate value for a given resolution, 
but without requiring extensive computational analysis. This preset produces a set of GOP-aligned MP4s. Given any input content, the service performs an initial lightweight analysis of the 
input content, and uses the results to determine the optimal number of layers, appropriate bitrate and resolution settings for delivery by adaptive streaming. 
This preset is particularly effective for low and medium complexity videos, where the output files will be at lower bitrates than the Adaptive Streaming preset but at a quality that still 
delivers a good experience to viewers. The output will contain MP4 files with video and audio interleaved.

To learn more about Content Aware Encoding, see the article on [Content Aware Encoding](https://docs.microsoft.com/azure/media-services/latest/encode-content-aware-concept)

1. Creates a Content Aware Encoding transform for H.264 with settings.
1. Creates an input asset and upload a media file into it.
1. Submits a job and monitoring the job using polling method.
1. Downloads the output asset.
1. Prints URLs for streaming.

## Prerequisites

* Required Assemblies

* Azure.Identity
* Azure.ResourceManager.Media
* Azure.Storage.Blobs
* System.Linq.Async

* An Azure Media Services account. See the steps described in [Create a Media Services account](https://docs.microsoft.com/en-us/azure/media-services/latest/account-create-how-to).

## Build and run

Update the settings in **appsetting.json** in the root folder of the repository to match your Azure subscription, resource group and Media Services account.
Then build and run the sample in Visual Studio or VS Code.

## Next steps

* [Streaming videos](https://docs.microsoft.com/en-us/azure/media-services/latest/stream-files-tutorial-with-api)
* [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
* [Azure Media Services v3 Documentation](https://docs.microsoft.com/azure/media-services/latest/)
