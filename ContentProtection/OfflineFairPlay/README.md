---
topic: sample
languages:
  - c#
products:
  - azure-media-services
---

# Offline playback with FairPlay DRM

This sample demonstrates how to dynamically encrypt your content with FairPlay DRM and play the content without requesting a license from license service. It shows how to perform the following tasks:

1. Creates a transform with built-in AdaptiveStreaming preset
1. Submits a job
1. Creates a ContentKeyPolicy with open restriction and FairPlay persistent configuration
1. Creates a custom StreamingPolicy
1. Associates the ContentKeyPolicy and the StreamingPolicy with a StreamingLocator
1. Prints a url for playback

When a user requests FairPlay protected content for the first time, the player application requests a license from the Media Services license service. If the player application is authorized, the Media Services license service issues a license to the player and the license is persisted. Because the license is persisted, subsequent playback won't send a request to license service again.

> [!TIP]
> The `Program.cs` file (in the `BasicWidevine` folder) has extensive comments.

## Prerequisites

* Required Assemblies

* Azure.Identity
* Azure.ResourceManager.Media
* Microsoft.Extensions.Hosting
* Newtonsoft.Json

* An Azure Media Services account. See the steps described in [Create a Media Services account](https://learn.microsoft.com/azure/media-services/latest/account-create-how-to).

* An Apple ASK (Application Secret Key).
* An Apple certificate(.pfx) and password.

## Build and run

Update the settings in **appsetting.json** in the root folder to match your Azure subscription, resource group and Media Services account.
Then build and run the sample in Visual Studio or VS Code.

The sample will authenticate using any of the methods supported by [`DefaultAzureCredential`](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet).

## Key concepts

* [Dynamic packaging](https://learn.microsoft.com/azure/media-services/latest/encode-dynamic-packaging-concept)
* [Content protection with dynamic encryption](https://learn.microsoft.com/azure/media-services/latest/drm-content-protection-concept)
* [Streaming Policies](https://learn.microsoft.com/azure/media-services/latest/stream-streaming-policy-concept)

## Next steps

* [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
* [Azure Media Services v3 Documentation](* [Streaming Policies](https://learn.microsoft.com/azure/media-services/latest/stream-streaming-policy-concept)
