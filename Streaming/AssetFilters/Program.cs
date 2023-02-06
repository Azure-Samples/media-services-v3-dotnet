// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Storage.Blobs;
using Common_Utils;
using Microsoft.Azure.Management.Media;
using Microsoft.Azure.Management.Media.Models;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace AssetFilters
{
    class Program
    {
        private const string adaptiveTransformName = "MyTransformWithAdaptiveStreamingPreset";
        private const string InputMP4FileName = @"ignite.mp4";
        private const string DefaultStreamingEndpointName = "default";   // Change this to your Streaming Endpoint name.

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

            var config = new ConfigWrapper(new ConfigurationBuilder()
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
                Console.Error.WriteLine("TIP: Make sure that you have filled out the appsettings.json or .env file before running this sample.");
                Console.Error.WriteLine($"{e.Message}");
                return;
            }

            // Set the polling interval for long running operations to 2 seconds.
            // The default value is 30 seconds for the .NET client SDK
            client.LongRunningOperationRetryTimeout = 2;

            // Creating a unique suffix so that we don't have name collisions if you run the sample
            // multiple times without cleaning up.
            string uniqueness = Guid.NewGuid().ToString().Substring(0, 13);
            string jobName = "job-" + uniqueness;
            string locatorName = "locator-" + uniqueness;
            string outputAssetName = "output-" + uniqueness;
            string assetFilterName = "assetFilter-" + uniqueness;
            string accountFilterName = "accountFilter-" + uniqueness;
            string inputAssetName = "input-" + uniqueness;
            bool stopEndpoint = false;

            try
            {
                // Ensure that you have customized encoding Transform.  This is really a one time setup operation.
                Transform adaptiveEncodeTransform = await GetOrCreateTransformAsync(client, config.ResourceGroup, config.AccountName,
                    adaptiveTransformName);

                // Create a new input Asset and upload the specified local video file into it.
                Asset inputAsset = await CreateInputAssetAndUploadVideoAsync(client, config.ResourceGroup, config.AccountName, inputAssetName, InputMP4FileName);

                // Output from the encoding Job must be written to an Asset, so let's create one.
                Asset outputAsset = await CreateOutputAssetAsync(client, config.ResourceGroup, config.AccountName, outputAssetName);

                Console.WriteLine("Creating a job...");
                Job job = await SubmitJobAsync(client, config.ResourceGroup, config.AccountName, adaptiveTransformName, jobName, inputAssetName, outputAsset.Name);

                DateTime startedTime = DateTime.Now;

                // In this demo code, we will poll for Job status. Polling is not a recommended best practice for production
                // applications because of the latency it introduces. Overuse of this API may trigger throttling. Developers
                // should instead use Event Grid. To see how to implement the event grid, see the sample
                // https://github.com/Azure-Samples/media-services-v3-dotnet/tree/master/ContentProtection/BasicAESClearKey.
                job = WaitForJobToFinish(client, config.ResourceGroup, config.AccountName, adaptiveTransformName, jobName);

                TimeSpan elapsed = DateTime.Now - startedTime;
                Console.WriteLine($"Job elapsed time: {elapsed}");

                if (job.State == JobState.Finished)
                {
                    Console.WriteLine("Job finished.");

                    // Now that the content has been encoded, publish it for Streaming by creating
                    // a StreamingLocator.
                    StreamingLocator locator = await client.StreamingLocators.CreateAsync(
                        config.ResourceGroup,
                        config.AccountName,
                        locatorName,
                        new StreamingLocator
                        {
                            AssetName = outputAsset.Name,
                            StreamingPolicyName = PredefinedStreamingPolicy.ClearStreamingOnly
                        });

                    // v3 API throws an ErrorResponseException if the resource is not found.
                    StreamingEndpoint streamingEndpoint = await client.StreamingEndpoints.GetAsync(config.ResourceGroup,
                        config.AccountName, DefaultStreamingEndpointName);

                    if (streamingEndpoint.ResourceState != StreamingEndpointResourceState.Running)
                    {
                        Console.WriteLine("Streaming Endpoint was Stopped, restarting now..");
                        await client.StreamingEndpoints.StartAsync(config.ResourceGroup, config.AccountName, DefaultStreamingEndpointName);

                        // Since we started the endpoint, we should stop it in cleanup.
                        stopEndpoint = true;
                    }

                    IList<string> urls = await GetDashStreamingUrlsAsync(client, config.ResourceGroup, config.AccountName, locator.Name, streamingEndpoint);

                    Console.WriteLine("Creating an asset filter...");
                    Console.WriteLine();
                    AssetFilter assetFilter = await CreateAssetFilterAsync(client, config.ResourceGroup, config.AccountName, outputAsset.Name, assetFilterName);

                    Console.WriteLine("Creating an account filter...");
                    Console.WriteLine();
                    AccountFilter accountFilter = await CreateAccountFilterAsync(client, config.ResourceGroup, config.AccountName, accountFilterName);

                    Console.WriteLine("We are going to use two different ways to show how to filter content. First, we will append the filters to the url(s).");
                    Console.WriteLine("Url(s) with filters:");
                    foreach (var url in urls)
                    {
                        if (url.EndsWith(")"))
                        {
                            Console.WriteLine(Regex.Replace(url, @"\)$", $",filter={assetFilter.Name};{accountFilter.Name})"));
                        }
                        else
                        {
                            Console.WriteLine($"{url}(filter={assetFilter.Name};{accountFilter.Name})");
                        }
                    }

                    Console.WriteLine();
                    Console.WriteLine("Copy and paste the streaming URL into the Azure Media Player at 'http://aka.ms/azuremediaplayer'.");
                    Console.WriteLine("Please note that we have used two filters in the url(s), one trimmed the start and the end of the media");
                    Console.WriteLine("and the other removed high resolution video tracks. To stream the original content, remove the filters");
                    Console.WriteLine("from the url(s) and update player.");
                    Console.WriteLine("When finished, press ENTER to continue.");
                    Console.WriteLine();
                    Console.Out.Flush();
                    Console.ReadLine();

                    // Create a new StreamingLocator and associate filters with it.
                    Console.WriteLine("Next, we will associate the filters with a new streaming locator.");
                    await client.StreamingLocators.DeleteAsync(config.ResourceGroup, config.AccountName, locatorName); // Delete the old locator.
                    Console.WriteLine("Creating a new streaming locator...");
                    IList<string> filters = new List<string>
                    {
                        assetFilter.Name,
                        accountFilter.Name
                    };
                    locator = await client.StreamingLocators.CreateAsync(
                        config.ResourceGroup,
                        config.AccountName,
                        locatorName,
                        new StreamingLocator
                        {
                            AssetName = outputAsset.Name,
                            StreamingPolicyName = PredefinedStreamingPolicy.ClearStreamingOnly,
                            Filters = filters
                        });

                    urls = await GetDashStreamingUrlsAsync(client, config.ResourceGroup, config.AccountName, locator.Name, streamingEndpoint);
                    Console.WriteLine("Since we have associated filters with the new streaming locator, No need to append filters to the url(s):");
                    foreach (string url in urls)
                    {
                        Console.WriteLine(url);
                    }
                    Console.WriteLine();
                    Console.WriteLine("Copy and paste the Streaming URL into the Azure Media Player at 'http://aka.ms/azuremediaplayer'.");
                    Console.WriteLine("When finished, press ENTER to continue.");
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
                Console.WriteLine("Cleaning up...");
                await CleanUpAsync(client, config.ResourceGroup, config.AccountName, adaptiveTransformName, jobName,
                        inputAssetName, outputAssetName, accountFilterName, locatorName, stopEndpoint, DefaultStreamingEndpointName);

                Console.WriteLine("Done.");
            }
        }


        /// <summary>
        /// If the specified transform exists, get that transform.
        /// If the it does not exist, creates a new transform with the specified output. 
        /// In this case, the output is set to encode a video using one of the built-in encoding presets.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="transformName">The name of the transform.</param>
        /// <returns></returns>
        private static async Task<Transform> GetOrCreateTransformAsync(IAzureMediaServicesClient client, string resourceGroupName,
            string accountName, string transformName)
        {

            // You need to specify what you want it to produce as an output
            TransformOutput[] output = new TransformOutput[]
            {
                new TransformOutput
                {
                    // The preset for the Transform is set to one of Media Services built-in sample presets.
                    // You can  customize the encoding settings by changing this to use "StandardEncoderPreset" class.
                    Preset = new BuiltInStandardEncoderPreset()
                    {
                        // This sample uses the built-in encoding preset for Adaptive Bitrate Streaming.
                        PresetName = EncoderNamedPreset.AdaptiveStreaming
                    }
                }
            };

            // Create the Transform with the output defined above
            // Does a Transform already exist with the desired name? This method will just overwrite (Update) the Transform if it exists already. 
            // In production code, you may want to be cautious about that. It really depends on your scenario.
            Transform transform = await client.Transforms.CreateOrUpdateAsync(resourceGroupName, accountName, transformName, output);

            return transform;
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
        private static async Task<Asset> CreateInputAssetAndUploadVideoAsync(IAzureMediaServicesClient client, string resourceGroupName,
            string accountName, string assetName, string fileToUpload)
        {
            // In this example, we are assuming that the asset name is unique.
            // If you already have an asset with the desired name, use the Assets.Get method
            // to get the existing asset. In Media Services v3, the Get method on entities throws an ErrorResponseException if the resource is not found.
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
            var container = new BlobContainerClient (sasUri);
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
        private static async Task<Asset> CreateOutputAssetAsync(IAzureMediaServicesClient client, string resourceGroupName,
            string accountName, string assetName)
        {

            var outputAsset = new Asset();

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
        /// <param name="inputAssetName">The name of the input asset.</param>
        /// <param name="outputAssetName">The (unique) name of the  output asset that will store the result of the encoding job. </param>
        private static async Task<Job> SubmitJobAsync(IAzureMediaServicesClient client, string resourceGroupName,
            string accountName, string transformName, string jobName, string inputAssetName, string outputAssetName)
        {
            // Use the name of the created input asset to create the job input.
            JobInput jobInput = new JobInputAsset(assetName: inputAssetName);

            JobOutput[] jobOutputs =
            {
                new JobOutputAsset(outputAssetName),
            };

            // In this example, we are assuming that the job name is unique.
            // If you already have a job with the desired name, use the Jobs.Get method
            // to get the existing job. In Media Services v3, the Get method on entities returns ErrorResponseException 
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
                    }
                );
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
        private static Job WaitForJobToFinish(IAzureMediaServicesClient client, string resourceGroupName, string accountName,
            string transformName, string jobName)
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
        /// Create an asset filter.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName">The Media Services account name.</param>
        /// <param name="assetName">The asset name.</param>
        /// <param name="assetFilterName"> The AssetFilter name.</param>
        /// <returns></returns>
        private async static Task<AssetFilter> CreateAssetFilterAsync(IAzureMediaServicesClient client, string resourceGroupName,
            string accountName, string assetName, string assetFilterName)
        {
            // startTimestamp = 100000000 and endTimestamp = 300000000 using the default timescale will generate
            // a play-list that contains fragments from between 10 seconds and 30 seconds of the VoD presentation.
            // If a fragment straddles the boundary, the entire fragment will be included in the manifest.
            AssetFilter assetFilter = await client.AssetFilters.CreateOrUpdateAsync(
                resourceGroupName,
                accountName,
                assetName,
                assetFilterName,
                new AssetFilter(
                    presentationTimeRange: new PresentationTimeRange(
                        startTimestamp: 100000000L,
                        endTimestamp: 300000000L))
                );

            return assetFilter;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName">The Media Services account name.</param>
        /// <param name="accountFilterName">The AccountFilter name</param>
        /// <returns></returns>
        private async static Task<AccountFilter> CreateAccountFilterAsync(IAzureMediaServicesClient client, string resourceGroupName,
            string accountName, string accountFilterName)
        {
            var audioConditions = new List<FilterTrackPropertyCondition>()
            {
                new FilterTrackPropertyCondition(FilterTrackPropertyType.Type, "Audio", FilterTrackPropertyCompareOperation.Equal),
                new FilterTrackPropertyCondition(FilterTrackPropertyType.FourCC, "EC-3", FilterTrackPropertyCompareOperation.Equal)
            };

            var videoConditions = new List<FilterTrackPropertyCondition>()
            {
                new FilterTrackPropertyCondition(FilterTrackPropertyType.Type, "Video", FilterTrackPropertyCompareOperation.Equal),
                new FilterTrackPropertyCondition(FilterTrackPropertyType.Bitrate, "0-1000000", FilterTrackPropertyCompareOperation.Equal)
            };

            var includedTracks = new List<FilterTrackSelection>()
            {
                new FilterTrackSelection(audioConditions),
                new FilterTrackSelection(videoConditions)
            };

            AccountFilter accountFilter = await client.AccountFilters.CreateOrUpdateAsync(
                resourceGroupName,
                accountName,
                accountFilterName,
                new AccountFilter(tracks: includedTracks));

            return accountFilter;
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
        private static async Task<IList<string>> GetDashStreamingUrlsAsync(IAzureMediaServicesClient client, string resourceGroupName,
            string accountName, string locatorName, StreamingEndpoint streamingEndpoint)
        {
            IList<string> streamingUrls = new List<string>();

            ListPathsResponse paths = await client.StreamingLocators.ListPathsAsync(resourceGroupName, accountName, locatorName);

            foreach (StreamingPath path in paths.StreamingPaths)
            {
                var uriBuilder = new UriBuilder()
                {
                    Scheme = "https",
                    Host = streamingEndpoint.HostName,
                    Path = path.Paths[0]
                };
                if (path.StreamingProtocol == StreamingPolicyStreamingProtocol.Dash)
                {
                    streamingUrls.Add(uriBuilder.ToString());
                }
            }

            return streamingUrls;
        }

        /// <summary>
        /// Delete the objects that were created.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="transformName">The transform name.</param>
        /// <param name="jobName">The job name.</param>
        /// <param name="inputAssetName">The input asset name.</param>
        /// <param name="outputAssetName">The output asset name.</param>
        /// <param name="accountFilterName">The AccountFilter name.</param>
        /// <param name="streamingLocatorName">The streaming locator name. </param>
        /// <param name="stopEndpoint">Stop endpoint if true, keep endpoint running if false.</param>
        /// <param name="streamingEndpointName">The endpoint name.</param>
        /// <returns>A task.</returns>
        private static async Task CleanUpAsync(IAzureMediaServicesClient client, string resourceGroupName, string accountName,
            string transformName, string jobName, string inputAssetName, string outputAssetName, string accountFilterName,
            string streamingLocatorName, bool stopEndpoint, string streamingEndpointName)
        {
            await client.Jobs.DeleteAsync(resourceGroupName, accountName, transformName, jobName);
            await client.Assets.DeleteAsync(resourceGroupName, accountName, inputAssetName);
            await client.Assets.DeleteAsync(resourceGroupName, accountName, outputAssetName);
            await client.AccountFilters.DeleteAsync(resourceGroupName, accountName, accountFilterName);
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
                Console.WriteLine($"The endpoint {streamingEndpointName} is running. To halt further billing on the endpoint, please stop it in azure portal or AMS Explorer.");
            }
        }
    }
}
