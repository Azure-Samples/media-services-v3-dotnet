---
topic: sample
languages:
  - csharp
products:
  - azure-media-services
---

# VideoAndAudioAnalyzer

This sample demonstrates how to create a transform that uses a video analyzer preset, upload a video file to an input asset, create and run an analyzer job, and download the output asset for verification.

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

* An Azure Media Services account. See the steps described in [Create a Media Services account](https://docs.microsoft.com/azure/media-services/latest/create-account-cli-quickstart).

## Build and run

* Update appsettings.json with your account settings The settings for your account can be retrieved using the following Azure CLI command in the Media Services module. The following bash shell script creates a service principal for the account and returns the json settings.

    #!/bin/bash

    resourceGroup=&lt;your resource group&gt;\
    amsAccountName=&lt;your ams account name&gt;\
    amsSPName=&lt;your AAD application&gt;

    #Create a service principal with password and configure its access to an Azure Media Services account.
    az ams account sp create \\\
    --account-name $amsAccountName \\\
    --name $amsSPName \\\
    --resource-group $resourceGroup \\\
    --role Owner \\\
    --years 2

* Optional, do the following steps if you want to use Event Grid for job monitoring. Please be noted, there are costs for using Event Hub. For more details, refer https://azure.microsoft.com/en-in/pricing/details/event-hubs/ and https://docs.microsoft.com/en-us/azure/event-hubs/event-hubs-faq#pricing.

- Enable Event Grid resource provider

  az provider register --namespace Microsoft.EventGrid

- To check if registered, run the next command. You should see "Registered".

  az provider show --namespace Microsoft.EventGrid --query "registrationState"

- Create an Event Hub

  namespace=&lt;unique-namespace-name&gt;\
  hubname=&lt;event-hub-name&gt;\
  az eventhubs namespace create --name $namespace --resource-group &lt;resource-group&gt;\
  az eventhubs eventhub create --name $hubname --namespace-name $namespace --resource-group &lt;resource-group&gt;

- Subscribe to Media Services events

  hubid=$(az eventhubs eventhub show --name $hubname --namespace-name $namespace --resource-group &lt;resource-group&gt; --query id --output tsv)\
  amsResourceId=$(az ams account show --name &lt;ams-account&gt; --resource-group &lt;resource-group&gt; --query id --output tsv)\
  az eventgrid event-subscription create --resource-id $amsResourceId --name &lt;event-subscription-name&gt; --endpoint-type eventhub --endpoint $hubid

- Create a storage account and container for Event Processor Host if you don't have one
  https://docs.microsoft.com/en-us/azure/event-hubs/event-hubs-dotnet-standard-getstarted-send#create-a-storage-account-for-event-processor-host

- Update appsettings.json with your Event Hub and Storage information
  StorageAccountName: The name of your storage account.\
  StorageAccountKey: The access key for your storage account. In Azure portal "All resources", search your storage account, then click "Access keys", copy key1.\
  StorageContainerName: The name of your container. Click Blobs in your storage account, find you container and copy the name.\
  EventHubConnectionString: The Event Hub connection string. search your namespace you just created. &lt;your namespace&gt; -&gt; Shared access policies -&gt; RootManageSharedAccessKey -&gt; Connection string-primary key.\
  EventHubName: The Event Hub name.  &lt;your namespace&gt; -&gt; Event Hubs.
