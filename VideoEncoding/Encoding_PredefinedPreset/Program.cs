// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
using System.Threading.Tasks;

namespace Encoding_PredefinedPreset
{
    class Program
    {
        private const string OutputFolder = @"Output";
        private const string TransformName = "AdaptiveBitrate";
        private const string DefaultStreamingEndpointName = "default";
        private const string BaseSourceUri = "https://nimbuscdn-nimbuspm.streaming.mediaservices.windows.net/2b533311-b215-4409-80af-529c3e853622/";
        private const string FileSourceUri = "Ignite-short.mp4";

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
        /// <param name="config">This param is of type ConfigWrapper, which reads values from local configuration file.</param>
        /// <returns>A task.</returns>
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
                    Console.Error.WriteLine("TIP: Make sure that you have filled out the appsettings.json or .env file before running this sample.");
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
            string outputAssetName = $"output-{uniqueness}";
            bool stopEndpoint = false;

            try
            {
                // Ensure that you have customized encoding Transform.  This is really a one time setup operation.
                Transform adaptiveEncodeTransform = await EnsureTransformExists(client,
                                                                                config.ResourceGroup,
                                                                                config.AccountName,
                                                                                TransformName,
                                                                                preset: new BuiltInStandardEncoderPreset(EncoderNamedPreset.AdaptiveStreaming));
                // This is using th built-in Adaptive Encdoing preset. You can choose from a variety of built-in presets.

                var input = new JobInputHttp(
                                    baseUri: BaseSourceUri,
                                    files: new List<String> { FileSourceUri },
                                    label: "input1"
                                    );

                // Output from the encoding Job must be written to an Asset, so let's create one. Note that we
                // are using a unique asset name, there should not be a name collision.
                Asset outputAsset = await CreateOutputAssetAsync(client, config.ResourceGroup, config.AccountName, outputAssetName);

                Job job = await SubmitJobAsync(client, config.ResourceGroup, config.AccountName, TransformName, jobName, input, outputAsset.Name);

                DateTime startedTime = DateTime.Now;

                // In this demo code, we will poll for Job status. Polling is not a recommended best practice for production
                // applications because of the latency it introduces. Overuse of this API may trigger throttling. Developers
                // should instead use Event Grid. To see how to implement the event grid, see the sample
                // https://github.com/Azure-Samples/media-services-v3-dotnet/tree/master/ContentProtection/BasicAESClearKey.
                job = WaitForJobToFinish(client, config.ResourceGroup, config.AccountName, TransformName, jobName);

                TimeSpan elapsed = DateTime.Now - startedTime;
                Console.WriteLine($"Job elapsed time: {elapsed}");

                if (job.State == JobState.Finished)
                {
                    Console.WriteLine("Job finished.");

                    // Now that the content has been encoded, publish it for Streaming by creating
                    // a StreamingLocator.
                    StreamingLocator locator = await CreateStreamingLocatorAsync(client, config.ResourceGroup, config.AccountName,
                        outputAsset.Name, locatorName);

                    StreamingEndpoint streamingEndpoint = await client.StreamingEndpoints.GetAsync(config.ResourceGroup, config.AccountName,
                        DefaultStreamingEndpointName);

                    if (streamingEndpoint.ResourceState != StreamingEndpointResourceState.Running)
                    {
                        await client.StreamingEndpoints.StartAsync(config.ResourceGroup, config.AccountName, DefaultStreamingEndpointName);

                        // We started the endpoint, we should stop it in cleanup.
                        stopEndpoint = true;
                    }

                    IList<string> urls = await GetStreamingUrlsAsync(client, config.ResourceGroup, config.AccountName, locator.Name, streamingEndpoint);


                    Console.WriteLine("To stream, copy and paste the Streaming URL into the Azure Media Player at 'http://aka.ms/azuremediaplayer'.");
                    Console.WriteLine("When finished, press ENTER to continue.");
                    Console.WriteLine();
                    Console.Out.Flush();
                    Console.ReadLine();

                    // Download output asset for verification.
                    Console.WriteLine("Downloading output asset...");
                    Console.WriteLine();
                    if (!Directory.Exists(OutputFolder))
                        Directory.CreateDirectory(OutputFolder);
                    DownloadResults(client, config.ResourceGroup, config.AccountName, outputAsset.Name, OutputFolder).Wait();

                    Console.WriteLine("Please check the files in the output folder.");
                    Console.WriteLine("When finished, press ENTER to cleanup.");
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
                Console.WriteLine("Hit ErrorResponseException");
                Console.WriteLine($"\tCode: {e.Body.Error.Code}");
                Console.WriteLine($"\tMessage: {e.Body.Error.Message}");
                Console.WriteLine();
                Console.WriteLine("Exiting, cleanup may be necessary...");
                Console.ReadLine();
            }
            finally
            {
                await CleanUpAsync(client, config.ResourceGroup, config.AccountName, TransformName, jobName, outputAssetName, locatorName,
                    stopEndpoint, DefaultStreamingEndpointName);
                Console.WriteLine("Done.");
            }
        }



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
            return await client.Assets.CreateOrUpdateAsync(resourceGroupName, accountName, assetName, outputAsset);
        }

        /// <summary>
        /// Create and submit a job.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName">The Media Services account name.</param>
        /// <param name="transformName">The name of the transform.</param>
        /// <param name="jobName">The name of the job to be created.</param>
        /// <param name="jobInput">The input to the job.</param>
        /// <param name="outputAssetName">The name of the asset that the job writes to.</param>
        /// <returns>The job created.</returns>
        private static async Task<Job> SubmitJobAsync(IAzureMediaServicesClient client, string resourceGroupName, string accountName, string transformName,
            string jobName, JobInput jobInput, string outputAssetName)
        {
            Console.WriteLine("Creating a job...");

            JobOutput[] jobOutputs =
            {
                new JobOutputAsset(outputAssetName),
            };

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
        /// Wait for the job to finish.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName">The Media Services account name.</param>
        /// <param name="transformName">The name of the transform.</param>
        /// <param name="jobName">The name of the job.</param>
        /// <returns></returns>
        private static Job WaitForJobToFinish(IAzureMediaServicesClient client, string resourceGroupName, string accountName, string transformName, string jobName)
        {
            const int SleepInterval = 10 * 1000;

            Job job;
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
        /// <returns>A task.</returns>
        private static async Task<StreamingLocator> CreateStreamingLocatorAsync(
            IAzureMediaServicesClient client,
            string resourceGroup,
            string accountName,
            string assetName,
            string locatorName)
        {
            Console.WriteLine("Creating a streaming locator...");
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
        /// if not, starts it. Then, builds the streaming URLs.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="locatorName">The name of the StreamingLocator that was created.</param>
        /// <param name="streamingEndpoint">The streaming endpoint.</param>
        /// <returns>A task.</returns>
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
        /// Delete the job and asset and streaming locator that were created.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="transformName">The transform name.</param>
        /// <param name="jobName">The job name.</param>
        /// <param name="assetName">The asset name.</param>
        /// <param name="streamingLocatorName">The streaming locator name. </param>
        /// <returns>A task.</returns>
        private static async Task CleanUpAsync(IAzureMediaServicesClient client, string resourceGroupName, string accountName,
            string transformName, string jobName, string assetName, string streamingLocatorName,
            bool stopEndpoint, string streamingEndpointName)
        {
            Console.WriteLine("Cleaning up...");

            await client.Jobs.DeleteAsync(resourceGroupName, accountName, transformName, jobName);
            await client.Assets.DeleteAsync(resourceGroupName, accountName, assetName);
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
