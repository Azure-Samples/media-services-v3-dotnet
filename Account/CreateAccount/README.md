# Create a Media Services account

The sample shows how to create a Media Services account and set the primary storage account, in addition to advanced configuration settings including Key Delivery IP allowlist, Managed Identity, storage auth, and bring your own encryption key.

## Prerequisites

Required Assemblies:

* Azure.Core
* Azure.Identity
* Azure.ResourceManager.Media
* Microsoft.Extensions.Hosting

## Build and run

Update the settings in **appsettings.json** in the root folder of the repository to match your Azure subscription, storage resource group name and storage account name.
Then build and run the sample in Visual Studio or VS Code.

The sample will authenticate using any of the methods supported by [`DefaultAzureCredential`](https://learn.microsoft.com/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet).

## Next steps

* [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
* [Azure Media Services v3 Documentation](https://learn.microsoft.com/azure/media-services/latest/)
