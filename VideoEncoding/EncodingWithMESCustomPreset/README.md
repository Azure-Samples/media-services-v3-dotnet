# Encode with a custom Transform
This sample shows how to create a custom encoding Transform using the StandardEncoderPreset settings. It shows how to perform the following tasks:
1. Creates a custom encoding transform.
1. Creates an input asset and upload a media file into it.
1. Submits a job and monitoring the job using polling method.
1. Downloads the output asset.
1. prints urls for streaming.

> [!TIP]
> The `Program.cs` file has extensive comments.

## Prerequisites

* Required Assemblies

- Microsoft.Azure.Management.Media -Version 2.0.1
- Microsoft.Extensions.Configuration -Version 2.1.1
- Microsoft.Extensions.Configuration.EnvironmentVariables -Version 2.1.1
- Microsoft.Extensions.Configuration.Json -Version 2.1.1
- Microsoft.Rest.ClientRuntime.Azure.Authentication -Version 2.3.4
- WindowsAzure.Storage  -Version 9.3.2

* An Azure Media Services account. See the steps described in [Create a Media Services account](https://docs.microsoft.com/azure/media-services/latest/create-account-cli-quickstart).

## Build and run

* Update appsettings.json with your account settings The settings for your account can be retrieved using the following Azure CLI command in the Media Services module. The following bash shell script creates a service principal for the account and returns the json settings.

    `#!/bin/bash`

    `resourceGroup=&lt;your resource group&gt;`\
    `amsAccountName=&lt;your ams account name&gt;`\
    `amsSPName=&lt;your AAD application&gt;`

    `#Create a service principal with password and configure its access to an Azure Media Services account.`
    `az ams account sp create` \\\
    `--account-name $amsAccountName` \\\
    `--name $amsSPName` \\\
    `--resource-group $resourceGroup` \\\
    `--role Owner` \\\
    `--years 2`

* Build and run the sample in Visual Studio.

## Next steps

- [Streaming videos](https://docs.microsoft.com/en-us/azure/media-services/latest/stream-files-tutorial-with-api)
- [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
- [Azure Media Services v3 Documentation](https://docs.microsoft.com/azure/media-services/latest/)
