﻿using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Common_Utils;
using Microsoft.Azure.Management.Media;
using Microsoft.Azure.Management.Media.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EncodingH264ContentAwareConstrained
{
    public class Program
    {
        private const string OutputFolder = @"Output";
        private const string ContentAwareTransform = "ContenAwareEncoding";
        private const string InputMP4FileName = @"ignite.mp4";
        private const string DefaultStreamingEndpointName = "default";   // Change this to your Endpoint name.

        // Set this variable to true if you want to authenticate Interactively through the browser using your Azure user account
        private const bool UseInteractiveAuth = false;


        public static async Task Main(string[] args)
        {
            // If Visual Studio is used, let's read the .env file which should be in the root folder (same folder than the solution .sln file).
            // Same code will work in VS Code, but VS Code uses also launch.json to get the .env file.
            // You can create this ".env" file by saving the "sample.env" file as ".env" file and fill it with the right values.
            try
            {
                DotEnv.Load(".env");
            }
            catch
            {

            }

            ConfigWrapper config = new(new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables() // parses the values from the optional .env file at the solution root
                .Build());

            try
            {
                await RunAsync(config);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"{exception.Message}");

                if (exception.GetBaseException() is ErrorResponseException apiException)
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
                client = await Authentication.CreateMediaServicesClientAsync(config, UseInteractiveAuth);
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

            // Creating a unique suffix so that we don't have name collisions if you run the sample
            // multiple times without cleaning up.
            string uniqueness = Guid.NewGuid().ToString().Substring(0, 13);
            string jobName = $"job-{uniqueness}";
            string locatorName = $"locator-{uniqueness}";
            string inputAssetName = $"input-{uniqueness}";
            string outputAssetName = $"output-{uniqueness}";
            bool stopEndpoint = false;

            try
            {
                // Ensure that you have the Content Aware Encoding Transform ready to submit a job to.

                #region PresetConfigurations

                // This sample uses constraints on the CAE encoding preset to reduce the number of tracks output and resolutions to a specific range. 
                // First we will create a PresetConfigurations object to define the constraints that we want to use

                PresetConfigurations presetConfigurations = new PresetConfigurations(
                    // Allows you to configure the encoder settings to control the balance between speed and quality. Example: set Complexity as Speed for faster encoding but less compression efficiency.
                    complexity: Complexity.Speed,
                    // The output includes both audio and video.
                    interleaveOutput: InterleaveOutput.InterleavedOutput,
                    // The key frame interval in seconds. Example: set as 2 to reduce the playback buffering for some players.
                    keyFrameIntervalInSeconds: 2,
                    // The maximum bitrate in bits per second (threshold for the top video layer). Example: set MaxBitrateBps as 6000000 to avoid producing very high bitrate outputs for contents with high complexity.
                    maxBitrateBps : 6000000,
                    // The minimum bitrate in bits per second (threshold for the bottom video layer). Example: set MinBitrateBps as 200000 to have a bottom layer that covers users with low network bandwidth.
                    minBitrateBps : 200000,
                    maxHeight: 720,
                    // The minimum height of output video layers. Example: set MinHeight as 360 to avoid output layers of smaller resolutions like 180P.
                    minHeight: 240,
                    // The maximum number of output video layers. Example: set MaxLayers as 4 to make sure at most 4 output layers are produced to control the overall cost of the encoding job.
                    maxLayers: 3
                );
                
                var contentAwareEncodingPreset = new BuiltInStandardEncoderPreset(
                    configurations: presetConfigurations,
                    presetName: EncoderNamedPreset.ContentAwareEncoding           
                );

                Transform transform = await EnsureTransformExists(client,
                                                                config.ResourceGroup,
                                                                config.AccountName,
                                                                ContentAwareTransform,
                                                                preset: new BuiltInStandardEncoderPreset(EncoderNamedPreset.ContentAwareEncoding));

                #endregion PresetConfigurations

                // This is using th built-in Content Aware Encoding for H264 preset. You can choose from a variety of built-in presets.

                // Create a new input Asset and upload the specified local video file into it.
                Asset inputAsset = await CreateInputAssetAsync(client, config.ResourceGroup, config.AccountName, inputAssetName, InputMP4FileName);

                // Output from the Job must be written to an Asset, so let's create one
                Asset outputAsset = await CreateOutputAssetAsync(client, config.ResourceGroup, config.AccountName, outputAssetName);

                Job job = await SubmitJobAsync(client, config.ResourceGroup, config.AccountName, ContentAwareTransform, jobName, inputAsset.Name, outputAsset.Name);

                DateTime startedTime = DateTime.Now;

                // In this sample, we use Event Grid to listen to the notifications through an Azure Event Hub. 
                // If you do not provide an Event Hub config in the settings, the sample will fall back to polling the job for status. 
                // For production ready code, it is always recommended to use Event Grid instead of polling on the Job status. 

                EventProcessorClient processorClient = null;
                BlobContainerClient storageClient = null;
                MediaServicesEventProcessor mediaEventProcessor = null;

                try
                {
                    // First we will try to process Job events through Event Hub in real-time. If this fails for any reason,
                    // we will fall-back on polling Job status instead.

                    // Please refer README for Event Hub and storage settings.
                    // A storage account is required to process the Event Hub events from the Event Grid subscription in this sample.

                    // Create a new host to process events from an Event Hub.
                    Console.WriteLine("Creating a new client to process events from an Event Hub...");
                    var credential = new DefaultAzureCredential();
                    var storageConnectionString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}",
                       config.StorageAccountName, config.StorageAccountKey);
                    var blobContainerName = config.StorageContainerName;
                    var eventHubsConnectionString = config.EventHubConnectionString;
                    var eventHubName = config.EventHubName;
                    var consumerGroup = config.EventHubConsumerGroup;

                    storageClient = new BlobContainerClient(
                        storageConnectionString,
                        blobContainerName);

                    processorClient = new EventProcessorClient(
                        storageClient,
                        consumerGroup,
                        eventHubsConnectionString,
                        eventHubName);

                    // eventProcessorHost = new EventProcessorHost(config.EventHubName,
                    //    PartitionReceiver.DefaultConsumerGroupName, config.EventHubConnectionString,
                    //    storageConnectionString, config.StorageContainerName);

                    // Create an AutoResetEvent to wait for the job to finish and pass it to EventProcessor so that it can be set when a final state event is received.
                    AutoResetEvent jobWaitingEvent = new(false);

                    // Create a Task list, adding a job waiting task and a timer task. Other tasks can be added too.
                    IList<Task> tasks = new List<Task>();

                    // Add a task to wait for the job to finish. The AutoResetEvent will be set when a final state is received by EventProcessor.
                    Task jobTask = Task.Run(() =>
                    jobWaitingEvent.WaitOne());
                    tasks.Add(jobTask);

                    // 30 minutes timeout.
                    var cancellationSource = new CancellationTokenSource();
                    var timeout = Task.Delay(30 * 60 * 1000, cancellationSource.Token);

                    tasks.Add(timeout);
                    mediaEventProcessor = new MediaServicesEventProcessor(jobName, jobWaitingEvent, null);
                    processorClient.ProcessEventAsync += mediaEventProcessor.ProcessEventsAsync;
                    processorClient.ProcessErrorAsync += mediaEventProcessor.ProcessErrorAsync;

                    await processorClient.StartProcessingAsync(cancellationSource.Token);

                    // Wait for tasks.
                    if (await Task.WhenAny(tasks) == jobTask)
                    {
                        // Job finished. Cancel the timer.
                        cancellationSource.Cancel();
                        // Get the latest status of the job.
                        job = await client.Jobs.GetAsync(config.ResourceGroup, config.AccountName, ContentAwareTransform, jobName);
                    }
                    else
                    {
                        // Timeout happened, Something might be wrong with job events. Fall-back on polling instead.
                        jobWaitingEvent.Set();
                        throw new Exception("Timeout occurred.");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Warning: Failed to connect to Event Hub, please refer README for Event Hub and storage settings.");
                    Console.WriteLine(e.Message);

                    // Polling is not a recommended best practice for production applications because of the latency it introduces.
                    // Overuse of this API may trigger throttling. Developers should instead use Event Grid and listen for the status events on the jobs
                    Console.WriteLine("Polling job status...");
                    job = await WaitForJobToFinishAsync(client, config.ResourceGroup, config.AccountName, ContentAwareTransform, jobName);
                }
                finally
                {
                    if (processorClient != null)
                    {
                        Console.WriteLine("Job final state received, Stopping the event processor...");
                        await processorClient.StopProcessingAsync();
                        Console.WriteLine();

                        // It is encouraged that you unregister your handlers when you have
                        // finished using the Event Processor to ensure proper cleanup.  This
                        // is especially important when using lambda expressions or handlers
                        // in any form that may contain closure scopes or hold other references.
                        processorClient.ProcessEventAsync -= mediaEventProcessor.ProcessEventsAsync;
                        processorClient.ProcessErrorAsync -= mediaEventProcessor.ProcessErrorAsync;
                    }

                }


                TimeSpan elapsed = DateTime.Now - startedTime;

                if (job.State == JobState.Finished)
                {
                    Console.WriteLine("Job finished.");
                    if (!Directory.Exists(OutputFolder))
                        Directory.CreateDirectory(OutputFolder);
                    DownloadResults(client, config.ResourceGroup, config.AccountName, outputAsset.Name, OutputFolder).Wait();

                    StreamingLocator locator = await CreateStreamingLocatorAsync(client, config.ResourceGroup, config.AccountName, outputAsset.Name, locatorName);

                    StreamingEndpoint streamingEndpoint = await client.StreamingEndpoints.GetAsync(config.ResourceGroup, config.AccountName,
                        DefaultStreamingEndpointName);

                    if (streamingEndpoint != null)
                    {
                        if (streamingEndpoint.ResourceState != StreamingEndpointResourceState.Running)
                        {
                            Console.WriteLine("Streaming Endpoint was Stopped, restarting now..");
                            await client.StreamingEndpoints.StartAsync(config.ResourceGroup, config.AccountName, DefaultStreamingEndpointName);

                            // Since we started the endpoint, we should stop it in cleanup.
                            stopEndpoint = true;
                        }
                    }

                    Console.WriteLine();
                    Console.WriteLine("Getting the Streaming manifest URLs for HLS and DASH:");
                    IList<string> urls = await GetStreamingUrlsAsync(client, config.ResourceGroup, config.AccountName, locator.Name, streamingEndpoint);


                    Console.WriteLine("To try streaming, copy and paste the Streaming URL into the Azure Media Player at 'http://aka.ms/azuremediaplayer'.");
                    Console.WriteLine("When finished, press ENTER to cleanup.");
                    Console.WriteLine();
                    Console.Out.Flush();
                    Console.ReadLine();
                }
                else if (job.State == JobState.Error)
                {
                    Console.WriteLine($"ERROR: Job finished with error message: {job.Outputs[0].Error.Message}");
                    Console.WriteLine($"ERROR:                   error details: {job.Outputs[0].Error.Details[0].Message}");
                }
            }
            catch (ErrorResponseException e)
            {
                Console.WriteLine("ErrorResponseException");
                Console.WriteLine($"\tCode: {e.Body.Error.Code}");
                Console.WriteLine($"\tMessage: {e.Body.Error.Message}");
                Console.WriteLine();
                Console.WriteLine("Exiting, cleanup may be necessary...");
                Console.ReadLine();
            }
            finally
            {
                Console.WriteLine("Cleaning up...");
                await CleanUpAsync(client, config.ResourceGroup, config.AccountName, ContentAwareTransform, jobName, inputAssetName, outputAssetName,
                    locatorName, stopEndpoint, DefaultStreamingEndpointName);


            }
        }

        #region EnsureTransformExists
        /// <summary>
        /// If the specified transform exists, get that transform. If the it does not exist, creates a new transform
        /// with the specified output. In this case, the output is set to encode a video using the passed in preset.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName">The Media Services account name.</param>
        /// <param name="transformName">The name of the transform.</param>
        /// <param name="preset">The preset.</param>
        /// <returns>The transform found or created.</returns>
        private static async Task<Transform> EnsureTransformExists(IAzureMediaServicesClient client, string resourceGroupName,
            string accountName, string transformName, Preset preset)
        {

            TransformOutput[] outputs = new TransformOutput[]
            {
                    new TransformOutput(preset),
            };

            Console.WriteLine("Creating a transform...");
            // Does a Transform already exist with the desired name? This method will just overwrite (Update) the Transform if it exists already. 
            // In production code, you may want to be cautious about that. It really depends on your scenario.
            Transform transform = await client.Transforms.CreateOrUpdateAsync(resourceGroupName, accountName, transformName, outputs);

            return transform;
        }
        #endregion EnsureTransformExists

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
            Asset outputAsset = new Asset();

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
            // to get the existing job. In Media Services v3, Get methods on entities returns ErrorResponseException 
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
                if (exception.GetBaseException() is ErrorResponseException apiException)
                {
                    Console.Error.WriteLine(
                        $"ERROR: API call failed with error code '{apiException.Body.Error.Code}' and message '{apiException.Body.Error.Message}'.");
                }
                throw;
            }

            return job;
        }


        /// <summary>
        /// Polls Media Services for the status of the Job.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="transformName">The name of the transform.</param>
        /// <param name="jobName">The name of the job you submitted.</param>
        /// <returns></returns>
        private static async Task<Job> WaitForJobToFinishAsync(IAzureMediaServicesClient client,
            string resourceGroupName,
            string accountName,
            string transformName,
            string jobName)
        {
            const int SleepIntervalMs = 30 * 1000;

            Job job;

            do
            {
                job = await client.Jobs.GetAsync(resourceGroupName, accountName, transformName, jobName);

                Console.WriteLine($"Job is '{job.State}'.");
                for (int i = 0; i < job.Outputs.Count; i++)
                {
                    JobOutput output = job.Outputs[i];
                    Console.Write($"\tJobOutput[{i}] is '{output.State}'.");
                    if (output.State == JobState.Processing)
                    {
                        Console.Write($"  Progress: '{output.Progress}'.");
                    }

                    Console.WriteLine();
                }

                if (job.State != JobState.Finished && job.State != JobState.Error && job.State != JobState.Canceled)
                {
                    await Task.Delay(SleepIntervalMs);
                }
            }
            while (job.State != JobState.Finished && job.State != JobState.Error && job.State != JobState.Canceled);

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
            // to get the existing asset. In Media Services v3, the Get method on entities will return an ErrorResponseException if the resource is not found. 
            Asset asset = await client.Assets.CreateOrUpdateAsync(resourceGroupName, accountName, assetName, new Asset());

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
            BlobContainerClient container = new(sasUri);
            BlobClient blob = container.GetBlobClient(Path.GetFileName(fileToUpload));

            // Use Storage API to upload the file into the container in storage.
            Console.WriteLine("Uploading a media file to the asset...");
            await blob.UploadAsync(fileToUpload);
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
            AssetContainerSas assetContainerSas = client.Assets.ListContainerSas(
                            resourceGroupName,
                            accountName,
                            assetName,
                            permissions: AssetContainerPermission.Read,
                            expiryTime: DateTime.UtcNow.AddHours(1).ToUniversalTime()
                            );

            Uri containerSasUrl = new(assetContainerSas.AssetContainerSasUrls.FirstOrDefault());
            BlobContainerClient container = new(containerSasUrl);

            string directory = Path.Combine(outputFolderName, assetName);
            Directory.CreateDirectory(directory);

            Console.WriteLine("Downloading results to {0}.", directory);

            string continuationToken = null;

            // Call the listing operation and enumerate the result segment.
            // When the continuation token is empty, the last segment has been returned
            // and execution can exit the loop.
            do
            {
                var resultSegment = container.GetBlobs().AsPages(continuationToken);

                foreach (Azure.Page<BlobItem> blobPage in resultSegment)
                {
                    foreach (BlobItem blobItem in blobPage.Values)
                    {

                        var blobClient = container.GetBlobClient(blobItem.Name);
                        string filename = Path.Combine(directory, blobItem.Name);
                        await blobClient.DownloadToAsync(filename);
                    }

                    // Get the continuation token and loop until it is empty.
                    continuationToken = blobPage.ContinuationToken;
                }

            } while (continuationToken != "");

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
        /// <param name="streamingEndpoint">The streaming endpoint.</param>
        /// <returns></returns>
        private static async Task<IList<string>> GetStreamingUrlsAsync(
            IAzureMediaServicesClient client,
            string resourceGroupName,
            string accountName,
            String locatorName,
            StreamingEndpoint streamingEndpoint)
        {
            IList<string> streamingUrls = new List<string>();

            ListPathsResponse paths = await client.StreamingLocators.ListPathsAsync(resourceGroupName, accountName, locatorName);

            foreach (StreamingPath path in paths.StreamingPaths)
            {
                Console.WriteLine($"The following formats are available for {path.StreamingProtocol.ToString().ToUpper()}:");
                foreach (string streamingFormatPath in path.Paths)
                {
                    UriBuilder uriBuilder = new()
                    {
                        Scheme = "https",
                        Host = streamingEndpoint.HostName,

                        Path = streamingFormatPath
                    };
                    Console.WriteLine($"\t{uriBuilder}");
                    streamingUrls.Add(uriBuilder.ToString());
                }
                Console.WriteLine();
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
        /// <param name="stopEndpoint">Stop endpoint if true, keep endpoint running if false.</param>
        /// <param name="streamingEndpointName">The endpoint name.</param>
        /// <returns>A task.</returns>
        private static async Task CleanUpAsync(IAzureMediaServicesClient client, string resourceGroupName, string accountName,
            string transformName, string jobName, string inputAssetName, string outputAssetName, string streamingLocatorName,
            bool stopEndpoint, string streamingEndpointName)
        {
            await client.Jobs.DeleteAsync(resourceGroupName, accountName, transformName, jobName);
            await client.Assets.DeleteAsync(resourceGroupName, accountName, inputAssetName);
            await client.Assets.DeleteAsync(resourceGroupName, accountName, outputAssetName);
            await client.StreamingLocators.DeleteAsync(resourceGroupName, accountName, streamingLocatorName);

            if (stopEndpoint)
            {
                // Because we started the endpoint, we'll stop it.
                await client.StreamingEndpoints.StopAsync(resourceGroupName, accountName, streamingEndpointName);
            }
            else
            {
                // We will keep the endpoint running because it was not started by us. There are costs to keep it running.
                // Please refer https://azure.microsoft.com/en-us/pricing/details/media-services/ for pricing. 
                Console.WriteLine($"The endpoint '{streamingEndpointName}' is running. To halt further billing on the endpoint, please stop it in azure portal or AMS Explorer.");
            }
        }
    }
}
