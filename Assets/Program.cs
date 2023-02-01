// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Media;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

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

var mediaServiceAccountId = MediaServicesAccountResource.CreateResourceIdentifier(
    subscriptionId: options.AZURE_SUBSCRIPTION_ID.ToString(),
    resourceGroupName: options.AZURE_RESOURCE_GROUP,
    accountName: options.AZURE_MEDIA_SERVICES_ACCOUNT_NAME);

var credential = new DefaultAzureCredential(includeInteractiveCredentials: true);
var armClient = new ArmClient(credential);

var mediaServicesAccount = armClient.GetMediaServicesAccountResource(mediaServiceAccountId);

// List all assets in the account
Console.WriteLine("Listing all the assets in this account");
await foreach (var asset in mediaServicesAccount.GetMediaAssets().GetAllAsync())
{
    Console.WriteLine($" - {asset.Data.Name,-45}  ID: {asset.Data.AssetId}  Container: {asset.Data.Container}");
}

// Create a new asset
Console.WriteLine("Creating a new asset");

// Create a new asset setting the description, alternate ID, a custom container name in storage to override the default
// naming which uses "asset-" + Guid.NewGuid()
var newAsset = await mediaServicesAccount.GetMediaAssets().CreateOrUpdateAsync(
    WaitUntil.Completed,
    assetName: "myAsset" + Guid.NewGuid(),
    new MediaAssetData
    {
        Description = "My Video description",
        AlternateId = "12345",
        Container = "my-custom-name-" + Guid.NewGuid()
    });

Console.WriteLine($"Created a new asset: '{newAsset.Value.Data.Name}' in with storage container '{newAsset.Value.Data.Container}'");

// List assets filtering by date
var dateFilter = DateTime.UtcNow.AddDays(-1).ToString("O", DateTimeFormatInfo.InvariantInfo);
await foreach (var asset in mediaServicesAccount.GetMediaAssets().GetAllAsync(filter: $"properties/created gt {dateFilter}"))
{
    Console.WriteLine($" - {asset.Data.Name,-45}  Created: {asset.Data.CreatedOn}");
}

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