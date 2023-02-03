// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Media;
using Azure.ResourceManager.Media.Models;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

// Loading the settings from the appsettings.json file or from the command line parameters
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddCommandLine(args)
    .Build();

if (!Options.TryGetOptions(configuration, out var options))
{
    return;
}

Console.WriteLine($"Subscription ID:             {options.AZURE_SUBSCRIPTION_ID}");
Console.WriteLine($"Storage resource group name: {options.AZURE_STORAGE_RESOURCE_GROUP}");
Console.WriteLine($"Storage account name:        {options.AZURE_STORAGE_ACCOUNT_NAME}");
Console.WriteLine();

// First we construct the ArmClient using DefaultAzureCredential
// This will use the Environment variables set for the current logged in user. 
// Use the VS Code Azure login command, or the CLI 'az login' to set the environment variables
var client = new ArmClient(new DefaultAzureCredential(), options.AZURE_SUBSCRIPTION_ID.ToString());

SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();
Console.WriteLine($"Got subscription: {subscription.Data.DisplayName}");

ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();

string uniqueness = Guid.NewGuid().ToString().Substring(0, 13).Replace('-', 'x'); // Create a GUID for uniqueness.

// Create a new resource group
string resourceGroupName = "newresourcegroup_" + uniqueness;
AzureLocation location = AzureLocation.WestUS2;
var resourceGroupData = new ResourceGroupData(location);
ArmOperation<ResourceGroupResource> operation = await resourceGroups.CreateOrUpdateAsync(WaitUntil.Completed, resourceGroupName, resourceGroupData);
ResourceGroupResource resourceGroupNew = operation.Value;

string accountName = "mynewaccount_" + uniqueness;
MediaServicesAccountCollection mediaServiceCollection = resourceGroupNew.GetMediaServicesAccounts();

ArmOperation<MediaServicesAccountResource> createAccountOperation = await mediaServiceCollection.CreateOrUpdateAsync(
    WaitUntil.Completed,
    accountName: accountName,
    new MediaServicesAccountData(location)
    {
        StorageAccounts = {
                        new MediaServicesStorageAccount(MediaServicesStorageAccountType.Primary)
                        {
                            Id = new ResourceIdentifier($"/subscriptions/{options.AZURE_SUBSCRIPTION_ID}/resourceGroups/{options.AZURE_STORAGE_RESOURCE_GROUP}/providers/Microsoft.Storage/storageAccounts/{options.AZURE_STORAGE_ACCOUNT_NAME}")
                        }
        }
    });

Console.WriteLine($"Created new Media Services account: {createAccountOperation.GetRawResponse()}");


/// <summary>
/// Class to manage the settings which come from appsettings.json or command line parameters.
/// </summary>
internal class Options
{
    [Required]
    public Guid? AZURE_SUBSCRIPTION_ID { get; set; }

    [Required]
    public string? AZURE_STORAGE_RESOURCE_GROUP { get; set; }

    [Required]
    public string? AZURE_STORAGE_ACCOUNT_NAME { get; set; }

    static public bool TryGetOptions(IConfiguration configuration, [NotNullWhen(returnValue: true)] out Options? options)
    {
        try
        {
            options = configuration.Get<Options>() ?? throw new Exception("No configuration found. Configuration can be set in appsettings.json or using command line options.");
            Validator.ValidateObject(options, new ValidationContext(options), true);
            return true;
        }
        catch (Exception ex)
        {
            options = null;
            Console.WriteLine(ex.Message);
            return false;
        }
    }
}
