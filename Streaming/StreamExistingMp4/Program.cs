// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Storage.Blobs;
using Common_Utils;
using Microsoft.Azure.Management.Media;
using Microsoft.Azure.Management.Media.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace StreamExistingMp4
{
    public class Program
    {
        private const string InputMP4FileName = @"IgniteHD1800kbps.mp4";
        private const string DefaultStreamingEndpointName = "default";

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
            string locatorName = $"locator-{uniqueness}";
            string inputAssetName = $"input-{uniqueness}";
            bool stopEndpoint = false;

            try
            {
                // Create a new input Asset and upload the Mp4 local video file into it that is already encoded with the following settings:
                //  GOP size: 2 seconds
                //  Constant Bitrate Encoded - CBR mode
                //  Key Frame distance max 2 seconds
                //  Min Key frame distance 2 seconds
                //  Video Codec: H.264 or HEVC
                //  Audio COdec: AAC

                var inputAsset = await CreateInputAssetAsync(client, config.ResourceGroup, config.AccountName, inputAssetName, InputMP4FileName);

                StreamingLocator locator = await CreateStreamingLocatorAsync(client, config.ResourceGroup, config.AccountName, inputAssetName, locatorName);

                // Generate the Server manifest for streaming .ism file.
                // This file is a simple SMIL 2.0 file format schema that includes references to the uploaded MP4 files in the XML.
                var manifestsList = await AssetUtils.CreateServerManifestsAsync(client, config.ResourceGroup, config.AccountName, inputAsset, locator);
                var ismManifestName = manifestsList.FirstOrDefault();

                // v3 API throws an ErrorResponseException if the resource is not found.
                StreamingEndpoint streamingEndpoint = await client.StreamingEndpoints.GetAsync(config.ResourceGroup, config.AccountName, DefaultStreamingEndpointName);
                if (streamingEndpoint.ResourceState != StreamingEndpointResourceState.Running)
                {
                    Console.WriteLine("Streaming Endpoint was Stopped, restarting now..");
                    await client.StreamingEndpoints.StartAsync(config.ResourceGroup, config.AccountName, DefaultStreamingEndpointName);

                    // Since we started the endpoint, we should stop it in cleanup.
                    stopEndpoint = true;
                }

                IList<string> urls = GetHLSAndDASHStreamingUrlsAsync(locator, ismManifestName, streamingEndpoint);
                Console.WriteLine();
                foreach (var url in urls)
                {
                    Console.WriteLine(url);
                }
                Console.WriteLine();
                Console.WriteLine("Copy and paste the Streaming URL into the Azure Media Player at 'http://aka.ms/azuremediaplayer'.");
                Console.WriteLine("When finished press enter to cleanup.");
                Console.Out.Flush();
                Console.ReadLine();

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
                await CleanUpAsync(client, config.ResourceGroup, config.AccountName, inputAssetName, stopEndpoint, DefaultStreamingEndpointName);
                Console.WriteLine("Done.");
            }
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
            // to get the existing asset. In Media Services v3, the Get method throws an ErrorResponseException if the resource is not found on a get. 
            Console.WriteLine("Creating an input asset...");
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
        /// Builds the streaming URLs.
        /// </summary>
        /// <param name="locatorName">The name of the StreamingLocator that was created.</param>
        /// <param name="streamingEndpoint">The streaming endpoint.</param>
        /// <returns></returns>
        private static IList<string> GetHLSAndDASHStreamingUrlsAsync(
            StreamingLocator locator,
            string manifestName,
            StreamingEndpoint streamingEndpoint)
        {
            var hostname = streamingEndpoint.HostName;
            var scheme = "https";
            IList<string> manifests = BuildManifestPaths(scheme, hostname, locator.StreamingLocatorId.ToString(), manifestName);

            Console.WriteLine($"The HLS (MP4) manifest for the uploaded asset is : {manifests[0]}");
            Console.WriteLine("Copy the following URL to use in an HLS compliant player (HLS.js, Shaka, ExoPlayer) or directly in an iOS device. Just send it in email to your phone and you can click and play it.");
            Console.WriteLine($"{manifests[0]}");
            Console.WriteLine();
            Console.WriteLine($"The DASH manifest URL for the uploaded asset is  : {manifests[1]}");
            Console.WriteLine("Open the following URL to playback the uploaded Mp4 file using quickstart heuristics in the Azure Media Player");
            Console.WriteLine($"https://ampdemo.azureedge.net/?url={manifests[1]}&heuristicprofile=quickstart");
            Console.WriteLine();
            Console.Out.Flush();
            Console.ReadLine();
            return manifests;
        }

        private static List<string> BuildManifestPaths(string scheme, string hostname, string streamingLocatorId, string manifestName)
        {
            const string hlsFormat = "format=m3u8-cmaf";
            const string dashFormat = "format=mpd-time-cmaf";

            var manifests = new List<string>();

            var manifestBase = $"{scheme}://{hostname}/{streamingLocatorId}/{manifestName}/manifest";
            var hlsManifest = $"{manifestBase}({hlsFormat})";
            manifests.Add(hlsManifest);

            var dashManifest = $"{manifestBase}({dashFormat})";
            manifests.Add(dashManifest);

            return manifests;
        }

        /// <summary>
        /// Deletes the jobs and assets that were created.
        /// Generally, you should clean up everything except objects 
        /// that you are planning to reuse (typically, you will reuse Transforms, and you will persist StreamingLocators).
        /// </summary>
        /// <param name="client"></param>
        /// <param name="resourceGroupName"></param>
        /// <param name="accountName"></param>
        /// <param name="transformName"></param>
        private static async Task CleanUpAsync(
            IAzureMediaServicesClient client, string resourceGroupName, string accountName,
           string inputAssetName, bool stopEndpoint, string streamingEndpointName)
        {
            await client.Assets.DeleteAsync(resourceGroupName, accountName, inputAssetName);

            if (stopEndpoint)
            {
                // Because we started the endpoint, we'll stop it.
                await client.StreamingEndpoints.StopAsync(resourceGroupName, accountName, streamingEndpointName);
            }
            else
            {
                // We will keep the endpoint running because it was not started by us. There are costs to keep it running.
                // Please refer https://azure.microsoft.com/en-us/pricing/details/media-services/ for pricing. 
                Console.WriteLine($"WARNING: The endpoint {streamingEndpointName} is running. To halt further billing on the endpoint, please stop it in azure portal or AMS Explorer.");
            }
        }
    }
}
