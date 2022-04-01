// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Common_Utils;
using Microsoft.Azure.Management.Media;
using Microsoft.Azure.Management.Media.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Rest.Azure;
using Microsoft.Rest.Azure.OData;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Assets
{
    class Program
    {

        // Set this variable to true if you want to authenticate Interactively through the browser using your Azure user account

        // For this sample, it is useful to do this as you need subscription level write permission to create a Media account or an identity 
        // with the policy authorization on 'Microsoft.Media/mediaservices/write'. 
        private const bool UseInteractiveAuth = true;

        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Asset management samples...");
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
                Console.Error.WriteLine("TIP: Make sure that you have filled out the appsettings.json file before running this sample.");
                Console.Error.WriteLine($"{e.Message}");
                return;
            }

            // Set the polling interval for long running operations to 2 seconds.
            // The default value is 30 seconds for the .NET client SDK
            client.LongRunningOperationRetryTimeout = 2;

            // List all assets in the account
            Console.WriteLine("Listing all assets in this account");
            IPage<Asset> assets = await client.Assets.ListAsync(config.ResourceGroup, config.AccountName);

            // list the assets in this page. There may be a lot more assets if there is a next page
            foreach (Asset a in assets)
            {
                Console.WriteLine($"Asset name: {a.Name}, id: {a.AssetId}, container: {a.Container}");
            }

            if (assets.NextPageLink != null)
            {
                Console.WriteLine("There are more pages of assets to get, you can call the listNextAsync");
                IPage<Asset> moreAssets = await client.Assets.ListNextAsync(assets.NextPageLink);
                foreach (Asset a in moreAssets)
                {
                    Console.WriteLine($"Asset name: {a.Name}, id: {a.AssetId}, container: {a.Container}");

                    // You can continue doing this recursively in your application to list all the assets in the account...
                }
            }


            // Create a new empty Asset
             Console.WriteLine("Creating a new empty asset");

             // Create a new asset setting the description, alternate id, a custom container name in storage to override the default
             // naming which uses "asset-" + Guid.NewGuid()
             var newAsset = await client.Assets.CreateOrUpdateAsync(config.ResourceGroup, 
                                                                    config.AccountName,
                                                                    assetName:"myAssetName", 
                                                                    new Asset(){
                                                                       Description = "My Video description",
                                                                       AlternateId = "12345",
                                                                       Container = "my-custom-name-" + Guid.NewGuid()
                                                                    });

            Console.WriteLine($"Created a new asset name : {newAsset.Name} in the storage container : {newAsset.Container}");

            // List assets with filter by date

            // List all assets created in the last 24 hours
            Console.WriteLine("Listing all assets created in the last 24 hours..");
            var dateFilter = DateTime.UtcNow.AddHours(-24).ToString("O", DateTimeFormatInfo.InvariantInfo);
            // We need to construct an Odata format query using the date filter to show assets created in the last 24 hours
            var odataQuery = new ODataQuery<Asset>($"properties/created gt {dateFilter}");

            IPage<Asset> todayAssets = await client.Assets.ListAsync(config.ResourceGroup,
                                                                        config.AccountName,
                                                                        odataQuery);
            // list the assets created in the last 24 hours
            foreach (Asset a in todayAssets)
            {
                Console.WriteLine($"Created: {a.Created} -  Asset name: {a.Name} ");
            }

             Console.WriteLine("Done.");

        }

    }
}
