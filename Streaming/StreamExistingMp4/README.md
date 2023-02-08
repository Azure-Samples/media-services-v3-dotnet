# Streaming an existing single bitrate MP4 file with HLS or Dash

This sample demonstrates how to dynamically package VOD content from an existing pre-encoded MP4 file (or set of ABR encoded Mp4s) and stream with HLS/DASH. 
It shows how you can create the required server manifest (.ism) and client manifest (.ismc) files needed for the Azure Media Services origin server to stream a pre-encoded file to HLS or DASH formats. 
Once the server and client manifest are available, the streaming server understands how to dynamically generate HLS and DASH format.  
Keep in mind that this can be slightly less performant and heavier on the CPU of the Streaming Endpoint than generating the content directly through the Standard encoder presets for adaptive streaming (AdaptiveStreaming or Content Aware Encoding presets)
due to the fact that the .mpi index binary files are not included.  When using this method, be sure to check with the AMS team when you are considering scaling the solution up to production level usage. 

It performs the following tasks:
1. Uploads an existing Mp4 file that is encoded with 2-second GOPs, CBR rate control, max 2 second key frame and min 2 second key frame distances. 
1. Creates a .ism manifest to point to the files uploaded and saves the .ism (server manifest) file back to the Asset container
1. Generates a request to create the .ismc file (client manifest) and saves it back into the Asset container.
1. Publishes output asset for HLS and DASH streaming.
1. Provides the HLS manifest URL for use in iOS, or any HLS supported player framework (i.e. Shaka, Hls.js, Video.js, Theo Player, Bitmovin, etc.)
1. Provides the DASH link and a clickable URL to launch the Azure Media Player with the DASH manifest using the "QuickStart" heuristics mode.

This sample only shows how to upload a single bitrate, but this sample can easily be extended to show how to upload a full adaptive bitrate (ABR) set of Mp4 files for streaming in Azure Media Services.

## Prerequisites

* Required Assemblies

* Azure.Identity
* Azure.ResourceManager.Media
* Azure.Storage.Blobs
* System.Linq.Async

* An Azure Media Services account. See the steps described in [Create a Media Services account](https://learn.microsoft.com/azure/media-services/latest/account-create-how-to).

## Build and run

Update the settings in **appsetting.json** in the root folder of the repository to match your Azure subscription, resource group and Media Services account.
Then build and run the sample in Visual Studio or VS Code.

## Key concepts

* [Dynamic packaging](https://learn.microsoft.com/azure/media-services/latest/dynamic-packaging-overview)
* [Streaming Policies](https://learn.microsoft.com/azure/media-services/latest/streaming-policy-concept)

## Next steps

* [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
* [Azure Media Services v3 Documentation](https://learn.microsoft.com/azure/media-services/latest/)
