---
topic: sample
languages:
  - csharp
products:
  - azure-media-services
---

# Create a Media Services account using the SDK

This sample demonstrates how to create and configure a Media Services account resource in a specific region using the SDK.

1. Create a Media Services client SDK object using Interactive login.  
1. Creates a Media Services object and populate the location, storage account, and Key Delivery IP allowList settings.
1. Check the availability of the name for the account in a region.
1. Create the account with the SDK.
1. Delete the account and cleanup


> [!TIP]
> The `Program.cs` file has extensive comments.

> [!TIP]
> Use interactive login in this sample with an account that has subscription level write access to the 'Microsoft.Media/mediaservices/write' path.

## Prerequisites

* Required Assemblies

* Azure.Storage.Blobs
* Microsoft.Azure.Management.Media
* Microsoft.Extensions.Configuration
* Microsoft.Extensions.Configuration.EnvironmentVariables
* Microsoft.Extensions.Configuration.Json
* Microsoft.Identity.Client


## Build and run

Update **appsettings.json** in the project folder OR create a **.env file** at the root of the solution with your account settings. Please choose one of these two methods.
For more information, see [Access APIs](https://docs.microsoft.com/en-us/azure/media-services/latest/access-api-howto).

(The default authentication is done using a Service Principal. It is possible to switch to interactive authentication by setting the boolean 'UseInteractiveAuth' to true in the sample. In that case, secret and app Id are not needed in the appsettings.json or .env file. The System browser will be launched to authenticate the user when running the sample.)

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

### Troubleshooting and Error conditions

If you see the following error:  "API call failed with the error code 'AuthorizationFailed'..." you likely are using an identity or service principal that does not have subscription level write access to the 'Microsoft.Media/mediaservices/write' entity. 
To avoid this issue, use the interactive login method (already configured in the sample) to access the sample with an identity that has the privilege to write to that resource path in ARM.

## Next steps

* [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
* [Azure Media Services v3 Documentation](https://docs.microsoft.com/azure/media-services/latest/)
