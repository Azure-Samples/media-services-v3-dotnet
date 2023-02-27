# Offline playback with PlayReady and Widevine DRM

This sample demonstrates how to dynamically encrypt your content with PlayReady and Widevine DRM and play the content without requesting a license from license service. It shows how to perform the following tasks:

1. Creates a transform with built-in AdaptiveStreaming preset
1. Submits a job
1. Creates a ContentKeyPolicy with a token restriction and PlayReady/Widevine persistent configuration
1. Associates the ContentKeyPolicy with a StreamingLocator
1. Prints a url for playback which includes a token

When a user requests PlayReady or Widevine protected content for the first time, the player application requests a license from the Media Services license service. If the player application is authorized, the Media Services license service issues a license to the player and the license is persisted. Because the license is persisted, subsequent playback won't send a request to Media Services license service again.

## Prerequisites

Required Assemblies:

* Azure.Identity
* Azure.ResourceManager.Media
* Microsoft.Extensions.Hosting
* Microsoft.IdentityModel.Tokens
* Newtonsoft.Json
* System.IdentityModel.Tokens.Jwt
* System.Security.Claims

An Azure Media Services account. See the steps described in [Create a Media Services account](https://learn.microsoft.com/azure/media-services/latest/account-create-how-to).

## Build and run

Update the settings in **appsettings.json** in the root folder to match your Azure subscription, resource group and Media Services account.
Then build and run the sample in Visual Studio or VS Code.

The sample will authenticate using any of the methods supported by [`DefaultAzureCredential`](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet).

## Key concepts

* [Dynamic packaging](https://learn.microsoft.com/azure/media-services/latest/encode-dynamic-packaging-concept)
* [Content protection with dynamic encryption](https://learn.microsoft.com/azure/media-services/latest/drm-content-protection-concept)
* [Streaming Policies](https://learn.microsoft.com/azure/media-services/latest/stream-streaming-policy-concept)

## Next steps

* [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
* [Azure Media Services v3 Documentation](https://learn.microsoft.com/azure/media-services/latest/)
