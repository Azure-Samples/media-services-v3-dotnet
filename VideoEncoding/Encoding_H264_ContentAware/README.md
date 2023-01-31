# Encode with Content Aware Encoding Preset for H.264

This sample shows how to create a built-in Content Aware Encoding preset with settings. It shows how to perform the following tasks:

The content-aware encoding preset extends the "adaptive bitrate streaming" mechanism, by incorporating custom logic that lets the encoder seek the optimal bitrate value for a given resolution, 
but without requiring extensive computational analysis. This preset produces a set of GOP-aligned MP4s. Given any input content, the service performs an initial lightweight analysis of the 
input content, and uses the results to determine the optimal number of layers, appropriate bitrate and resolution settings for delivery by adaptive streaming. 
This preset is particularly effective for low and medium complexity videos, where the output files will be at lower bitrates than the Adaptive Streaming preset but at a quality that still 
delivers a good experience to viewers. The output will contain MP4 files with video and audio interleaved.

To learn more about Content Aware Encoding, see the article on [Content Aware Encoding](https://docs.microsoft.com/azure/media-services/latest/encode-content-aware-concept)

1. Creates a Content Aware Encoding transform for H.264 with settings.
1. Creates an input asset and upload a media file into it.
1. Submits a job and monitoring the job using polling method.
1. Downloads the output asset.
1. prints urls for streaming.

> [!TIP]
> The `Program.cs` file has extensive comments.

## Prerequisites

* Required Assemblies

* Azure.Storage.Blobs
* Azure.Messaging.EventGrid
* Azure.Messaging.EventHubs.Processor
* Microsoft.Azure.Management.Media
* Microsoft.Extensions.Hosting
* Microsoft.Identity.Client
* Azure.Identity

* An Azure Media Services account. See the steps described in [Create a Media Services account](https://docs.microsoft.com/en-us/azure/media-services/latest/account-create-how-to).

## Build and run

Update  **.env file** at the root of the solution with your account settings or updated the **appsettings.json** in the project folder. Please choose only one of these two methods.
For more information, see [Access APIs](https://docs.microsoft.com/azure/media-services/latest/access-api-howto).

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

If using the .env settings file in the root of the solution instead, you need to fill out the information for these environment variables.

  #### Event Hub settings to listen to Event Grid subscription
  EVENTHUBCONNECTIONSTRING=""
  EVENTHUBNAME=""
  EVENTCONSUMERGROUP=""

  #### Azure Storage Account settings
  STORAGECONTAINERNAME=""
  STORAGEACCOUNTNAME=""
  STORAGEACCOUNTKEY=""
  STORAGECONNECTIONSTRING=""

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

* Update *appsettings.json* or *.env* (at root of solution) with your Event Hub and Storage information
  * **StorageAccountName**: The name of your storage account.
  * **StorageAccountKey**: The access key for your storage account. In Azure portal "All resources", search your storage account, then click "Access keys", copy key1.
  * **StorageConnectionString**: This is required for the event hub client to listen to the subscribed media events. 
  * **StorageContainerName**: The name of your container. Click Blobs in your storage account, find you container and copy the name.
  * **EventHubConnectionString**: The Event Hub connection string. Search for your Event Hub namespace you just created. &lt;your namespace&gt; -&gt; Shared access policies -&gt; RootManageSharedAccessKey -&gt; Connection string-primary key. You can optionally create a SAS policy for the Event Hub instance with Manage and Listen policies and use the connection string for the Event Hub instance.
  * **EventHubName**: The Event Hub instance name.  &lt;your namespace&gt; -&gt; Event Hubs.
  * **EventConsumerGroup**: The Event Hub consumer group name - this is "$Default" typically, unless you changed it in the portal during setup of your Event Hub. 


## Next steps

* [Streaming videos](https://docs.microsoft.com/en-us/azure/media-services/latest/stream-files-tutorial-with-api)
* [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
* [Azure Media Services v3 Documentation](https://docs.microsoft.com/azure/media-services/latest/)
