# Use of built-in Copy Codec preset with fast proxy MP4

This sample shows how to use the built-in Copy codec preset that can take a source video file that is already encoded using H264 and AAC audio, and copy it into MP4 tracks that are ready to be streamed by Azure Media Services.
In addition, this preset generates a fast proxy MP4 from the source video.
This is very helpful for scenarios where you want to make the uploaded MP4 asset available quickly for streaming, but also generate a low quality proxy version of the asset for quick preview, video thumbnails, or low bitrate delivery while your application logic decides if you need to backfill any more additional layers (540P, 360P, etc) to make the full adaptive bitrate set complete.
This strategy is commonly used by services like YouTube to make content appear to be "instantly" available, but slowly fill in the quality levels for a more complete adaptive streaming experience.
See the Encoding_BuiltIn_CopyCodec sample for a version that does not generate the additional proxy layer.

This is useful for scenarios where you have complete control over the source asset, and can encode it in a way that is consistent with streaming (2-6 second GOP length, Constant Bitrate CBR encoding, no or limited B frames).
This preset should be capable of converting a source 1 hour video into a streaming MP4 format in under 1 minute, as it is not doing any encoding - just re-packaging the content into MP4 files.
This preset works up to 4K and 60fps content.

NOTE: If the input has any B frames encoded, we occasionally can get the GOP boundaries that are off by 1 tick which can cause some issues with adaptive switching.

The sample shows how to perform the following tasks:

1. Creates a custom transform using CopyCodec preset with fast proxy MP4
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
