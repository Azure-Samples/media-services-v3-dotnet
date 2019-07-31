---
topic: sample
languages:
  - c#
products:
  - azure-media-services
---

# Live event with DVR

This sample demonstrates how to create and use LiveEvents and LiveOutputs in the v3 Media Services API. It performs the following tasks:
1. Create a pass-through LiveEvent.
1. Start monitoring the LiveEvent using Event Grid and Event Hub.
1. Create an Asset and an AssetFilter with 5 minutes sliding window and 30 seconds seek back time.
1. Create a LiveOutput with 25 hours time span.
1. Create a StreamingLocator and associate the AssetFilter with it.
1. Print urls for the LiveEvent.
1. Print playback urls for the event archive after the LiveEvent stops.

## Prerequisites
* Required Assemblies

- Microsoft.Azure.EventGrid -Version 3.2.0
- Microsoft.Azure.EventHubs -Version 3.0.0
- Microsoft.Azure.EventHubs.Processor -Version 3.0.0
- Microsoft.Azure.Management.Media -Version 2.0.3
- Microsoft.Extensions.Configuration -Version 2.1.1
- Microsoft.Extensions.Configuration.EnvironmentVariables -Version 2.1.1
- Microsoft.Extensions.Configuration.Json -Version 2.1.1
- Microsoft.Rest.ClientRuntime.Azure.Authentication -Version 2.4.0
- WindowsAzure.Storage -Version 9.3.3

* A camera connected to your computer.
* A media encoder. For a recommended encoder, please visit [Recommended encoders](https://docs.microsoft.com/en-us/azure/media-services/latest/recommended-on-premises-live-encoders).
* An Azure Media Services account. See the steps described in [Create a Media Services account](https://docs.microsoft.com/azure/media-services/latest/create-account-cli-quickstart).

## Build and run

* Add appropriate values to the appsettings.json configuration file. For more information, see [Access APIs](https://docs.microsoft.com/azure/media-services/latest/access-api-cli-how-to).

* Build and run the sample in Visual Studio.

* Optional, do the following steps if you want to use Event Grid for job monitoring. Please be noted, there are costs for using Event Hub. For more details, refer https://azure.microsoft.com/en-in/pricing/details/event-hubs/ and https://docs.microsoft.com/en-us/azure/event-hubs/event-hubs-faq#pricing.

- Enable Event Grid resource provider

  `az provider register --namespace Microsoft.EventGrid`

- To check if registered, run the next command. You should see "Registered".

  `az provider show --namespace Microsoft.EventGrid --query "registrationState"`

- Create an Event Hub

  `namespace=<unique-namespace-name>`\
  `hubname=<event-hub-name>`\
  `az eventhubs namespace create --name $namespace --resource-group <resource-group>`\
  `az eventhubs eventhub create --name $hubname --namespace-name $namespace --resource-group <resource-group>`

- Subscribe to Media Services events

  `hubid=$(az eventhubs eventhub show --name $hubname --namespace-name $namespace --resource-group <resource-group> --query id --output tsv)`\
  `amsResourceId=$(az ams account show --name <ams-account> --resource-group <resource-group> --query id --output tsv)`\
  `az eventgrid event-subscription create --resource-id $amsResourceId --name <event-subscription-name> --endpoint-type eventhub --endpoint $hubid`

- Create a storage account and container for Event Processor Host if you don't have one
  https://docs.microsoft.com/en-us/azure/event-hubs/event-hubs-dotnet-standard-getstarted-send#create-a-storage-account-for-event-processor-host

- Update appsettings.json with your Event Hub and Storage information
  StorageAccountName: The name of your storage account.\
  StorageAccountKey: The access key for your storage account. In Azure portal "All resources", search your storage account, then click "Access keys", copy key1.\
  StorageContainerName: The name of your container. Click Blobs in your storage account, find your container and copy the name.\
  EventHubConnectionString: The Event Hub connection string. Search for the namespace you just created. &lt;your namespace&gt; -&gt; Shared access policies -&gt; RootManageSharedAccessKey -&gt; Connection string-primary key.\
  EventHubName: The Event Hub name.  &lt;your namespace&gt; -&gt; Event Hubs.

  ## Key concepts

* [Dynamic packaging](https://docs.microsoft.com/azure/media-services/latest/dynamic-packaging-overview)
* [Streaming Policies](https://docs.microsoft.com/azure/media-services/latest/streaming-policy-concept)

## Next steps

- [Live Event states and billing](https://docs.microsoft.com/en-us/azure/media-services/latest/live-event-states-billing)
- [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
- [Azure Media Services v3 Documentation](https://docs.microsoft.com/azure/media-services/latest/)
