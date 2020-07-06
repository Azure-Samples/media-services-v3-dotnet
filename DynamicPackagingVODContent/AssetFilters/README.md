---
topic: sample
languages:
  - c#
products:
  - azure-media-services
---

# Dynamic packaging VOD content into HLS/DASH and streaming

This sample demonstrates how to filter content using asset and account filters. It performs the following tasks:

1. Creates an encoding Transform that uses a built-in preset for adaptive bitrate encoding.
1. Ingests a file.
1. Submits a job.
1. Creates an asset filter.
1. Creates an Account filter.
1. Publishes output asset for streaming.
1. Gets Dash streaming url(s) with filters.
1. Associates filters to a new streaming locator.
1. Gets Dash streaming url(s) for the new locator.

## Prerequisites

* Required Assemblies

- Microsoft.Azure.Management.Media -Version 2.0.5
- Microsoft.Extensions.Configuration -Version 3.1.5
- Microsoft.Extensions.Configuration.EnvironmentVariables -Version 3.1.5
- Microsoft.Extensions.Configuration.Json -Version 3.1.5
- Microsoft.Rest.ClientRuntime.Azure.Authentication -Version 2.4.1
- Microsoft.Azure.Storage.Blob -Version 11.1.7


## Build and run

* Update appsettings.json with your account settings The settings for your account can be retrieved using the following Azure CLI command in the Media Services module. The following bash shell script creates a service principal for the account and returns the json settings.

    `#!/bin/bash`

    `resourceGroup=&lt;your resource group&gt;`\
    `amsAccountName=&lt;your ams account name&gt;`\
    `amsSPName=&lt;your AAD application&gt;`

    `#Create a service principal with password and configure its access to an Azure Media Services account.`\
    `az ams account sp create` \\\
    `--account-name $amsAccountName` \\\
    `--name $amsSPName` \\\
    `--resource-group $resourceGroup` \\\
    `--role Owner` \\\
    `--years 2`

* Build and run the sample in Visual Studio

## Key concepts

* [Dynamic packaging](https://docs.microsoft.com/azure/media-services/latest/dynamic-packaging-overview)
* [Streaming Policies](https://docs.microsoft.com/azure/media-services/latest/streaming-policy-concept)

## Next steps

* [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
* [Azure Media Services v3 Documentation](https://docs.microsoft.com/azure/media-services/latest/)
