# Dynamic packaging VOD content with filters

This sample demonstrates how to filter content using asset and account filters. It performs the following tasks:

1. Creates an encoding Transform that uses a built-in preset for adaptive bitrate encoding.
1. Ingests a file.
1. Submits a job.
1. Creates an asset filter.
1. Creates an Account filter.
1. Publishes output asset for streaming.
1. Gets streaming url(s) with filters.
1. Associates filters to a new streaming locator.
1. Gets streaming url(s) for the new locator.

## Prerequisites

* Required Assemblies

* Azure.Identity
* Azure.ResourceManager.Media
* Azure.Storage.Blobs
* System.Linq.Async

* An Azure Media Services account. See the steps described in [Create a Media Services account](https://learn.microsoft.com/azure/media-services/latest/account-create-how-to).

## Build and run

Update the settings in **appsetting.json** in the root folder of the repository to match your Azure subscription, resource group and Media Services account.
Then build and run the sample in Visual Studio or VS Code.

## Key concepts

* [Dynamic packaging](https://learn.microsoft.com/azure/media-services/latest/dynamic-packaging-overview)
* [Streaming Policies](https://learn.microsoft.com/azure/media-services/latest/streaming-policy-concept)

## Next steps

* [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
* [Azure Media Services v3 Documentation](https://learn.microsoft.com/azure/media-services/latest/)
