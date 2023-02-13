// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Media;
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
Console.WriteLine($"Resource group name:         {options.AZURE_RESOURCE_GROUP}");
Console.WriteLine($"Media Services account name: {options.AZURE_MEDIA_SERVICES_ACCOUNT_NAME}");
Console.WriteLine();

// First we construct the ArmClient using DefaultAzureCredential
// This will use the Environment variables set for the current logged in user. 
// Use the VS Code Azure login command, or the CLI 'az login' to set the environment variables
var client = new ArmClient(new DefaultAzureCredential());

SubscriptionCollection subscriptions = client.GetSubscriptions();
SubscriptionResource subscription = subscriptions.Get(options.AZURE_SUBSCRIPTION_ID.ToString());
Console.WriteLine($"Got subscription: {subscription.Data.DisplayName}");

ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();
ResourceGroupResource resourceGroup = await resourceGroups.GetAsync(options.AZURE_RESOURCE_GROUP);

// Get all the media accounts in as resource group
MediaServicesAccountCollection mediaServices = resourceGroup.GetMediaServicesAccounts();

await foreach (var amsAccount in mediaServices)
{
    Console.WriteLine($"name= {amsAccount.Data.Name}");
    Console.WriteLine($"location= {amsAccount.Data.Location}");
}

// Get a specific media account 
MediaServicesAccountResource mediaService = await resourceGroup.GetMediaServicesAccountAsync(options.AZURE_MEDIA_SERVICES_ACCOUNT_NAME);

Console.WriteLine($"Got media service : {mediaService.Data.Name}");

/// <summary>
/// Class to manage the settings which come from appsettings.json or command line parameters.
/// </summary>
internal class Options
{
    [Required]
    public Guid? AZURE_SUBSCRIPTION_ID { get; set; }

    [Required]
    public string? AZURE_RESOURCE_GROUP { get; set; }

    [Required]
    public string? AZURE_MEDIA_SERVICES_ACCOUNT_NAME { get; set; }

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
