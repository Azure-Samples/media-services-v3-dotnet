---
topic: sample
languages:
  - csharp
products:
  - azure-media-services
description: "This sample demonstrates how to create an encoding Transform that uses a built-in preset for adaptive bitrate encoding."
---

# Encoding With MES Predefined Preset

This sample demonstrates how to create an encoding Transform that uses a built-in preset for adaptive bitrate encoding and ingests a file directly from an HTTPs source URL, publish output asset for streaming, and download results for verification.

## Prerequisites

* Required Assemblies

- Azure.Storage.Blobs
- Microsoft.Azure.Management.Media
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Configuration.EnvironmentVariables
- Microsoft.Extensions.Configuration.Json
- Microsoft.Rest.ClientRuntime.Azure.Authentication

* An Azure Media Services account. See the steps described in [Create a Media Services account](https://docs.microsoft.com/azure/media-services/latest/create-account-cli-quickstart).

## Build and run

* Update appsettings.json with your account settings The settings for your account can be retrieved using the following Azure CLI command in the Media Services module. The following bash shell script creates a service principal for the account and returns the json settings.

    #!/bin/bash
    
    resourceGroup=&lt;your resource group&gt;\
    amsAccountName=&lt;your AMS Account name&gt;\
    amsSPName=&lt;your AAD application&gt; 

    # Create a service principal with password and configure its access to an Azure Media Services account.
    az ams account sp create  \\\
    --account-name $amsAccountName  \\\
    --name $amsSPName  \\\
    --resource-group $resourceGroup  \\\
    --role Owner  \\\
    --years 2
