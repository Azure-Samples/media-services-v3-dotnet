# Manage, create, list, and modify assets in a Media Services account

This sample demonstrates how to do common asset management with Media Services asset resource in a specific region using the SDK.

1. List assets
1. Create a new asset and provide alternate identifiers, descriptions, and storage container
1. List and filter assets using OData

> [!TIP]
> Use interactive login in this sample with an account that has subscription level write access to the 'Microsoft.Media/mediaservices/write' path.

## Prerequisites

Required Assemblies:

* Azure.Identity
* Azure.ResourceManager.Media
* System.Linq.Async

## Build and run

Update the settings in **appsettings.json** in the root folder of the repository to match your Azure subscription, resource group and Media Services account.

The sample will authenticate using any of the methods supported by [`DefaultAzureCredential`](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet).

## Next steps

* [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
* [Azure Media Services v3 Documentation](https://learn.microsoft.com/azure/media-services/latest/)
