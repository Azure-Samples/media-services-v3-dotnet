// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Common_Utils;
using Microsoft.Azure.Management.Media;
using Microsoft.Azure.Management.Media.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AudioAnalyzer
{
    class Program
    {
        private const string AudioAnalyzerTransformName = "MyAudioAnalyzerTransformName";
        private const string InputMP4FileName = @"ignite.mp4";
        private const string OutputFolderName = @"Output";

        // Set this variable to true if you want to authenticate Interactively through the browser using your Azure user account
        private const bool UseInteractiveAuth = false;

        /// <summary>
        /// The main method of the sample. Please make sure you have set settings in appsettings.json or in the .env file in the root folder
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
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
                Console.Error.WriteLine("TIP: Make sure that you have filled out the appsettings.json or .env file before running this sample.");
                Console.Error.WriteLine($"{e.Message}");
                return;
            }

            // Set the polling interval for long running operations to 2 seconds.
            // The default value is 30 seconds for the .NET client SDK
            client.LongRunningOperationRetryTimeout = 2;

            // Creating a unique suffix so that we don't have name collisions if you run the sample
            // multiple times without cleaning up.
            string uniqueness = Guid.NewGuid().ToString("N");
            string jobName = $"job-{uniqueness}";
            string outputAssetName = $"output-{uniqueness}";
            string inputAssetName = $"input-{uniqueness}";

            // Create an AudioAnalyzer preset with audio insights and Basic audio mode.
            Preset preset = new AudioAnalyzerPreset(
                audioLanguage: "en-US",
                //
                // There are two modes available, Basic and Standard
                // Basic : This mode performs speech-to-text transcription and generation of a VTT subtitle/caption file. 
                //         The output of this mode includes an Insights JSON file including only the keywords, transcription,and timing information. 
                //         Automatic language detection and speaker diarization are not included in this mode.
                // Standard : Performs all operations included in the Basic mode, additionally performing language detection and speaker diarization.
                //
                mode: AudioAnalysisMode.Standard
            );

            // Ensure that you have the desired encoding Transform. This is really a one time setup operation.
            // Once it is created, we won't delete it.
            Transform audioAnalyzerTransform = await GetOrCreateTransformAsync(client, config.ResourceGroup, config.AccountName, AudioAnalyzerTransformName, preset);

            // Create a new input Asset and upload the specified local media file into it.
            await CreateInputAssetAsync(client, config.ResourceGroup, config.AccountName, inputAssetName, InputMP4FileName);

            // Output from the encoding Job must be written to an Asset, so let's create one
            Asset outputAsset = await CreateOutputAssetAsync(client, config.ResourceGroup, config.AccountName, outputAssetName);

            // Use a preset override to change the language or mode on the Job level.
            // Above we created a Transform with a preset that was set to a specific audio language and mode. 
            // If we want to change that language or mode before submitting the job, we can modify it using the PresetOverride property 
            // on the JobOutput.

            #region PresetOverride
            // First we re-define the preset that we want to use for this specific Job...
            var presetOverride = new AudioAnalyzerPreset
            {
                AudioLanguage = "en-US",
                Mode = AudioAnalysisMode.Basic  /// Switch this job to use Basic mode instead of standard
            };

            // Then we use the PresetOverride property of the JobOutput to pass in the override values to use on this single Job 
            // without the need to create a completely separate and new Transform with another langauge code or Mode setting. 
            // This can save a lot of complexity in your AMS account and reduce the number of Transforms used.
            JobOutput jobOutput = new JobOutputAsset()
            {
                AssetName = outputAsset.Name,
                PresetOverride = presetOverride
            };

            Job job = await SubmitJobAsync(client, config.ResourceGroup, config.AccountName, AudioAnalyzerTransformName, jobName, inputAssetName, jobOutput);

            #endregion PresetOverride

            // In this sample, we use Event Grid to listen to the notifications through an Azure Event Hub. 
            // If you do not provide an Event Hub config in the settings, the sample will fall back to polling the job for status. 
            // For production ready code, it is always recommended to use Event Grid instead of polling on the Job status. 

            EventProcessorClient? processorClient = null;
            BlobContainerClient? storageClient = null;
            MediaServicesEventProcessor? mediaEventProcessor = null;
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
                    job = await client.Jobs.GetAsync(config.ResourceGroup, config.AccountName, AudioAnalyzerTransformName, jobName);
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
                job = await WaitForJobToFinishAsync(client, config.ResourceGroup, config.AccountName, AudioAnalyzerTransformName, jobName);
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

            if (job.State == JobState.Finished)
            {
                Console.WriteLine("Job finished.");
                if (!Directory.Exists(OutputFolderName))
                    Directory.CreateDirectory(OutputFolderName);

                await DownloadOutputAssetAsync(client, config.ResourceGroup, config.AccountName, outputAsset.Name, OutputFolderName);
            }

            await CleanUpAsync(client, config.ResourceGroup, config.AccountName, AudioAnalyzerTransformName, inputAssetName, outputAssetName, jobName);
        }


        /// <summary>
        /// If the specified transform exists, get that transform.
        /// If the it does not exist, creates a new transform with the specified output. 
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="transformName">The name of the transform.</param>
        /// <returns></returns>
        private static async Task<Transform> GetOrCreateTransformAsync(IAzureMediaServicesClient client,
            string resourceGroupName,
            string accountName,
            string transformName,
            Preset preset)
        {

            // Start by defining the desired outputs.
            TransformOutput[] outputs = new TransformOutput[]
            {
                new TransformOutput(preset),
            };

            // Does a Transform already exist with the desired name? This method will just overwrite (Update) the Transform if it exists already. 
            // In production code, you may want to be cautious about that. It really depends on your scenario.
            Transform transform = await client.Transforms.CreateOrUpdateAsync(resourceGroupName, accountName, transformName, outputs);

            return transform;
        }

        /// <summary>
        /// Creates a new input Asset and uploads the specified local media file into it.
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
            Asset asset;

            try
            {
                asset = await client.Assets.GetAsync(resourceGroupName, accountName, assetName);

                // The asset already exists and we are going to overwrite it. In your application, if you don't want to overwrite
                // an existing asset, use an unique name.
                Console.WriteLine($"Warning: The asset named {assetName} already exists. It will be overwritten.");

            }
            catch (ErrorResponseException)
            {
                // Call Media Services API to create an Asset.
                // This method creates a container in storage for the Asset.
                // The files (blobs) associated with the asset will be stored in this container.
                Console.WriteLine("Creating an input asset...");
                asset = await client.Assets.CreateOrUpdateAsync(resourceGroupName, accountName, assetName, new Asset());
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
            BlobContainerClient container = new(sasUri);
            BlobClient blob = container.GetBlobClient(Path.GetFileName(fileToUpload));

            // Use Storage API to upload the file into the container in storage.
            Console.WriteLine("Uploading a media file to the asset...");
            await blob.UploadAsync(fileToUpload);
            return asset;
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
            Asset outputAsset = new();
            Console.WriteLine("Creating an output asset...");
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
        // <SubmitJob>
        private static async Task<Job> SubmitJobAsync(IAzureMediaServicesClient client,
            string resourceGroupName,
            string accountName,
            string transformName,
            string jobName,
            string inputAssetName,
            JobOutput jobOutput)
        {
            JobInput jobInput = new JobInputAsset(assetName: inputAssetName);

            JobOutput[] jobOutputs =
            {
                jobOutput
            };

            // In this example, we are assuming that the job name is unique.
            //
            // If you already have a job with the desired name, use the Jobs.Get method
            // to get the existing job. In Media Services v3, Get methods on entities returns ErrorResponseException 
            // if the entity doesn't exist (a case-insensitive check on the name).
            Job job;
            try
            {
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
        ///  Downloads the results from the specified output asset, so you can see what you got.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="assetName">The output asset.</param>
        /// <param name="outputFolderName">The name of the folder into which to download the results.</param>
        private static async Task DownloadOutputAssetAsync(
            IAzureMediaServicesClient client,
            string resourceGroup,
            string accountName,
            string assetName,
            string outputFolderName)
        {
            if (!Directory.Exists(outputFolderName))
            {
                Directory.CreateDirectory(outputFolderName);
            }

            AssetContainerSas assetContainerSas = await client.Assets.ListContainerSasAsync(
                resourceGroup,
                accountName,
                assetName,
                permissions: AssetContainerPermission.Read,
                expiryTime: DateTime.UtcNow.AddHours(1).ToUniversalTime());

            Uri containerSasUrl = new(assetContainerSas.AssetContainerSasUrls.First());
            BlobContainerClient container = new(containerSasUrl);

            string directory = Path.Combine(outputFolderName, assetName);
            Directory.CreateDirectory(directory);

            Console.WriteLine($"Downloading output results to '{directory}'...");

            string continuationToken = null;
            IList<Task> downloadTasks = new List<Task>();

            do
            {
                var resultSegment = container.GetBlobs().AsPages(continuationToken);

                foreach (Azure.Page<BlobItem> blobPage in resultSegment)
                {
                    foreach (BlobItem blobItem in blobPage.Values)
                    {
                        var blobClient = container.GetBlobClient(blobItem.Name);
                        string filename = Path.Combine(directory, blobItem.Name);

                        downloadTasks.Add(blobClient.DownloadToAsync(filename));
                    }
                    // Get the continuation token and loop until it is empty.
                    continuationToken = blobPage.ContinuationToken;
                }


            } while (continuationToken != "");

            await Task.WhenAll(downloadTasks);

            Console.WriteLine("Download complete.");
        }

        /// <summary>
        /// Deletes the jobs and assets that were created.
        /// Generally, you should clean up everything except objects 
        /// that you are planning to reuse (typically, you will reuse Transforms, and you will persist StreamingLocators).
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="transformName">The transform name.</param>
        /// <param name="inputAssetName">The input asset name.</param>
        /// <param name="outputAssetName">The output asset name.</param>
        /// <param name="jobName">The job name.</param>
        private static async Task CleanUpAsync(
            IAzureMediaServicesClient client,
            string resourceGroupName,
            string accountName,
            string transformName,
            string inputAssetName,
            string outputAssetName,
            string jobName)
        {
            Console.WriteLine("Cleaning up...");
            Console.WriteLine();
            Console.WriteLine($"Deleting Job: {jobName}");
            await client.Jobs.DeleteAsync(resourceGroupName, accountName, transformName, jobName);
            Console.WriteLine($"Deleting input asset: {inputAssetName}");
            await client.Assets.DeleteAsync(resourceGroupName, accountName, inputAssetName);
            Console.WriteLine($"Deleting output asset: {outputAssetName}");
            await client.Assets.DeleteAsync(resourceGroupName, accountName, outputAssetName);
            Console.WriteLine($"Deleting Transform: {transformName}.");
            await client.Transforms.DeleteAsync(resourceGroupName, accountName, transformName);
        }
    }
}
