﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Media;
using Azure.ResourceManager.Resources;
using Common_Utils;


// Based on the guidelines in https://github.com/Azure/azure-sdk-for-net/blob/main/doc/dev/mgmt_quickstart.md
namespace Account
{
    class Program
    {
        /// <summary>
        /// The main method of the sample. Please make sure you have set settings in the .env file in the root folder
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static async Task Main(string[] args)
        {
            // If Visual Studio is used, let's read the .env file which should be in the root folder (same folder than the solution .sln file).
            // Same code will work in VS Code, but VS Code uses also launch.json to get the .env file.
            // You can create this ".env" file by saving the "sample.env" file as ".env" file and fill it with the right values.

            // Load configuration using the Common Utilities library
            ConfigWrapper config = Common_Utils.DotEnv.LoadEnvOrAppSettings();

            // First we construct the ArmClient using DefaultAzureCredential
            // This will use the Environment variables set for the current logged in user. 
            // Use the VS Code Azure login command, or the CLI 'az login' to set the environment variables
            ArmClient client = new ArmClient(new DefaultAzureCredential());

            string subscriptionId = config.SubscriptionId;
            SubscriptionCollection subscriptions = client.GetSubscriptions();
            SubscriptionResource subscription = subscriptions.Get(subscriptionId);
            Console.WriteLine($"Got subscription: {subscription.Data.DisplayName}");

            ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();
            ResourceGroupResource resourceGroup = await resourceGroups.GetAsync(config.ResourceGroup);

            // Get all the media accounts in as resource group
            MediaServicesAccountCollection mediaServices = resourceGroup.GetMediaServicesAccounts();

            await foreach (var amsAccount in mediaServices)
            {
                Console.WriteLine($"name= {amsAccount.Data.Name}");
                Console.WriteLine($"location= {amsAccount.Data.Location}");
            }

            // Get a specific media account 
            MediaServicesAccountResource mediaService = await resourceGroup.GetMediaServicesAccountAsync(config.AccountName);

            Console.WriteLine($"Got media service : {mediaService.Data.Name}");

        }
    }
}