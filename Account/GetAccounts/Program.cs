// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Media;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Configuration;


// Based on the guidelines in https://github.com/Azure/azure-sdk-for-net/blob/main/doc/dev/mgmt_quickstart.md
namespace Account
{
    class Program
    {
        /// <summary>
        /// The main method of the sample. Please make sure you have set your settings in the appsettings.json file
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static async Task Main(string[] args)
        {
            IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            IConfigurationRoot configuration = builder.Build();
            
            // First we construct the ArmClient using DefaultAzureCredential
            // This will use the Environment variables set for the current logged in user. 
            // Use the VS Code Azure login command, or the CLI 'az login' to set the environment variables
            ArmClient client = new ArmClient(new DefaultAzureCredential());

            SubscriptionCollection subscriptions = client.GetSubscriptions();
            SubscriptionResource subscription = subscriptions.Get(configuration["AZURE_SUBSCRIPTION_ID"]);
            Console.WriteLine($"Got subscription: {subscription.Data.DisplayName}");

            ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();
            ResourceGroupResource resourceGroup = await resourceGroups.GetAsync(configuration["AZURE_RESOURCE_GROUP"]);

            // Get all the media accounts in as resource group
            MediaServicesAccountCollection mediaServices = resourceGroup.GetMediaServicesAccounts();

            await foreach (var amsAccount in mediaServices)
            {
                Console.WriteLine($"name= {amsAccount.Data.Name}");
                Console.WriteLine($"location= {amsAccount.Data.Location}");
            }

            // Get a specific media account 
            MediaServicesAccountResource mediaService = await resourceGroup.GetMediaServicesAccountAsync(configuration["AZURE_MEDIA_SERVICES_ACCOUNT_NAME"]);

            Console.WriteLine($"Got media service : {mediaService.Data.Name}");
        }
    }
}