# Encode with Content Aware Encoding Preset for H.264 with Constraints

This sample shows how to use constraints with the built-in Content Aware Encoding preset with settings. It shows how to perform the following tasks:

The content-aware encoding preset extends the "adaptive bitrate streaming" mechanism, by incorporating custom logic that lets the encoder seek the optimal bitrate value for a given resolution, 
but without requiring extensive computational analysis. This preset produces a customized and constrained set of GOP-aligned MP4s. Given any input content, the service performs an initial lightweight analysis of the 
input content, and uses the results to determine the optimal number of layers, appropriate bitrate and resolution settings for delivery by adaptive streaming. 
This preset is particularly effective for low and medium complexity videos, where the output files will be at lower bitrates than the Adaptive Streaming preset but at a quality that still 
delivers a good experience to viewers. The output will contain MP4 files with video and audio interleaved.

## The PresetConfigurations class

Using the **[PresetConfigurations](https://github.com/Azure/azure-rest-api-specs/blob/32d5a0348f38da79fafdf14b945df0f9b8119df4/specification/mediaservices/resource-manager/Microsoft.Media/stable/2021-06-01/Encoding.json#L2397)** class with the Content Aware Encoding preset allows you to adjust the outputs and constrain them. **PresetConfigurations** are only supported for the ContentAwareEncoding and H265ContentAwareEncoding built-in presets. These settings will not affect other built-in or custom defined presets.

To use the PresetConfigurations, first create an instance of the object and fill out the values that you wish to constrain on the CAE preset. 
Available values are:
 
- complexity (Speed | Balanced | Quality)
- interleaveOutput (NonInterleavedOutput | InterleavedOutput)
- keyFrameIntervalInSeconds
- maxBitrateBps
- maxHeight
- maxLayers
- minBitrateBps
- minHeight 

Next, pass this new object into the [BuiltInStandardEncodingPreset.configurations](https://github.com/Azure/azure-rest-api-specs/blob/32d5a0348f38da79fafdf14b945df0f9b8119df4/specification/mediaservices/resource-manager/Microsoft.Media/stable/2021-06-01/Encoding.json#L1354) property.

``` csharp
var transform = await mediaServicesAccount.GetMediaTransforms().CreateOrUpdateAsync(
    WaitUntil.Completed,
    transformName,
    new MediaTransformData
    {
        Outputs =
        {
            new MediaTransformOutput(
                preset: new BuiltInStandardEncoderPreset(EncoderNamedPreset.ContentAwareEncoding)
                {
                    Configurations = new EncoderPresetConfigurations
                    {
                        // Allows you to configure the encoder settings to control the balance between speed and quality. Example: set Complexity
                        // as Speed for faster encoding but less compression efficiency.
                        Complexity = EncodingComplexity.Speed,
                        // The output includes both audio and video.
                        InterleaveOutput = InterleaveOutput.InterleavedOutput,
                        // The key frame interval in seconds. Example: set as 2 to reduce the playback buffering for some players.
                        KeyFrameIntervalInSeconds = 2,
                        // The maximum bitrate in bits per second (threshold for the top video layer). Example: set MaxBitrateBps as 6000000 to
                        // avoid producing very high bitrate outputs for contents with high complexity.
                        MaxBitrateBps = 6000000,
                        // The minimum bitrate in bits per second (threshold for the bottom video layer). Example: set MinBitrateBps as 200000 to
                        // have a bottom layer that covers users with low network bandwidth.
                        MinBitrateBps = 200000,
                        MaxHeight = 720,
                        // The minimum height of output video layers. Example: set MinHeight as 360 to avoid output layers of smaller resolutions like 180P.
                        MinHeight = 270,
                        // The maximum number of output video layers. Example: set MaxLayers as 4 to make sure at most 4 output layers are produced
                        // to control the overall cost of the encoding job.
                        MaxLayers = 3
                    }
                }
            )
        }
    });
```

## Benefits of using the PresetConfigurations on the Content Aware Encoding Preset

- Provides more predictable outputs and bitrates
- Helps to constrain costs by limiting the number of outputs and resolutions used
- Improves speed of encoding by reducing the output options

## Sample Workflow 
To learn more about Content Aware Encoding, see the article on [Content Aware Encoding](https://learn.microsoft.com/azure/media-services/latest/encode-content-aware-concept)

1. Creates a Content Aware Encoding transform for H.264 with settings.
1. Configures the PresetConfigurations class to control the number of outputs and resolutions allowed when using the CAE Encoder
1. Creates an input asset and upload a media file into it.
1. Submits a job and monitoring the job using polling method.
1. Downloads the output asset.
1. Prints URLs for streaming.

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

## Next steps

* [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
* [Azure Media Services v3 Documentation](https://learn.microsoft.com/azure/media-services/latest/)
