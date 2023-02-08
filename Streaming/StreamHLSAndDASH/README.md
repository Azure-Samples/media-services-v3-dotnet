---
topic: sample
languages:
  - c#
products:
  - azure-media-services
---

# Dynamic packaging VOD content into HLS/DASH and streaming

This sample demonstrates how to dynamically package VOD content into HLS/DASH for streaming. It performs the following tasks:
1. Creates an encoding Transform that uses a built-in preset for adaptive bitrate encoding.
1. Ingests a file.
1. Submits a job.
1. Publishes output asset for HLS and DASH streaming.

## Prerequisites

* Required Assemblies

- Azure.Storage.Blobs
- Microsoft.Azure.Management.Media
- Microsoft.Extensions.Hosting
- Microsoft.Identity.Client

* An Azure Media Services account. See the steps described in [Create a Media Services account](https://learn.microsoft.com/azure/media-services/latest/account-create-how-to).

## Build and run

Update **appsettings.json** in the project folder OR create a **.env file** at the root of the solution with your account settings. Please choose one of these two methods.
For more information, see [Access APIs](https://learn.microsoft.com/azure/media-services/latest/access-api-howto).

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

## Key concepts

* [Dynamic packaging](https://learn.microsoft.com/azure/media-services/latest/dynamic-packaging-overview)
* [Streaming Policies](https://learn.microsoft.com/azure/media-services/latest/streaming-policy-concept)

## Next steps

- [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
- [Azure Media Services v3 Documentation](https://learn.microsoft.com/azure/media-services/latest/)
