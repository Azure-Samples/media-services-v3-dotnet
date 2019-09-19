---
page_type: sample
languages:
  - csharp
products:
  - azure
  - azure-media-services
description: "The samples in this repo show how to encode, package, protect, analyze your videos with Azure Media Services v3 using .NET SDK. You also learn how to perform live ingest in order to broadcast your events."  
---
 
# Azure Media Services v3 samples using .NET

The samples in this repo show how to encode, package, protect your videos with Azure Media Services v3 using .NET SDK. The repo also contains samples that demonstrate how to analyze videos and perform live ingest in order to broadcast your events.  

## Contents

| Folder | Description |
|-------------|-------------|
| VideoEncoding/EncodingWithMESPredefinedPreset|The sample shows how to submit a job using a built-in preset and an HTTP URL input, publish output asset for streaming, and download results for verification.|
| VideoEncoding/EncodingWithMESCustomPreset|The sample shows how to submit a job using a custom preset and an HTTP URL input, publish output asset for streaming, and download results for verification.|
| LiveIngest/LiveEventWithDVR|This sample first shows how to create a LiveEvent with a full archive up to 25 hours and an filter on the asset with 5 minutes DVR window, then it shows how to use the filter to create a locator for streaming.|
| VideoAnalytics/VideoAnalyzer|This sample illustrates how to create a video analyzer transform, upload a video file to an input asset, submit a job with the transform and download the results for verification.|
| AudioAnalytics/AudioAnalyzer|This sample illustrates how to create a audio analyzer transform, upload a media file to an input asset, submit a job with the transform and download the results for verification.|
| ContentProtection/BasicAESClearKey|This sample demonstrates how to create a transform with built-in AdaptiveStreaming preset, submit a job, create a ContentKeyPolicy using a secret key, associate the ContentKeyPolicy with StreamingLocator, get a token and print a url for playback in Azure Media Player. When a stream is requested by a player, Media Services uses the specified key to dynamically encrypt your content with AES-128 and Azure Media Player uses the token to decrypt.|
| ContentProtection/BasicWidevine|This sample demonstrates how to create a transform with built-in AdaptiveStreaming preset, submit a job, create a ContentKeyPolicy with Widevine configuration using a secret key, associate the ContentKeyPolicy with StreamingLocator, get a token and print a url for playback in a Widevine Player. When a user requests Widevine-protected content, the player application requests a license from the Media Services license service. If the player application is authorized, the Media Services license service issues a license to the player. A Widevine license contains the decryption key that can be used by the client player to decrypt and stream the content.|
| ContentProtection/BasicPlayReady|This sample demonstrates how to create a transform with built-in AdaptiveStreaming preset, submit a job, create a ContentKeyPolicy with PlayReady configuration using a secret key, associate the ContentKeyPolicy with StreamingLocator, get a token and print a url for playback in a Azure Media Player. When a user requests PlayReady-protected content, the player application requests a license from the Media Services license service. If the player application is authorized, the Media Services license service issues a license to the player. A PlayReady license contains the decryption key that can be used by the client player to decrypt and stream the content.|
| ContentProtection/OfflinePlayReadyAndWidevine|This sample demonstrates how to dynamically encrypt your content with PlayReady and Widevine DRM and play the content without requesting a license from license service. It shows how to create a transform with built-in AdaptiveStreaming preset, submit a job, create a ContentKeyPolicy with open restriction and PlayReady/Widevine persistent configuration, associatethe ContentKeyPolicy with a StreamingLocator and print a url for playback.|
| DynamicPackagingVODContent/AssetFilters|This sample demonstrates how to create a transform with built-in AdaptiveStreaming preset, submit a job, create an asset-filter and an account-filter, associate the filters to streaming locators and print urls for playback.|
| DynamicPackagingVODContent/StreamHLSAndDASH|This sample demonstrates how to create a transform with built-in AdaptiveStreaming preset, submit a job, publish output asset for HLS and DASH streaming.|

## Prerequisites

- A Windows 10 PC.
- Visual Studio 2019 installed.

## Setup

- Clone or download this sample repository.
- Open the project file you are interested (for example, ContentProtection\BasicAESClearKey\BasicAESClearKey.csproj) in Visual Studio.
- Read sample's README.md to see what key concepts to review and how to set up and run the sample.

## See also

Java samples: https://github.com/Azure-Samples/media-services-v3-java

## Next steps

- Azure Media Services documentation: https://docs.microsoft.com/en-us/azure/media-services/
- Azure Media Services pricing: https://azure.microsoft.com/en-in/pricing/details/media-services/
