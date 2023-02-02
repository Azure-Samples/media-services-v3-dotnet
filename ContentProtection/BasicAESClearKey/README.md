---
topic: sample
languages:
  - c#
products:
  - azure-media-services
---

# Dynamically encrypt your content with AES-128

This sample demonstrates how to dynamically encrypt your content with AES-128. It shows how to perform the following tasks:

1. Creates a transform with built-in AdaptiveStreaming preset
1. Submits a job
1. Creates a ContentKeyPolicy using a secret key
1. Associates the ContentKeyPolicy with StreamingLocator
1. Gets a token and print a url for playback

When a stream is requested by a player, Media Services uses the specified key to dynamically encrypt your content with AES-128 and Azure Media Player uses the token to decrypt.

> [!TIP]
> The `Program.cs` file (in the `BasicAESClearKey` folder) has extensive comments.

## Prerequisites

* Required Assemblies

* Azure.Identity
* Azure.ResourceManager.Media
* Azure.Storage.Blobs
* Microsoft.Extensions.Hosting
* Microsoft.Identity.Client
* Microsoft.IdentityModel.Tokens
* System.IdentityModel.Tokens.Jwt
* System.Linq.Async
* System.Security.Claims

* An Azure Media Services account. See the steps described in [Create a Media Services account](https://docs.microsoft.com/en-us/azure/media-services/latest/account-create-how-to).

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
