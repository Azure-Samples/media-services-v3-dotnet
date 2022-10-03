// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Media;

var MediaServiceAccount = MediaServicesAccountResource.CreateResourceIdentifier(
    subscriptionId: "---set-your-subscription-id-here---",
    resourceGroupName: "---set-your-resource-group-name-here---",
    accountName: "---set-your-media-services-account-name-here---");

var credential = new DefaultAzureCredential(includeInteractiveCredentials: true);
var armClient = new ArmClient(credential);

var mediaServicesAccount = armClient.GetMediaServicesAccountResource(MediaServiceAccount);

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
