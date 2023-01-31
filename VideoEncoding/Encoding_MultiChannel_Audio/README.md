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
                var trackList = new List<TrackDescriptor>
                {
                       new SelectAudioTrackById()
                        {
                            TrackId = 1,
                            ChannelMapping = ChannelMapping.StereoLeft
                        },
                        new SelectAudioTrackById()
                        {
                            TrackId = 2,
                            ChannelMapping = ChannelMapping.StereoRight
                        },
                        new SelectAudioTrackById()
                        {
                            TrackId = 3,
                            ChannelMapping = ChannelMapping.FrontLeft
                        },
                        new SelectAudioTrackById()
                        {
                            TrackId = 4,
                            ChannelMapping = ChannelMapping.FrontRight
                        },
                        new SelectAudioTrackById()
                        {
                            TrackId = 5,
                            ChannelMapping = ChannelMapping.Center
                        },
                        new SelectAudioTrackById()
                        {
                            TrackId = 6,
                            ChannelMapping = ChannelMapping.LowFrequencyEffects
                        },
                        new SelectAudioTrackById()
                        {
                            TrackId = 7,
                            ChannelMapping = ChannelMapping.BackLeft
                        },
                        new SelectAudioTrackById()
                        {
                            TrackId = 8,
                            ChannelMapping = ChannelMapping.BackRight
                        }
                };
```

A Transform is then created to generate the Stereo and 5.1 surround sound tracks from the track descriptor list. The first two tracks in the track descriptor list will be output to the Stereo AAC output defined as 2 channels in the Transform. The remaining 6 tracks will go into the second AAC output defined in the transform as using 6 channels.

```csharp
                  new TransformOutput(
                        new StandardEncoderPreset(
                            codecs: new Codec[]
                            {
                                // Add an AAC Audio layer for the audio encoding of the Stereo tracks to be mapped to.
                                new AacAudio(
                                    channels: 2, // stereo track
                                    samplingRate: 48000,
                                    bitrate: 128000,
                                    profile: AacAudioProfile.AacLc
                                ),
                                 new AacAudio(
                                    channels: 6, // 5.1 surround sound track 
                                    samplingRate: 48000,
                                    bitrate: 320000,
                                    profile: AacAudioProfile.AacLc
                                ),
```

After encoding, you will be able to playback the asset in the Azure Media Player and select from two track options.  One stereo track and one 5.1 surround sound AAC track.

## Prerequisites

* Required Assemblies

* Azure.Storage.Blobs
* Microsoft.Azure.Management.Media
* Microsoft.Extensions.Hosting
* Microsoft.Identity.Client

* An Azure Media Services account. See the steps described in [Create a Media Services account](https://docs.microsoft.com/en-us/azure/media-services/latest/account-create-how-to).

## Build and run

Update **appsettings.json** in the project folder OR create a **.env file** at the root of the solution with your account settings. Please choose one of these two methods.
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
