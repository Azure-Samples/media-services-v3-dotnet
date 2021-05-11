---
topic: sample
languages:
  - c#
products:
  - azure-media-services
---

# Offline playback with FairPlay DRM

This sample demonstrates how to dynamically encrypt your content with FairPlay DRM and play the content without requesting a license from license service. It shows how to perform the following tasks:

1. Creates a transform with built-in AdaptiveStreaming preset
1. Submits a job
1. Creates a ContentKeyPolicy with open restriction and FairPlay persistent configuration
1. Creates a custom StreamingPolicy
1. Associates the ContentKeyPolicy and the StreamingPolicy with a StreamingLocator
1. Prints a url for playback

When a user requests FairPlay protected content for the first time, the player application requests a license from the Media Services license service. If the player application is authorized, the Media Services license service issues a license to the player and the license is persisted. Because the license is persisted, subsequent playback won't send a request to license service again.

> [!TIP]
> The `Program.cs` file has extensive comments.

## Prerequisites

* Required Assemblies

- Azure.Storage.Blobs
- Microsoft.Azure.EventGrid
- Microsoft.Azure.EventHubs
- Microsoft.Azure.EventHubs.Processor
- Microsoft.IdentityModel.Tokens
- System.IdentityModel.Tokens.Jwt
- Microsoft.Azure.Management.Media
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Configuration.EnvironmentVariables
- Microsoft.Extensions.Configuration.Json
- Microsoft.Identity.Client
- System.Security.Claims


* An Azure Media Services account. See the steps described in [Create a Media Services account](https://docs.microsoft.com/azure/media-services/latest/create-account-cli-quickstart).
* An Apple ASK(Application Secret Key).
* An Apple certificate(.pfx) and password.

## Run the sample

* Configure `appsettings.json` with appropriate access values. The settings for your account can be retrieved using the following Azure CLI command in the Media Services module. The following bash shell script creates a service principal for the account and returns the json settings.

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

* Build and run the sample in Visual Studio.

### Optional - Use Event Grid instead of polling (recommended for production code)

* The following steps should be used if you want to test Event Grid for job monitoring. Please note, there are costs for using Event Hub. For more details, refer to [Event Hubs overview](https://azure.microsoft.com/en-in/pricing/details/event-hubs/) and [Event Hubs pricing](https://docs.microsoft.com/en-us/azure/event-hubs/event-hubs-faq#pricing).

#### Enable Event Grid resource provider

  `az provider register --namespace Microsoft.EventGrid`

#### To check if registered, run the next command. You should see "Registered".

  `az provider show --namespace Microsoft.EventGrid --query "registrationState"`

#### Create an Event Hub

```bash
  namespace=<unique-namespace-name>
  hubname=<event-hub-name>
  az eventhubs namespace create --name $namespace --resource-group <resource-group>
  az eventhubs eventhub create --name $hubname --namespace-name $namespace --resource-group <resource-group>
```

#### Subscribe to Media Services events

```bash
  hubid=$(az eventhubs eventhub show --name $hubname --namespace-name $namespace --resource-group <resource-group> --query id --output tsv)\
  
  amsResourceId=$(az ams account show --name <ams-account> --resource-group <resource-group> --query id --output tsv)\
  
  az eventgrid event-subscription create --source-resource-id $amsResourceId --name &lt;event-subscription-name&gt; --endpoint-type eventhub --endpoint $hubid
```


- Create a storage account and container for Event Processor Host if you don't have one - see [Create a Storage account for event processor host](https://docs.microsoft.com/en-us/azure/event-hubs/event-hubs-dotnet-standard-getstarted-send#create-a-storage-account-for-event-processor-host)

- Update *appsettings.json* or *.env* (at root of solution) with your Event Hub and Storage information
  - **StorageAccountName**: The name of your storage account.
  - **StorageAccountKey**: The access key for your storage account. In Azure portal "All resources", search your storage account, then click "Access keys", copy key1.
  - **StorageContainerName**: The name of your container. Click Blobs in your storage account, find you container and copy the name.
  - **EventHubConnectionString**: The Event Hub connection string. Search for your Event Hub namespace you just created. &lt;your namespace&gt; -&gt; Shared access policies -&gt; RootManageSharedAccessKey -&gt; Connection string-primary key. You can optionally create a SAS policy for the Event Hub instance with Manage and Listen policies and use the connection string for the Event Hub instance.
  - **EventHubName**: The Event Hub instance name.  &lt;your namespace&gt; -&gt; Event Hubs.

## Key concepts

* [Dynamic packaging](https://docs.microsoft.com/azure/media-services/latest/dynamic-packaging-overview)
* [Content protection with dynamic encryption](https://docs.microsoft.com/azure/media-services/latest/content-protection-overview)
* [Streaming Policies](https://docs.microsoft.com/azure/media-services/latest/streaming-policy-concept)

## Next steps

- [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
- [Azure Media Services v3 Documentation](https://docs.microsoft.com/azure/media-services/latest/)
