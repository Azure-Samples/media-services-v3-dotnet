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

namespace EncodingWithMESPredefinedPreset
{
    class Program
    {
        const String outputFolder = @"Output";
        const String transformName = "AdaptiveBitrate";

        public static async Task Main(string[] args)
        {
            ConfigWrapper config = new ConfigWrapper(new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build());

            try
            {
                await RunEncodingWithMESPredefinedPreset(config);
            }
            catch (Exception exception)
            {
                if (exception.Source.Contains("ActiveDirectory"))
                {
                     Console.Error.WriteLine("TIP: Make sure that you have filled out the appsettings.json file before running this sample.");
                }

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
        /// <param name="config">This param is of type ConfigWrapper, which reads values from local configuration file.</param>
        /// <returns>A task.</returns>
        private static async Task RunEncodingWithMESPredefinedPreset(ConfigWrapper config)
        {
            IAzureMediaServicesClient client = await CreateMediaServicesClientAsync(config);
            // Set the polling interval for long running operations to 2 seconds.
            // The default value is 30 seconds for the .NET client SDK
            client.LongRunningOperationRetryTimeout = 2;

            try
            {
                // Ensure that you have customized encoding Transform.  This is really a one time setup operation.
                Transform adaptiveEncodeTransform = EnsureTransformExists(client, config.ResourceGroup, config.AccountName,
                    transformName, preset: new BuiltInStandardEncoderPreset(EncoderNamedPreset.AdaptiveStreaming));

                // Creating a unique suffix so that we don't have name collisions if you run the sample
                // multiple times without cleaning up.
                string uniqueness = Guid.NewGuid().ToString().Substring(0, 13);

                string jobName = "job-" + uniqueness;
                string locatorName = "locator-" + uniqueness;
                string outputAssetName = "output-" + uniqueness;

                var input = new JobInputHttp(
                                    baseUri: "https://nimbuscdn-nimbuspm.streaming.mediaservices.windows.net/2b533311-b215-4409-80af-529c3e853622/",
                                    files: new List<String> {"Ignite-short.mp4"},
                                    label:"input1"
                                    );

                // Output from the encoding Job must be written to an Asset, so let's create one. Note that we
                // are using a unique asset name, there should not be a name collision.
                Asset outputAsset = CreateOutputAsset(client, config.ResourceGroup, config.AccountName, outputAssetName);

                Job job = SubmitJob(client, config.ResourceGroup, config.AccountName, transformName, jobName, input, outputAsset.Name);

                DateTime startedTime = DateTime.Now;

                // In this demo code, we will poll for Job status.
                // Polling is not a recommended best practice for production applications because of the latency it introduces.
                // Overuse of this API may trigger throttling. Developers should instead use Event Grid.
                job = WaitForJobToFinish(client, config.ResourceGroup, config.AccountName, transformName, jobName);

                TimeSpan elapsed = DateTime.Now - startedTime;
                Console.WriteLine($"Job elapsed time: {elapsed}");

                if (job.State == JobState.Finished)
                {
                    Console.WriteLine("Job finished.");

                    // Now that the content has been encoded, publish it for Streaming by creating
                    // a StreamingLocator.
                    StreamingLocator locator = await CreateStreamingLocatorAsync(client, config.ResourceGroup, config.AccountName, outputAsset.Name, locatorName);

                    IList<string> urls = await GetStreamingUrlsAsync(client, config.ResourceGroup, config.AccountName, locator.Name);
                    foreach (var url in urls)
                    {
                        Console.WriteLine(url);
                        Console.WriteLine();
                    }

                    Console.WriteLine("To try streaming, copy and paste the Streaming URL into the Azure Media Player at 'http://aka.ms/azuremediaplayer'.");
                    Console.WriteLine("When finished, press ENTER to continue.");
                    Console.WriteLine();
                    Console.Out.Flush();
                    Console.ReadLine();

                    // Download output asset for verification.
                    Console.WriteLine("Downloading output asset...");
                    Console.WriteLine();
                    if (!Directory.Exists(outputFolder))
                        Directory.CreateDirectory(outputFolder);
                    DownloadResults(client, config.ResourceGroup, config.AccountName, outputAsset.Name, outputFolder).Wait();

                    Console.WriteLine("Please check the files in the output folder.");
                    Console.WriteLine("When finished, press ENTER to cleanup.");
                    Console.Out.Flush();
                    Console.ReadLine();

                    await CleanUpAsync(client, config.ResourceGroup, config.AccountName, transformName, job.Name, outputAsset.Name, locatorName);

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
        /// <param name="config">The param is of type ConfigWrapper, which reads values from local configuration file.</param>
        /// <returns>A task.</returns>
        private static async Task<ServiceClientCredentials> GetCredentialsAsync(ConfigWrapper config)
        {
            // Use ApplicationTokenProvider.LoginSilentWithCertificateAsync or UserTokenProvider.LoginSilentAsync to get a token using service principal with certificate
            //// ClientAssertionCertificate
            //// ApplicationTokenProvider.LoginSilentWithCertificateAsync

            // Use ApplicationTokenProvider.LoginSilentAsync to get a token using a service principal with symetric key
            ClientCredential clientCredential = new ClientCredential(config.AadClientId, config.AadSecret);
            return await ApplicationTokenProvider.LoginSilentAsync(config.AadTenantId, clientCredential, ActiveDirectoryServiceSettings.Azure);
        }

        /// <summary>
        /// Creates the AzureMediaServicesClient object based on the credentials
        /// supplied in local configuration file.
        /// </summary>
        /// <param name="config">The param is of type ConfigWrapper, which reads values from local configuration file.</param>
        /// <returns>A task.</returns>
        private static async Task<IAzureMediaServicesClient> CreateMediaServicesClientAsync(ConfigWrapper config)
        {
            var credentials = await GetCredentialsAsync(config);

            return new AzureMediaServicesClient(config.ArmEndpoint, credentials)
            {
                SubscriptionId = config.SubscriptionId,
            };
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
        private static Transform EnsureTransformExists(IAzureMediaServicesClient client, string resourceGroupName, string accountName, string transformName, Preset preset)
        {
            Transform transform = client.Transforms.Get(resourceGroupName, accountName, transformName);

            if (transform == null)
            {
                TransformOutput[] outputs = new TransformOutput[]
                {
                    new TransformOutput(preset),
                };

                transform = client.Transforms.CreateOrUpdate(resourceGroupName, accountName, transformName, outputs);
            }

            return transform;
        }

        /// <summary>
        /// Create an asset.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName">The Media Services account name.</param>
        /// <param name="assetName">The name of the asset to be created. It is known to be unique.</param>
        /// <returns>The asset created.</returns>
        private static Asset CreateOutputAsset(IAzureMediaServicesClient client, string resourceGroupName, string accountName,  string assetName)
        {
            Asset input = new Asset();

            return client.Assets.CreateOrUpdate(resourceGroupName, accountName, assetName, input);
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
        private static Job SubmitJob(IAzureMediaServicesClient client, string resourceGroupName, string accountName,  string transformName, string jobName, JobInput jobInput, string outputAssetName)
        {
            JobOutput[] jobOutputs =
            {
                new JobOutputAsset(outputAssetName), 
            };

            Job job = client.Jobs.Create(
                resourceGroupName, 
                accountName,
                transformName,
                jobName,
                new Job
                {
                    Input = jobInput,
                    Outputs = jobOutputs,
                });

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
        private static Job WaitForJobToFinish(IAzureMediaServicesClient client, string resourceGroupName, string accountName,  string transformName, string jobName)
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
        /// Use Media Service and Storage APIs to download the output files to a local folder
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName">The Media Services account name.</param>
        /// <param name="assetName">The asset name.</param>
        /// <param name="resultsFolder">The output folder name for downloaded files.</param>
        /// <returns>A task.</returns>
        private async static Task DownloadResults(IAzureMediaServicesClient client, string resourceGroupName, string accountName, string assetName, string resultsFolder)
        {
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

            string directory = Path.Combine(resultsFolder, assetName);
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
        /// <returns>A task.</returns>
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
        /// if not, starts it. Then, builds the streaming URLs.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="locatorName">The name of the StreamingLocator that was created.</param>
        /// <returns>A task.</returns>
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
            string transformName, string jobName, string assetName, string streamingLocatorName)
        {
            Console.WriteLine("Cleaning up...");

            await client.Jobs.DeleteAsync(resourceGroupName, accountName, transformName, jobName);
            await client.Assets.DeleteAsync(resourceGroupName, accountName, assetName);
            await client.StreamingLocators.DeleteAsync(resourceGroupName, accountName, streamingLocatorName);
        }
    }
}
