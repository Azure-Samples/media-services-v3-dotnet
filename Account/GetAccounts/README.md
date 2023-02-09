# Get Media Services accounts from a resource group

This sample shows how to list all Media Services accounts in a resource group and select one by its name.

## Prerequisites

Required Assemblies:

* Azure.Identity
* Azure.ResourceManager.Media
* Microsoft.Extensions.Hosting

## Build and run

Update the settings in **appsettings.json** in the root folder of the repository to match your Azure subscription, resource group and Media Services account.
Then build and run the sample in Visual Studio or VS Code.

The sample will authenticate using any of the methods supported by [`DefaultAzureCredential`](https://learn.microsoft.com/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet).

## Next steps

* [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
* [Azure Media Services v3 Documentation](https://learn.microsoft.com/azure/media-services/latest/)
