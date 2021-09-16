// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Common_Utils;
using Microsoft.Azure.Management.Media;
using Microsoft.Azure.Management.Media.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Account
{
    class Program
    {
        // Set this variable to true if you want to authenticate Interactively through the browser using your Azure user account

        // For this sample, it is useful to do this as you need subscription level write permission to create a Media account or an identity 
        // with the policy authorization on 'Microsoft.Media/mediaservices/write'. 
        private const bool UseInteractiveAuth = true;

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
                Console.Error.WriteLine("TIP: Make sure that you have filled out the appsettings.json file before running this sample.");
                Console.Error.WriteLine($"{e.Message}");
                return;
            }

            // Set the polling interval for long running operations to 2 seconds.
            // The default value is 30 seconds for the .NET client SDK
            client.LongRunningOperationRetryTimeout = 2;

            string uniqueness = Guid.NewGuid().ToString().Substring(0, 13).Replace('-', 'x'); // Create a GUID for uniqueness.
            string accountName = "testaccount" + uniqueness;

            // Set this to one of the available region names using the format japanwest,japaneast,eastasia,southeastasia,
            // westeurope,northeurope,eastus,westus,australiaeast,australiasoutheast,eastus2,centralus,brazilsouth,
            // centralindia,westindia,southindia,northcentralus,southcentralus,uksouth,ukwest,canadacentral,canadaeast,
            // westcentralus,westus2,koreacentral,koreasouth,francecentral,francesouth,southafricanorth,southafricawest,
            // uaecentral,uaenorth,germanywestcentral,germanynorth,switzerlandwest,switzerlandnorth,norwayeast

            string accountLocation = "westus";

            // Set up the values for your Media Services account 
            MediaService parameters = new(
                location: accountLocation, // This is the location for the account to be created. 
                storageAccounts: new List<StorageAccount>(){
                    new StorageAccount(
                        type: StorageAccountType.Primary,
                        // set this to the name of a storage account in your subscription using the full resource path formatting for Microsoft.Storage
                        id: $"/subscriptions/{config.SubscriptionId}/resourceGroups/{config.ResourceGroup}/providers/Microsoft.Storage/storageAccounts/{config.StorageAccountName}"
                    )
                },

                keyDelivery: new(  // To restrict the client access and delivery of your content keys, set the key delivery accessControl ipAllowList. 
                    accessControl: new(
                        defaultAction: DefaultAction.Allow,  // Allow or Deny access from the ipAllowList. If this is set to Allow, the ipAllowList should be empty.
                        ipAllowList: new List<string>()
                        {  // List the IPv3 addresses to Allow or Deny based on the default action. 
                            // "10.0.0.1/32", // you can use the CIDR IPv3 format,
                            // "127.0.0.1"  or a single individual Ipv4 address as well.
                        }
                    )
                )
            );

            var availability = client.Locations.CheckNameAvailability(
                type: "Microsoft.Media/mediaservices",
                locationName: accountLocation,
                name: accountName
            );

            if (!availability.NameAvailable)
            {
                Console.WriteLine($"The account with the name {accountName} is not available.");
                Console.WriteLine(availability.Message);
                throw new Exception(availability.Reason);
            }

            // Create a new Media Services account
            client.Mediaservices.CreateOrUpdate(config.ResourceGroup, accountName, parameters);

            Console.WriteLine($"Media Services account : {accountName} created!");
            Console.WriteLine("Press enter to clean up resources and delete the account...");
            Console.Out.Flush();

            await CleanUpAsync(client, config.ResourceGroup, accountName);
        }


        /// <summary>
        /// Deletes the account that was created.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        private static async Task CleanUpAsync(
            IAzureMediaServicesClient client,
            string resourceGroupName,
            string accountName)
        {
            Console.WriteLine("Cleaning up...");
            Console.WriteLine();

            Console.WriteLine($"Deleting Media account: {accountName}.");
            await client.Mediaservices.DeleteAsync(resourceGroupName, accountName);

        }
    }
}
