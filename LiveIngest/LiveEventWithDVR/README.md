---
topic: sample
languages:
  - c#
products:
  - azure-media-services
---

# LiveEventWithDVR

This sample first demonstrates how to create a LiveEvent with a full archive up to 25 hours and an filter on the asset with 5 minutes DVR window, then it shows how to use the filter to create a locator for streaming.

## Prerequisites
* Required Assemblies

  - Microsoft.Azure.Management.Media -Version 2.0.3
  - Microsoft.Extensions.Configuration -Version 2.1.1
  - Microsoft.Extensions.Configuration.EnvironmentVariables -Version 2.1.1
  - Microsoft.Extensions.Configuration.Json -Version 2.1.1
  - Microsoft.Rest.ClientRuntime.Azure.Authentication -Version 2.3.4
  - WindowsAzure.Storage -Version 9.3.2

* A camera connected to your computer.
* A media encoder. For a recommended encoder, please visit https://docs.microsoft.com/en-us/azure/media-services/latest/recommended-on-premises-live-encoders.
* An Azure Media Services account. See the steps described in [Create a Media Services account](https://docs.microsoft.com/azure/media-services/latest/create-account-cli-quickstart).

## Build and run

* Add appropriate values to the appsettings.json configuration file. For more information, see [Access APIs](https://docs.microsoft.com/azure/media-services/latest/access-api-cli-how-to).
* This is a windows console application that can be built and run in Visual Studio.

