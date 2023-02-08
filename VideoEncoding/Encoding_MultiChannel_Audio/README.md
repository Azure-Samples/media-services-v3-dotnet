---
topic: sample
languages:
  - csharp
products:
  - azure-media-services
description: "This sample demonstrates how to create an encoding Transform that uses a built-in preset for adaptive bitrate encoding."
---

# Encoding for multi-channel audio with channel mapping Preset

This sample demonstrates how to create an encoding Transform that uses channel mapping and audio track selection from the input source to output two new AAC audio tracks.
The standard encoder is limited to outputting 1 Stereo track, followed by a 5.1 surround sound audio track in AAC format.

In this example we input an audio only source file with the following track layout of discreet audio tracks. The file is named "surround-audio.mp4" in the project folder.

1) Left stereo
2) Right stereo
3) Left front surround
4) Right front surround
5) Center surround
6) Low frequency
7) Back left
8) Back right

We then create a list of TrackDescriptor type to allow us to selectively map the audio tracks from the input source file into a specific Channel on the output.  The order of these are important when output to the Transform next below.

```csharp
var jobInput = new MediaJobInputAsset(inputAssetName)
{
    InputDefinitions =
    {
        new MediaJobInputFile
        {
            Filename = InputMP4FileName,
            IncludedTracks =
            {
                new SelectAudioTrackById(0) { ChannelMapping = ChannelMapping.StereoLeft },
                new SelectAudioTrackById(1) { ChannelMapping = ChannelMapping.StereoRight },
                new SelectAudioTrackById(2) { ChannelMapping = ChannelMapping.FrontLeft },
                new SelectAudioTrackById(3) { ChannelMapping = ChannelMapping.FrontRight },
                new SelectAudioTrackById(4) { ChannelMapping = ChannelMapping.Center },
                new SelectAudioTrackById(5) { ChannelMapping = ChannelMapping.LowFrequencyEffects },
                new SelectAudioTrackById(6) { ChannelMapping = ChannelMapping.BackLeft },
                new SelectAudioTrackById(7) { ChannelMapping = ChannelMapping.BackRight }
            }
        }

    }
};
```

A Transform is then created to generate the Stereo and 5.1 surround sound tracks from the track descriptor list. The first two tracks in the track descriptor list will be output to the Stereo AAC output defined as 2 channels in the Transform. The remaining 6 tracks will go into the second AAC output defined in the transform as using 6 channels.

```csharp
// Create the custom Transform with the outputs defined above
// Does a Transform already exist with the desired name? This method will just overwrite (Update) the Transform if it exists already. 
// In production code, you may want to be cautious about that. It really depends on your scenario.
var transform = await mediaServicesAccount.GetMediaTransforms().CreateOrUpdateAsync(
    WaitUntil.Completed,
    transformName,
    new MediaTransformData
    {
        Outputs =
        {
            // Create a new TransformOutput with a custom Standard Encoder Preset using the HEVC (H265Layer) codec
            // This demonstrates how to create custom codec and layer output settings
            new MediaTransformOutput(
                preset: new StandardEncoderPreset(
                    codecs: new MediaCodecBase[]
                    {
                        new AacAudio
                        {
                            Channels = 2,
                            SamplingRate = 48000,
                            Bitrate = 128000,
                            Profile = AacAudioProfile.AacLc,
                            Label = "stereo"
                        },
                        new AacAudio
                        {
                            Channels = 6, // 5.1 surround sound track 
                            SamplingRate = 48000,
                            Bitrate = 320000,
                            Profile = AacAudioProfile.AacLc,
                            Label = "surround"
                        }
                    },
                    // Specify the format for the output files
                    formats: new MediaFormatBase[]
                    {
                        // Mux the AAC audio into MP4 files, using basename, label, bitrate and extension macros
                        new Mp4Format(filenamePattern: "{Basename}-{Label}-{Bitrate}{Extension}")
                        {
                            OutputFiles =
                            {
                                new MediaOutputFile(new[] { "stereo"}),
                                new MediaOutputFile(new[] { "surround"})
                            }
                        }
                    }
                )
            )
            {
                OnError = MediaTransformOnErrorType.StopProcessingJob,
                RelativePriority = MediaJobPriority.Normal
            }
        },
        Description = "A custom multi-channel audio encoding preset"
    });
```

After encoding, you will be able to playback the asset in the Azure Media Player and select from two track options.  One stereo track and one 5.1 surround sound AAC track.

## Prerequisites

* Required Assemblies

* Azure.Identity
* Azure.ResourceManager.Media
* Azure.Storage.Blobs
* System.Linq.Async

* An Azure Media Services account. See the steps described in [Create a Media Services account](https://learn.microsoft.com/en-us/azure/media-services/latest/account-create-how-to).

## Build and run

Update the settings in **appsetting.json** in the root folder of the repository to match your Azure subscription, resource group and Media Services account.
Then build and run the sample in Visual Studio or VS Code.

## Next steps

* [Azure Media Services pricing](https://azure.microsoft.com/pricing/details/media-services/)
* [Azure Media Services v3 Documentation](https://learn.microsoft.com/azure/media-services/latest/)

