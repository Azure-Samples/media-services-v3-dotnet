# Live event with DVR

This sample demonstrates how to create and use LiveEvents and LiveOutputs in the v3 Media Services API. It performs the following tasks:

1. Creates a "standard" pass-through LiveEvent.
1. Starts monitoring the LiveEvent using Event Grid and Event Hub.
1. Creates an Asset and an AssetFilter with 5 minutes sliding window and 30 seconds seek back time.
1. Creates a LiveOutput with 25 hours time span.
1. Creates a StreamingLocator and associate the AssetFilter with it.
1. Prints urls for the LiveEvent.
1. Prints playback urls for the event archive after the LiveEvent stops.

## Live events and live outputs in Media Services

This sample shows how to create a "standard" pass-through live event.  There are several types of live events available at different pricing points.  This is the cheapest option, and takes the source video without transcoding it any further and just packages it for HLS and DASH delivery.  You can also choose "basic" pass-through, or from the two cloud transcoding live events for 720P and 1080P adaptive bitrate output.  
For details on the various types of live events see the article [Live events and live outputs in Media Services.](https://learn.microsoft.com/azure/media-services/latest/live-event-outputs-concept)

## Prerequisites

Required Assemblies:

* Azure.Identity
* Azure.Messaging.EventGrid
* Azure.Messaging.EventHubs.Processor
* Azure.ResourceManager.Media
* Azure.Storage.Blobs
* Microsoft.Extensions.Hosting
* System.Linq.Async

Also required:

* A camera connected to your computer.
* A media encoder. For a recommended encoder, please visit [Recommended encoders](https://learn.microsoft.com/azure/media-services/latest/encode-recommended-on-premises-live-encoders).
* An Azure Media Services account. See the steps described in [Create a Media Services account](https://learn.microsoft.com/azure/media-services/latest/account-create-how-to).

## Build and run

Update the settings in **appsettings.json** in the root folder of the repository.
For more information, see [Access APIs](https://learn.microsoft.com/azure/media-services/latest/access-api-howto).

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

### Optional - Use Event Grid instead of polling (recommended for production code)

* The following steps should be used if you want to test Event Grid for job monitoring. Please note, there are costs for using Event Hub. For more details, refer to [Event Hubs overview](https://azure.microsoft.com/en-in/pricing/details/event-hubs/) and [Event Hubs pricing](https://docs.microsoft.com/en-us/azure/event-hubs/event-hubs-faq#pricing).

#### Enable Event Grid resource provider

  `az provider register --namespace Microsoft.EventGrid`

#### To check if registered, run the next command. You should see "Registered"

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

* Create a storage account and container for Event Processor Host if you don't have one - see [Create a Storage account for event processor host](https://docs.microsoft.com/en-us/azure/event-hubs/event-hubs-dotnet-standard-getstarted-send#create-a-storage-account-for-event-processor-host)

* Update *appsettings.json* with your Event Hub and Storage information
  * **AZURE_STORAGE_ACCOUNT_NAME**: The name of your storage account.
  * **AZURE_STORAGE_ACCOUNT_KEY**: The access key for your storage account. In Azure portal "All resources", search your storage account, then click "Access keys", copy key1.
  * **AZURE_BLOB_CONTAINER_NAME**: The name of your container. Click Blobs in your storage account, find you container and copy the name.
  * **AZURE_EVENT_HUBS_CONNECTION_STRING**: The Event Hub connection string. Search for your Event Hub namespace you just created. &lt;your namespace&gt; -&gt; Shared access policies -&gt; RootManageSharedAccessKey -&gt; Connection string-primary key. You can optionally create a SAS policy for the Event Hub instance with Manage and Listen policies and use the connection string for the Event Hub instance.
  * **AZURE_EVENT_HUB_NAME**: The Event Hub instance name.  &lt;your namespace&gt; -&gt; Event Hubs.
  * **AZURE_CONSUMER_GROUP**: The name of the consumer group for the event hub processor.

## Key concepts

* [Dynamic packaging](https://learn.microsoft.com/azure/media-services/latest/dynamic-packaging-overview)
* [Streaming Policies](https://learn.microsoft.com/azure/media-services/latest/streaming-policy-concept)

## Next steps

* [Live Event states and billing](https://learn.microsoft.com/azure/media-services/latest/live-event-states-billing-concept)
* [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
* [Azure Media Services v3 Documentation](https://learn.microsoft.com/azure/media-services/latest/)
