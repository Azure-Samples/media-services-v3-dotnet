// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Media;
using Microsoft.Azure.Management.Media.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Rest;
using Microsoft.Rest.Azure.Authentication;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace EncodingWithMESCustomPreset
{
    public class Program
    {
        const String outputFolder = @"Output";
        const String transformName = "Custom_TwoLayerMp4_Png";
        private const string InputMP4FileName = @"ignite.mp4";

        public static async Task Main(string[] args)
        {
            ConfigWrapper config = new ConfigWrapper(new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build());

            try
            {
                await RunAsync(config);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"{exception.Message}");

                ApiErrorException apiException = exception.GetBaseException() as ApiErrorException;
                if (apiException != null)
                {
                    Console.Error.WriteLine(
                        $"ERROR: API call failed with error code '{apiException.Body.Error.Code}' and message '{apiException.Body.Error.Message}'.");
                }
            }

            Console.WriteLine("Press Enter to continue.");
            Console.ReadLine();
        }

        /// <summary>
        /// Run the sample async.
        /// </summary>
        /// <param name="config">The param is of type ConfigWrapper. This class reads values from local configuration file.</param>
        /// <returns></returns>
        private static async Task RunAsync(ConfigWrapper config)
        {
            IAzureMediaServicesClient client;
            try
            {
                client = await CreateMediaServicesClientAsync(config);
            }
            catch (Exception e)
            {
                if (e.Source.Contains("ActiveDirectory"))
                {
                    Console.Error.WriteLine("TIP: Make sure that you have filled out the appsettings.json file before running this sample.");
                    Console.Error.WriteLine();
                }
                Console.Error.WriteLine($"{e.Message}");
                return;
            }

            // Set the polling interval for long running operations to 2 seconds.
            // The default value is 30 seconds for the .NET client SDK
            client.LongRunningOperationRetryTimeout = 2;

            try
            {
                // Ensure that you have customized encoding Transform.  This is really a one time setup operation.
                Transform transform = await CreateCustomTransform(client, config.ResourceGroup, config.AccountName, transformName);

                // Creating a unique suffix so that we don't have name collisions if you run the sample
                // multiple times without cleaning up.
                string uniqueness = Guid.NewGuid().ToString().Substring(0, 13);
                string jobName = "job-" + uniqueness;
                string locatorName = "locator-" + uniqueness;
                string inputAssetName = "input-" + uniqueness;
                string outputAssetName = "output-" + uniqueness;

                // Create a new input Asset and upload the specified local video file into it.
                Asset inputAsset = await CreateInputAssetAsync(client, config.ResourceGroup, config.AccountName, inputAssetName, InputMP4FileName);

                // Out from the Job must be written to an Asset, so let's create one
                Asset outputAsset = await CreateOutputAssetAsync(client, config.ResourceGroup, config.AccountName, outputAssetName);

                Job job = await SubmitJobAsync(client, config.ResourceGroup, config.AccountName, transformName, jobName, inputAsset.Name, outputAsset.Name);

                DateTime startedTime = DateTime.Now;
                // In this demo code, we will poll for Job status
                // Polling is not a recommended best practice for production applications because of the latency it introduces.
                // Overuse of this API may trigger throttling.
                job = WaitForJobToFinish(client, config.ResourceGroup, config.AccountName, transformName, jobName);

                TimeSpan elapsed = DateTime.Now - startedTime;

                if (job.State == JobState.Finished)
                {
                    Console.WriteLine("Job finished.");
                    if (!Directory.Exists(outputFolder))
                        Directory.CreateDirectory(outputFolder);
                    DownloadResults(client, config.ResourceGroup, config.AccountName, outputAsset.Name, outputFolder).Wait();

                    StreamingLocator locator = await CreateStreamingLocatorAsync(client, config.ResourceGroup, config.AccountName, outputAsset.Name, locatorName);

                    Console.WriteLine();
                    Console.WriteLine("Streaming urls:");
                    IList<string> urls = await GetStreamingUrlsAsync(client, config.ResourceGroup, config.AccountName, locator.Name);
                    foreach (var url in urls)
                    {
                        Console.WriteLine(url);
                    }

                    Console.WriteLine("To try streaming, copy and paste the Streaming URL into the Azure Media Player at 'http://aka.ms/azuremediaplayer'.");
                    Console.WriteLine("When finished, press ENTER to cleanup.");
                    Console.WriteLine();
                    Console.Out.Flush();
                    Console.ReadLine();

                    await CleanUpAsync(client, config.ResourceGroup, config.AccountName, transformName, job.Name, outputAsset.Name, inputAsset.Name, locatorName);
                    Console.WriteLine("Done.");
                }
                else if (job.State == JobState.Error)
                {
                    Console.WriteLine($"ERROR: Job finished with error message: {job.Outputs[0].Error.Message}");
                    Console.WriteLine($"ERROR:                   error details: {job.Outputs[0].Error.Details[0].Message}");
                }
            }
            catch(ApiErrorException ex)
            {
                string code = ex.Body.Error.Code;
                string message = ex.Body.Error.Message;

                Console.WriteLine("ERROR:API call failed with error code: {0} and message: {1}", code, message);
            }          
        }

        /// <summary>
        /// Create the ServiceClientCredentials object based on the credentials
        /// supplied in local configuration file.
        /// </summary>
        /// <param name="config">The param is of type ConfigWrapper. This class reads values from local configuration file.</param>
        /// <returns></returns>
        private static async Task<ServiceClientCredentials> GetCredentialsAsync(ConfigWrapper config)
        {
            // Use ApplicationTokenProvider.LoginSilentAsync to get a token using a service principal with symmetric key
            ClientCredential clientCredential = new ClientCredential(config.AadClientId, config.AadSecret);
            return await ApplicationTokenProvider.LoginSilentAsync(config.AadTenantId, clientCredential, ActiveDirectoryServiceSettings.Azure);
        }

        /// <summary>
        /// Creates the AzureMediaServicesClient object based on the credentials
        /// supplied in local configuration file.
        /// </summary>
        /// <param name="config">The param is of type ConfigWrapper. This class reads values from local configuration file.</param>
        /// <returns></returns>
        private static async Task<IAzureMediaServicesClient> CreateMediaServicesClientAsync(ConfigWrapper config)
        {
            var credentials = await GetCredentialsAsync(config);

            return new AzureMediaServicesClient(config.ArmEndpoint, credentials)
            {
                SubscriptionId = config.SubscriptionId,
            };
        }

        /// <summary>
        /// If the specified transform exists, return that transform. If the it does not
        /// exist, creates a new transform with the specified output. In this case, the
        /// output is set to encode a video using a custom preset.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="transformName">The transform name.</param>
        /// <returns></returns>
        private static async Task<Transform> CreateCustomTransform(IAzureMediaServicesClient client, string resourceGroupName, string accountName, string transformName)
        {
            // Does a transform already exist with the desired name? Assume that an existing Transform with the desired name
            // also uses the same recipe or Preset for processing content.
            Transform transform = client.Transforms.Get(resourceGroupName, accountName, transformName);

            if (transform == null)
            {
                Console.WriteLine("Creating a custom transform...");
                // Create a new Transform Outputs array - this defines the set of outputs for the Transform
                TransformOutput[] outputs = new TransformOutput[]
                {
                    // Create a new TransformOutput with a custom Standard Encoder Preset
                    // This demonstrates how to create custom codec and layer output settings

                  new TransformOutput(
                        new StandardEncoderPreset(
                            codecs: new Codec[]
                            {
                                // Add an AAC Audio layer for the audio encoding
                                new AacAudio(
                                    channels: 2,
                                    samplingRate: 48000,
                                    bitrate: 128000,
                                    profile: AacAudioProfile.AacLc
                                ),
                                // Next, add a H264Video for the video encoding
                               new H264Video (
                                    // Set the GOP interval to 2 seconds for both H264Layers
                                    keyFrameInterval:TimeSpan.FromSeconds(2),
                                     // Add H264Layers, one at HD and the other at SD. Assign a label that you can use for the output filename
                                    layers:  new H264Layer[]
                                    {
                                        new H264Layer (
                                            bitrate: 1000000, // Units are in bits per second
                                            width: "1280",
                                            height: "720",
                                            label: "HD" // This label is used to modify the file name in the output formats
                                        ),
                                        new H264Layer (
                                            bitrate: 600000, 
                                            width: "640",
                                            height: "360",
                                            label: "SD"
                                        )
                                    }
                                ),
                                // Also generate a set of PNG thumbnails
                                new PngImage(
                                    start: "25%",
                                    step: "25%",
                                    range: "80%",
                                    layers: new PngLayer[]{
                                        new PngLayer(
                                            width: "50%", 
                                            height: "50%"
                                        )
                                    }
                                )
                            },
                            // Specify the format for the output files - one for video+audio, and another for the thumbnails
                            formats: new Format[]
                            {
                                // Mux the H.264 video and AAC audio into MP4 files, using basename, label, bitrate and extension macros
                                // Note that since you have multiple H264Layers defined above, you have to use a macro that produces unique names per H264Layer
                                // Either {Label} or {Bitrate} should suffice
                                 
                                new Mp4Format(
                                    filenamePattern:"Video-{Basename}-{Label}-{Bitrate}{Extension}"
                                ),
                                new PngFormat(
                                    filenamePattern:"Thumbnail-{Basename}-{Index}{Extension}"
                                )
                            }
                        ),
                        onError: OnErrorType.StopProcessingJob,
                        relativePriority: Priority.Normal
                    )
                };

                string description = "A simple custom encoding transform with 2 MP4 bitrates";
                // Create the custom Transform with the outputs defined above
                transform = await client.Transforms.CreateOrUpdateAsync(resourceGroupName, accountName, transformName, outputs, description);
            }

            return transform;
        }

        /// <summary>
        /// Creates an output asset. The output from the encoding Job must be written to an Asset.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="assetName">The output asset name.</param>
        /// <returns></returns>
        private static async Task<Asset> CreateOutputAssetAsync(IAzureMediaServicesClient client, string resourceGroupName, string accountName, string assetName)
        {
            // Check if an Asset already exists
            Asset outputAsset = await client.Assets.GetAsync(resourceGroupName, accountName, assetName);

            if (outputAsset != null)
            {
                // The asset already exists and we are going to overwrite it. In your application, if you don't want to overwrite
                // an existing asset, use an unique name.
                Console.WriteLine($"Warning: The asset named {assetName} already exists. It will be overwritten in this sample.");
            }
            else
            {
                Console.WriteLine("Creating an output asset..");
                outputAsset = new Asset();
            }

            return await client.Assets.CreateOrUpdateAsync(resourceGroupName, accountName, assetName, outputAsset);
        }

        /// <summary>
        /// Submits a request to Media Services to apply the specified Transform to a given input video.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="transformName">The name of the transform.</param>
        /// <param name="jobName">The (unique) name of the job.</param>
        /// <param name="inputAssetName"></param>
        /// <param name="outputAssetName">The (unique) name of the  output asset that will store the result of the encoding job. </param>
        private static async Task<Job> SubmitJobAsync(IAzureMediaServicesClient client,
            string resourceGroupName,
            string accountName,
            string transformName,
            string jobName,
            string inputAssetName,
            string outputAssetName)
        {
            JobInput jobInput = new JobInputAsset(assetName: inputAssetName);

            JobOutput[] jobOutputs =
            {
                new JobOutputAsset(outputAssetName),
            };

            // In this example, we are assuming that the job name is unique.
            //
            // If you already have a job with the desired name, use the Jobs.Get method
            // to get the existing job. In Media Services v3, Get methods on entities returns null 
            // if the entity doesn't exist (a case-insensitive check on the name).
            Job job;
            try
            {
                Console.WriteLine("Creating a job...");
                job = await client.Jobs.CreateAsync(
                         resourceGroupName,
                         accountName,
                         transformName,
                         jobName,
                         new Job
                         {
                             Input = jobInput,
                             Outputs = jobOutputs,
                         });
            }
            catch (Exception exception)
            {
                ApiErrorException apiException = exception.GetBaseException() as ApiErrorException;
                if (apiException != null)
                {
                    Console.Error.WriteLine(
                        $"ERROR: API call failed with error code '{apiException.Body.Error.Code}' and message '{apiException.Body.Error.Message}'.");
                }
                throw exception;
            }

            return job;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="transformName">The name of the transform.</param>
        /// <param name="jobName">The name of the job you submitted.</param>
        /// <returns></returns>
        private static Job WaitForJobToFinish(IAzureMediaServicesClient client, string resourceGroupName, string accountName, string transformName, string jobName)
        {
            const int SleepInterval = 10 * 1000;

            Job job = null;
            bool exit = false;

            do
            {
                job = client.Jobs.Get(resourceGroupName, accountName, transformName, jobName);
                
                if (job.State == JobState.Finished || job.State == JobState.Error || job.State == JobState.Canceled)
                {
                    exit = true;
                }
                else
                {
                    Console.WriteLine($"Job is {job.State}.");

                    for (int i = 0; i < job.Outputs.Count; i++)
                    {
                        JobOutput output = job.Outputs[i];

                        Console.Write($"\tJobOutput[{i}] is {output.State}.");

                        if (output.State == JobState.Processing)
                        {
                            Console.Write($"  Progress: {output.Progress}");
                        }

                        Console.WriteLine();
                    }

                    System.Threading.Thread.Sleep(SleepInterval);
                }
            }
            while (!exit);

            return job;
        }

        /// <summary>
        /// Creates a new input Asset and uploads the specified local video file into it.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="assetName">The asset name.</param>
        /// <param name="fileToUpload">The file you want to upload into the asset.</param>
        /// <returns></returns>
        private static async Task<Asset> CreateInputAssetAsync(
            IAzureMediaServicesClient client,
            string resourceGroupName,
            string accountName,
            string assetName,
            string fileToUpload)
        {
            // In this example, we are assuming that the asset name is unique.
            //
            // If you already have an asset with the desired name, use the Assets.Get method
            // to get the existing asset. In Media Services v3, the Get method on entities returns null 
            // if the entity doesn't exist (a case-insensitive check on the name).
            Asset asset = await client.Assets.GetAsync(resourceGroupName, accountName, assetName);

            if (asset == null)
            {
                // Call Media Services API to create an Asset.
                // This method creates a container in storage for the Asset.
                // The files (blobs) associated with the asset will be stored in this container.
                Console.WriteLine("Creating an input asset...");
                asset = await client.Assets.CreateOrUpdateAsync(resourceGroupName, accountName, assetName, new Asset());
            }
            else
            {
                // The asset already exists and we are going to overwrite it. In your application, if you don't want to overwrite
                // an existing asset, use an unique name.
                Console.WriteLine($"Warning: The asset named {assetName} already exists. It will be overwritten in this sample.");
            }

            // Use Media Services API to get back a response that contains
            // SAS URL for the Asset container into which to upload blobs.
            // That is where you would specify read-write permissions 
            // and the expiration time for the SAS URL.
            var response = await client.Assets.ListContainerSasAsync(
                resourceGroupName,
                accountName,
                assetName,
                permissions: AssetContainerPermission.ReadWrite,
                expiryTime: DateTime.UtcNow.AddHours(4).ToUniversalTime());

            var sasUri = new Uri(response.AssetContainerSasUrls.First());

            // Use Storage API to get a reference to the Asset container
            // that was created by calling Asset's CreateOrUpdate method.  
            CloudBlobContainer container = new CloudBlobContainer(sasUri);
            var blob = container.GetBlockBlobReference(Path.GetFileName(fileToUpload));

            // Use Storage API to upload the file into the container in storage.
            Console.WriteLine("Uploading a video file...");
            await blob.UploadFromFileAsync(fileToUpload);

            return asset;
        }

        /// <summary>
        /// Downloads the specified output asset.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="assetName">The output asset.</param>
        /// <param name="outputFolderName">The name of the folder into which to download the results.</param>
        /// <returns></returns>
        private async static Task DownloadResults(IAzureMediaServicesClient client, string resourceGroupName, string accountName,
            string assetName, string outputFolderName)
        {
            // Use Media Service and Storage APIs to download the output files to a local folder
            ListContainerSasInput parameters = new ListContainerSasInput();
            AssetContainerSas assetContainerSas = client.Assets.ListContainerSas(
                            resourceGroupName, 
                            accountName, 
                            assetName,
                            permissions: AssetContainerPermission.Read, 
                            expiryTime: DateTime.UtcNow.AddHours(1).ToUniversalTime()
                            );

            Uri containerSasUrl = new Uri(assetContainerSas.AssetContainerSasUrls.FirstOrDefault());
            CloudBlobContainer container = new CloudBlobContainer(containerSasUrl);

            string directory = Path.Combine(outputFolderName, assetName);
            Directory.CreateDirectory(directory);

            Console.WriteLine("Downloading results to {0}.", directory);
            
            var blobs = container.ListBlobsSegmentedAsync(null,true, BlobListingDetails.None,200,null,null,null).Result;
            
            foreach (var blobItem in blobs.Results)
            {
                if (blobItem is CloudBlockBlob)
                {
                    CloudBlockBlob blob = blobItem as CloudBlockBlob;
                    string filename = Path.Combine(directory, blob.Name);

                    await blob.DownloadToFileAsync(filename, FileMode.Create);
                }
            }

            Console.WriteLine("Download complete.");
        }

        /// <summary>
        /// Creates a StreamingLocator for the specified asset and with the specified streaming policy name.
        /// Once the StreamingLocator is created the output asset is available to clients for playback.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="assetName">The name of the output asset.</param>
        /// <param name="locatorName">The StreamingLocator name (unique in this case).</param>
        /// <returns></returns>
        private static async Task<StreamingLocator> CreateStreamingLocatorAsync(
            IAzureMediaServicesClient client,
            string resourceGroup,
            string accountName,
            string assetName,
            string locatorName)
        {
            StreamingLocator locator = await client.StreamingLocators.CreateAsync(
                resourceGroup,
                accountName,
                locatorName,
                new StreamingLocator
                {
                    AssetName = assetName,
                    StreamingPolicyName = PredefinedStreamingPolicy.ClearStreamingOnly
                });

            return locator;
        }

        /// <summary>
        /// Checks if the streaming endpoint is in the running state,
        /// if not, starts it.
        /// Then, builds the streaming URLs.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="locatorName">The name of the StreamingLocator that was created.</param>
        /// <returns></returns>
        private static async Task<IList<string>> GetStreamingUrlsAsync(
            IAzureMediaServicesClient client,
            string resourceGroupName,
            string accountName,
            String locatorName)
        {
            const string DefaultStreamingEndpointName = "se";

            IList<string> streamingUrls = new List<string>();

            StreamingEndpoint streamingEndpoint = await client.StreamingEndpoints.GetAsync(resourceGroupName, accountName, DefaultStreamingEndpointName);

            if (streamingEndpoint != null)
            {
                if (streamingEndpoint.ResourceState != StreamingEndpointResourceState.Running)
                {
                    await client.StreamingEndpoints.StartAsync(resourceGroupName, accountName, DefaultStreamingEndpointName);
                }
            }

            ListPathsResponse paths = await client.StreamingLocators.ListPathsAsync(resourceGroupName, accountName, locatorName);

            foreach (StreamingPath path in paths.StreamingPaths)
            {
                UriBuilder uriBuilder = new UriBuilder();
                uriBuilder.Scheme = "https";
                uriBuilder.Host = streamingEndpoint.HostName;

                uriBuilder.Path = path.Paths[0];
                streamingUrls.Add(uriBuilder.ToString());
            }

            return streamingUrls;
        }

        /// <summary>
        /// Delete the job and assets and streaming locator that were created.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="transformName">The transform name.</param>
        /// <param name="jobName">The job name.</param>
        /// <param name="inputAssetName">The input asset name.</param>
        /// <param name="outputAssetName">The output asset name.</param>
        /// <param name="streamingLocatorName">The streaming locator name. </param>
        /// <returns>A task.</returns>
        private static async Task CleanUpAsync(IAzureMediaServicesClient client, string resourceGroupName, string accountName,
            string transformName, string jobName, string inputAssetName, string outputAssetName, string streamingLocatorName)
        {
            Console.WriteLine("Cleaning up...");

            await client.Jobs.DeleteAsync(resourceGroupName, accountName, transformName, jobName);
            await client.Assets.DeleteAsync(resourceGroupName, accountName, inputAssetName);
            await client.Assets.DeleteAsync(resourceGroupName, accountName, outputAssetName);
            await client.StreamingLocators.DeleteAsync(resourceGroupName, accountName, streamingLocatorName);
        }
    }
}
