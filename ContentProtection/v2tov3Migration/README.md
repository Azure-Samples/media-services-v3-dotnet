---
topic: sample
languages:
  - c#
products:
  - azure-media-services
---

# v2 to v3 Migration for Content Protection

This sample demonstrates how to find your v2 assets and migrate them to v3. It does the following things:

1. Creates a published Asset in v2 using AES output encryption
1. Illustrates that the entities are visible in both V2 and V3 by querying the Asset, Locator, ContentKey, and policies in both API versions.
    1. It shows how to translate the V2 Asset identifier into the name of the V3 Asset.
    1. It shows how to find the other entities related to the Asset in both v2 and v3
1. How to unpublish and cleanup the content keys and policies in V2 so that you can replace it with V3 policies. This is not needed unless you want to change something about the publishing and then it is the recommended method.
1. How to publish the Asset again in v3

> [!TIP]
> The `Program.cs` file has extensive comments.

## Prerequisites

1. [Create an AMS account](https://docs.microsoft.com/azure/media-services/latest/account-create-how-to) (or use an existing one).  Make sure that the "Enable Classic APIs" checkbox is checked if creating a new one so that the V2 APIs are available.
1. Go to the API Access page in the portal.  [Setup a service principal](https://docs.microsoft.com/azure/media-services/latest/access-api-howto?tabs=portal) and select the v2 (classic) Media Services API version to get the settings.

* Required Assemblies

* System
* System.Collections.Generic
* System.IO
* System.Linq
* System.Security.Cryptography
* System.Threading
* Microsoft.WindowsAzure.MediaServices.Client (For the v2 client.)
* Microsoft.WindowsAzure.MediaServices.Client.ContentKeyAuthorization (For the v2 client.)
* Microsoft.WindowsAzure.MediaServices.Client.DynamicEncryption (For the v2 client.)
* Microsoft.Azure.Management.Media (For the v3 client.)
* Microsoft.Azure.Management.Media.Models (For v3 client models)
* Microsoft.IdentityModel.Clients.ActiveDirectory
* Microsoft.Rest.Azure.Authentication

## Build and run

Update **appsettings.json** in the project folder OR create a **.env file** at the root of the solution with your account settings. Please choose one of these two methods.
Then build and run the sample in Visual Studio or VS Code.

### appsettings.json

The settings for your account can be retrieved using the following Azure CLI command in the Media Services module. The following bash shell script creates a service principal for the account and returns the json settings.

```bash
    #!/bin/bash

    resourceGroup= <your resource group>
    amsAccountName= <your ams account name>
    amsSPName= <your AAD application>

    #Create a service principal with password and configure its access to an Azure Media Services account.
    az ams account sp create
    --account-name $amsAccountName` \\
    --name $amsSPName` \\
    --resource-group $resourceGroup` \\
    --role Owner` \\
    --years 2`
```

### .env

Use [sample.env](../../sample.env) as a template for the .env file to be created. The .env file must be placed at the root of the sample (same location than sample.env).
Connect to the Azure portal with your browser and go to your media services account / API access to get the .ENV data to store to the .env file.

## Next steps

* [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
* [Azure Media Services v3 Documentation](https://docs.microsoft.com/azure/media-services/latest/)
