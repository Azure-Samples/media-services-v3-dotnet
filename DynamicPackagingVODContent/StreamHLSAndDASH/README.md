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

- Microsoft.Azure.Management.Media -Version 2.0.4
- Microsoft.Extensions.Configuration -Version 3.1.3
- Microsoft.Extensions.Configuration.EnvironmentVariables -Version 3.1.3
- Microsoft.Extensions.Configuration.Json -Version 3.1.3
- Microsoft.Extensions.Configuration.EnvironmentVariables -Version 3.1.3
- Microsoft.Rest.ClientRuntime.Azure.Authentication -Version 2.4.0
- Microsoft.Azure.Storage.Blob -Version 11.1.5

* An Azure Media Services account. See the steps described in [Create a Media Services account](https://docs.microsoft.com/azure/media-services/latest/create-account-cli-quickstart).

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

- [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
- [Azure Media Services v3 Documentation](https://docs.microsoft.com/azure/media-services/latest/)
