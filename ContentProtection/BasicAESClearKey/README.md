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

- Microsoft.Azure.EventGrid -Version 3.2.0
- Microsoft.Azure.EventHubs -Version 3.0.0
- Microsoft.Azure.EventHubs.Processor -Version 3.0.0
- Microsoft.Azure.Management.Media -Version 2.0.0
- Microsoft.Extensions.Configuration -Version 2.1.1
- Microsoft.Extensions.Configuration.EnvironmentVariables -Version 2.1.1
- Microsoft.Extensions.Configuration.Json -Version 2.1.1
- Microsoft.Rest.ClientRuntime.Azure.Authentication -Version 2.4.0
- WindowsAzure.Storage -Version 9.3.3
- Microsoft.IdentityModel.Tokens -Version 5.3.0
- System.IdentityModel.Tokens.Jwt -Version 5.3.0
- System.Security.Claims 4.3.0

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

* Optional, do the following steps if you want to use Event Grid for job monitoring. Please note, there are costs for using Event Hub. For more details, refer https://azure.microsoft.com/en-in/pricing/details/event-hubs/ and https://docs.microsoft.com/en-us/azure/event-hubs/event-hubs-faq#pricing.

- Enable Event Grid resource provider

  `az provider register --namespace Microsoft.EventGrid`

- To check if registered, run the next command. You should see "Registered".

  `az provider show --namespace Microsoft.EventGrid --query "registrationState"`

- Create an Event Hub

  `namespace=&lt;unique-namespace-name&gt;`\
  `hubname=&lt;event-hub-name&gt;`\
  `az eventhubs namespace create --name $namespace --resource-group &lt;resource-group&gt;`\
  `az eventhubs eventhub create --name $hubname --namespace-name $namespace --resource-group &lt;resource-group&gt;`

- Subscribe to Media Services events

  `hubid=$(az eventhubs eventhub show --name $hubname --namespace-name $namespace --resource-group &lt;resource-group&gt; --query id --output tsv)`\
  `amsResourceId=$(az ams account show --name &lt;ams-account&gt; --resource-group &lt;resource-group&gt; --query id --output tsv)`\
  `az eventgrid event-subscription create --resource-id $amsResourceId --name &lt;event-subscription-name&gt; --endpoint-type eventhub --endpoint $hubid`

- Create a storage account and container for Event Processor Host if you don't have one
  https://docs.microsoft.com/en-us/azure/event-hubs/event-hubs-dotnet-standard-getstarted-send#create-a-storage-account-for-event-processor-host

- Update appsettings.json with your Event Hub and Storage information
  StorageAccountName: The name of your storage account.\
  StorageAccountKey: The access key for your storage account. In Azure portal "All resources", search your storage account, then click "Access keys", copy key1.\
  StorageContainerName: The name of your container. Click Blobs in your storage account, find you container and copy the name.\
  EventHubConnectionString: The Event Hub connection string. search your namespace you just created. &lt;your namespace&gt; -&gt; Shared access policies -&gt; RootManageSharedAccessKey -&gt; Connection string-primary key.\
  EventHubName: The Event Hub name.  &lt;your namespace&gt; -&gt; Event Hubs.

## Key concepts

* [Dynamic packaging](https://docs.microsoft.com/azure/media-services/latest/dynamic-packaging-overview)
* [Content protection with dynamic encryption](https://docs.microsoft.com/azure/media-services/latest/content-protection-overview)
* [Streaming Policies](https://docs.microsoft.com/azure/media-services/latest/streaming-policy-concept)

## Next steps

- [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
- [Azure Media Services v3 Documentation](https://docs.microsoft.com/azure/media-services/latest/)
