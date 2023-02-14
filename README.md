---
page_type: sample
languages:
  - csharp
products:
  - azure
  - azure-media-services
description: "The samples in this repo show how to encode, package, and protect with Azure Media Services v3 using .NET 7.0 SDK. You also learn how to perform live ingest in order to broadcast your events."  
---
 
# Azure Media Services v3 samples using .NET 7.0

This project is part of the Azure Media Services API samples. For more information and links to more samples, see [Azure Media Services](https://media.microsoft.com).

The samples in this repo show how to encode, package, live stream, and protect your videos with Azure Media Services v3 using [.NET 7.0 SDK](https://dotnet.microsoft.com/download).

To install the latest version of the Azure.ResourceManager.Media client SDK, go to the Nuget package [Azure.ResourceManager.Media](https://www.nuget.org/packages/Azure.ResourceManager.Media).

## .NET solution files and how to launch projects

Open the root /Media-services-v3-dotnet.sln (or just open the folder in VS Code).
The main solution contains all of the projects.  You can select the active project to launch in Visual Studio.

When using VS Code, select the project you wish to Launch in the Debugger console (Ctrl-shift-D).

## Contents

| Folder | Description |
|-------------|-------------|
| **Account** ||
| [Account/CreateAccount](/Account/CreateAccount)|The sample shows how to create a Media Services account and set the primary storage account, in addition to advanced configuration settings including Key Delivery IP allowlist, Managed Identity, storage auth, and bring your own encryption key.|
| [Account/GetAccounts](/Account/GetAccounts)|The sample shows how to list all Media Services accounts in a resource group and select one by its name.|
| [Account/Quotas](/Account/Quotas)|The sample shows how to get account quotas.|
| **Assets** ||
| [Assets/AssetManagement](/Assets/AssetManagement)|The sample shows how to manage assets in Media Services. Demonstrates how to create assets, set the container name, alternate identification, as well as list assets using paging and oData filters.|
| **Analytics** ||
| [AudioAnalytics/AudioAnalyzer](/AudioAnalytics/AudioAnalyzer)|This sample illustrates how to create a audio analyzer transform, upload a media file to an input asset, submit a job with the transform and download the results for verification.|
| **Encoding** ||
| [VideoEncoding/Encoding_PredefinedPreset](/VideoEncoding/Encoding_PredefinedPreset)|The sample shows how to submit a job using a built-in preset and an HTTP URL input, publish output asset for streaming, and download results for verification.|
| [VideoEncoding/Encoding_H264](/VideoEncoding/Encoding_H264)|The sample shows how to submit a job using a custom H.264 encoding preset and an HTTP URL input, publish output asset for streaming, and download results for verification.|
| [VideoEncoding/Encoding_H264_ConstantRateFactor](/VideoEncoding/Encoding_H264_ConstantRateFactor)|The sample shows how to create a custom encoding Transform using custom H.264 Constant Rate Factor (CRF) encoding settings.|
| [VideoEncoding/Encoding_H264_ContentAware](/VideoEncoding/Encoding_H264_ContentAware)|The sample shows how to submit a job using a the built in Content Aware encoding preset for H.264. For details on the Content Aware Encoding preset, see the article - [Content Aware Encoding](https://learn.microsoft.com/azure/media-services/latest/encode-content-aware-concept).|
| [VideoEncoding/Encoding_H264_ContentAware_Constrained](/VideoEncoding/Encoding_H264_ContentAware_Constrained)|The sample shows how to submit a job using a custom constrained version of the Content Aware encoding preset for H.264. This is useful for situations where you need to control the behavior of the CAE encoder by reducing the number of outputs or bitrates into a defined range. Using the PresetConfigurations class, you can control the output for the CAE presets. For details on the Content Aware Encoding preset, see the article - [Content Aware Encoding](https://learn.microsoft.com/azure/media-services/latest/encode-content-aware-concept).|
| [VideoEncoding/Encoding_HEVC](/VideoEncoding/Encoding_HEVC)|The sample shows how to submit a job using a custom HEVC encoding preset and an HTTP URL input, publish output asset for streaming, and download results for verification.|
| [VideoEncoding/Encoding_HEVC_ContentAware](/VideoEncoding/Encoding_HEVC_ContentAware)|The sample shows how to submit a job using a the built in Content Aware encoding preset for HEVC(H.265). Using the PresetConfigurations class, you can control the output for the CAE presets. For details on the Content Aware Encoding preset, see the article - [Content Aware Encoding](https://learn.microsoft.com/azure/media-services/latest/encode-content-aware-concept).|
| [VideoEncoding/Encoding_StitchTwoAssets](/VideoEncoding/Encoding_StitchTwoAssets)|The sample shows how to submit a job using the JobInputSequence to stitch together 2 or more assets that may be clipped by start or end time. The resulting encoded file is a single video with all assets stitched together.  The sample will also  publish output asset for streaming and download results for verification.|
| [VideoEncoding/Encoding_SpriteThumbnail](/VideoEncoding/Encoding_SpriteThumbnail)|The sample shows how to submit a job using a custom preset with a thumbnail sprite and an HTTP URL input, publish output asset for streaming, and download results for verification.|
| [VideoEncoding/Encoding_OverlayImage](/VideoEncoding/Encoding_OverlayImage)|The sample shows how to overlay an image logo or watermark on a video, publish output asset for streaming, and download results for verification.|
| [VideoEncoding/Encoding_MultiChannelAudio](/VideoEncoding/Encoding_MultiChannelAudio)|The sample demonstrates how to create an encoding Transform that uses channel mapping and audio track selection from the input source to output two new AAC audio tracks.|
| **Live Streaming** ||
| [Live/LiveEventWithDVR](/Live/LiveEventWithDVR)|This sample first shows how to create a LiveEvent with a full archive up to 25 hours and an filter on the asset with 5 minutes DVR window, then it shows how to use the filter to create a locator for streaming.|
| **VOD Streaming** ||
| [Streaming/AssetFilters](/Streaming/AssetFilters)|This sample demonstrates how to create a transform with built-in AdaptiveStreaming preset, submit a job, create an asset-filter and an account-filter, associate the filters to streaming locators and print urls for playback.|
| [Streaming/StreamHLSAndDASH](/Streaming/StreamHLSAndDASH)|This sample demonstrates how to create a transform with built-in AdaptiveStreaming preset, submit a job, publish output asset for HLS and DASH streaming.|
| [Streaming/StreamExistingMp4](/Streaming/StreamExistingMp4)|This sample demonstrates how upload and stream from an existing, pre-encoded MP4 file.  This can be useful if you have content encoded in FFMPEG or an external encoder tool or service that you would like to upload and stream from Media Services.  This sample shows the process for uploading the MP4 file (single bitrate), the code for generating the server manifest (.ism) as well as the client manifest (.ismc) that are required for streaming with Dynamic Packaging from the Streaming Endpoint (origin server)|
| **Content protection** ||
| [ContentProtection/BasicAESClearKey](/ContentProtection/BasicAESClearKey)|This sample demonstrates how to create a transform with built-in AdaptiveStreaming preset, submit a job, create a ContentKeyPolicy using a secret key, associate the ContentKeyPolicy with StreamingLocator, get a token and print a url for playback in Azure Media Player. When a stream is requested by a player, Media Services uses the specified key to dynamically encrypt your content with AES-128 and Azure Media Player uses the token to decrypt.|
| [ContentProtection/BasicPlayReady](/ContentProtection/BasicPlayReady)|This sample demonstrates how to create a transform with built-in AdaptiveStreaming preset, submit a job, create a ContentKeyPolicy with PlayReady configuration using a secret key, associate the ContentKeyPolicy with StreamingLocator, get a token and print a url for playback in a Azure Media Player on Microsoft Edge. When a user requests PlayReady-protected content, the player application requests a license from the Media Services license service. If the player application is authorized, the Media Services license service issues a license to the player. A PlayReady license contains the decryption key that can be used by the client player to decrypt and stream the content.|
| [ContentProtection/BasicWidevine](/ContentProtection/BasicWidevine)|This sample demonstrates how to create a transform with built-in AdaptiveStreaming preset, submit a job, create a ContentKeyPolicy with Widevine configuration using a secret key, associate the ContentKeyPolicy with StreamingLocator, get a token and print a url for playback in a Azure Media Player on Google Chrome. When a user requests Widevine-protected content, the player application requests a license from the Media Services license service. If the player application is authorized, the Media Services license service issues a license to the player. A Widevine license contains the decryption key that can be used by the client player to decrypt and stream the content.|
| [ContentProtection/OfflineFairPlay](/ContentProtection/OfflineFairPlay)|This sample demonstrates how to dynamically encrypt your content with Apple FairPlay DRM and play the content without requesting each time a new license from license service. It shows how to create a transform with built-in AdaptiveStreaming preset, submit a job, create a ContentKeyPolicy with open restriction and FairPlay persistent configuration, associate the ContentKeyPolicy with a StreamingLocator and print a url for playback.|When a user plays the first time the FairPlay-protected content, the player application requests a license from the Media Services license service. The Media Services license service issues a license to the player and the license is persisted. Because the license is persisted, subsequent playback won't send a request to Media Services license service again.
| [ContentProtection/OfflinePlayReadyAndWidevine](/ContentProtection/OfflinePlayReadyAndWidevine)|This sample demonstrates how to dynamically encrypt your content with PlayReady and Widevine DRM and play the content without requesting each time a new license from license service. It shows how to create a transform with built-in AdaptiveStreaming preset, submit a job, create a ContentKeyPolicy with a token restriction and PlayReady/Widevine persistent configuration, associate the ContentKeyPolicy with a StreamingLocator, get a token and print a url for playback. When a user plays the first time the DRM-protected content, the player application requests a license from the Media Services license service. If the player application is authorized, the Media Services license service issues a license to the player and the license is persisted. Because the license is persisted, subsequent playback won't send a request to Media Services license service again.|

## Prerequisites

- A Windows 10/11 PC, Mac or Linux
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (Windows 10/11), or optional [Visual Studio Code](https://code.visualstudio.com/) (Windows, Mac or Linux)
- .NET 7.0 SDK : <https://dotnet.microsoft.com/download>

## Setup

- Clone or download this sample repository.
- Open the solution file media-services-v3-dotnet.sln in Visual Studio 2022, or open the folder in Visual Studio Code.
- Update the settings in **appsettings.json** in the root folder of the repository to match your Azure subscription, resource group and Media Services account.
- Read sample's README.md to see what key concepts to review and how to set up and run the sample.
- Select the project to Run in the Debugger (ctrl-shift-D in VS Code on Window) and press F5 to launch

## Troubleshooting and known issues

| Issue or Error message | Notes |
|-------------|-----------|
|"Encountered error while fetching the list of EventHub PartitionIds" | Make sure that you are using the SAS policy for the EventHub and not the EventHub namespace. Also make sure that your .env or appsettings.json file is pointing to the name of the EventHub (instance) and not the EventHub namespace (which is higher level entity in the portal).|

## See also

- Nodejs samples : <https://github.com/Azure-Samples/media-services-v3-node-tutorials>
- Java samples: <https://github.com/Azure-Samples/media-services-v3-java>
- Python samples: <https://github.com/Azure-Samples/media-services-v3-python>

## Next steps

- Azure Media Services documentation: <https://learn.microsoft.com/azure/media-services/>
- Azure Media Services pricing: <https://azure.microsoft.com/pricing/details/media-services/>
