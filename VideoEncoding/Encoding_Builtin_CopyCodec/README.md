# Use of built-in Copy Codec preset

This sample shows how to use the built-in Copy codec preset that can take a source video file that is already encoded using H264 and AAC audio, and copy it into MP4 tracks that are ready to be streamed by Azure Media Services.
This is useful for scenarios where you have complete control over the source asset, and can encode it in a way that is consistent with streaming (2-6 second GOP length, Constant Bitrate CBR encoding, no or limited B frames).
This preset should be capable of converting a source 1 hour video into a streaming MP4 format in under 1 minute, as it is not doing any encoding - just re-packaging the content into MP4 files.
This preset works up to 4K and 60fps content.

NOTE: If the input has any B frames encoded, we occasionally can get the GOP boundaries that are off by 1 tick which can cause some issues with adaptive switching.

The sample shows how to perform the following tasks:

1. Creates a custom transform using CopyCodec preset
1. Creates an input asset and upload a media file into it.
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
Then build and run the sample in Visual Studio or VS Code.

## Next steps

* [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
* [Azure Media Services v3 Documentation](https://learn.microsoft.com/azure/media-services/latest/)
