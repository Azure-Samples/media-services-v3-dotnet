---
topic: sample
languages:
  - c#
products:
  - azure-media-services
---

# Streaming an existing single bitrate MP4 file with HLS or Dash

This sample demonstrates how to dynamically package VOD content from an existing pre-encoded MP4 file (or set of ABR encoded Mp4s) and stream with HLS/DASH. 
It shows how you can create the required server manifest (.ism) and client manifest (.ismc) files needed for the Azure Media Services origin server to stream a pre-encoded file to HLS or DASH formats. 
Once the server and client manifest are available, the streaming server understands how to dynamically generate HLS and DASH format.  
Keep in mind that this can be slightly less performant and heavier on the CPU of the Streaming Endpoint than generating the content directly through the Standard encoder presets for adaptive streaming (AdaptiveStreaming or Content Aware Encoding presets)
due to the fact that the .mpi index binary files are not included.  When using this method, be sure to check with the AMS team when you are considering scaling the solution up to production level usage. 

It performs the following tasks:
1. Uploads an existing Mp4 file that is encoded with 2-second GOPs, CBR rate control, max 2 second key frame and min 2 second key frame distances. 
1. Creates a .ism manifest to point to the files uploaded and saves the .ism (server manifest) file back to the Asset container
1. Generates a request to create the .ismc file (client manifest) and saves it back into the Asset container.
1. Publishes output asset for HLS and DASH streaming.
1. Provides the HLS manifest URL for use in iOS, or any HLS supported player framework (i.e. Shaka, Hls.js, Video.js, Theo Player, Bitmovin, etc.)
1. Provides the DASH link and a clickable URL to launch the Azure Media Player with the DASH manifest using the "QuickStart" heuristics mode.

This sample only shows how to upload a single bitrate, but this sample can easily be extended to show how to upload a full adaptive bitrate (ABR) set of Mp4 files for streaming in Azure Media Services.

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
