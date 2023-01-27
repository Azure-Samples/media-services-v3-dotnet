---
topic: sample
languages:
  - csharp
products:
  - azure-media-services
---

# Prints quotas for a Media Services account

This sample shows how to use the metrics API to retrieve account quotas.

## Example output
```
Resource               Current     Quota
--------            ----------  --------
Assets                      19   1000000
Content Key Polices          1   1000000
Streaming Policies           0       100
Live Events                  2         5
Running Live Events          0         5
Transforms                           100
Jobs                              500000
Jobs Scheduled               0
```

## Prerequisites

* Required Assemblies

* Azure.Identity
* Azure.ResourceManager.Media
* Azure.Monitor.Query

Update the `MediaServicesAccountResource` in **Program.cs** to match your Azure subscription, resource group and Media Services account.

The sample will authenticate using any of the methods supported by [`DefaultAzureCredential`](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet).

## Next steps

* [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
* [Azure Media Services v3 Documentation](https://docs.microsoft.com/azure/media-services/latest/)
