// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Media;
using Azure.ResourceManager.Media.Models;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using System.Text;
using StreamExistingMP4Utils;

const string InputMP4FileName = "IgniteHD1800kbps.mp4";
const string DefaultStreamingEndpointName = "default";   // Change this to your Streaming Endpoint name.

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

// Creating a unique suffix so that we don't have name collisions if you run the sample
// multiple times without cleaning up.
string uniqueness = Guid.NewGuid().ToString()[..13];
string locatorName = $"locator-{uniqueness}";
string inputAssetName = $"input-{uniqueness}";
bool stopStreamingEndpoint = false;


// Create a new input Asset and upload the specified local video file into it.
var inputAsset = await CreateInputAssetAsync(mediaServicesAccount, inputAssetName, InputMP4FileName);

var streamingLocator = await CreateStreamingLocatorAsync(mediaServicesAccount, inputAsset.Data.Name, locatorName);

var streamingEndpoint = (await mediaServicesAccount.GetStreamingEndpoints().GetAsync(DefaultStreamingEndpointName)).Value;

if (streamingEndpoint.Data.ResourceState != StreamingEndpointResourceState.Running)
{
    Console.WriteLine("Streaming Endpoint is not running, starting now...");
    await streamingEndpoint.StartAsync(WaitUntil.Completed);

    // Since we started the endpoint, we should stop it in cleanup.
    stopStreamingEndpoint = true;
}

// Generate the Server manifest for streaming .ism file.
// This file is a simple SMIL 2.0 file format schema that includes references to the uploaded MP4 files in the XML.
var manifestsList = await CreateServerManifestsAsync(mediaServicesAccount, inputAsset, streamingLocator, streamingEndpoint);
var ismManifestName = manifestsList.FirstOrDefault();


Console.WriteLine();
Console.WriteLine("Getting the streaming manifest URLs for HLS and DASH:");
await PrintStreamingUrlsAsync(streamingLocator, streamingEndpoint);

Console.WriteLine("To try streaming, copy and paste the streaming URL into the Azure Media Player at 'http://aka.ms/azuremediaplayer'.");
Console.WriteLine("When finished, press ENTER to cleanup.");
Console.WriteLine();
Console.ReadLine();

await CleanUpAsync(null, null, inputAsset, null, streamingLocator, stopStreamingEndpoint, streamingEndpoint);

/// <summary>
/// Creates a new input Asset and uploads the specified local video file into it.
/// </summary>
/// <param name="mediaServicesAccount">The Media Services client.</param>
/// <param name="assetName">The Asset name.</param>
/// <param name="fileToUpload">The file you want to upload into the Asset.</param>
/// <returns></returns>
static async Task<MediaAssetResource> CreateInputAssetAsync(MediaServicesAccountResource mediaServicesAccount, string assetName, string fileToUpload)
{
    // In this example, we are assuming that the Asset name is unique.
    MediaAssetResource asset;

    try
    {
        asset = await mediaServicesAccount.GetMediaAssets().GetAsync(assetName);

        // The Asset already exists and we are going to overwrite it. In your application, if you don't want to overwrite
        // an existing Asset, use an unique name.
        Console.WriteLine($"Warning: The Asset named {assetName} already exists. It will be overwritten.");
    }
    catch (RequestFailedException)
    {
        // Call Media Services API to create an Asset.
        // This method creates a container in storage for the Asset.
        // The files (blobs) associated with the Asset will be stored in this container.
        Console.WriteLine("Creating an input Asset...");
        asset = (await mediaServicesAccount.GetMediaAssets().CreateOrUpdateAsync(WaitUntil.Completed, assetName, new MediaAssetData())).Value;
    }

    // Use Media Services API to get back a response that contains
    // SAS URL for the Asset container into which to upload blobs.
    // That is where you would specify read-write permissions 
    // and the expiration time for the SAS URL.
    var sasUriCollection = asset.GetStorageContainerUrisAsync(
        new MediaAssetStorageContainerSasContent
        {
            Permissions = MediaAssetContainerPermission.ReadWrite,
            ExpireOn = DateTime.UtcNow.AddHours(1)
        });

    var sasUri = await sasUriCollection.FirstOrDefaultAsync();

    // Use Storage API to get a reference to the Asset container
    // that was created by calling Asset's CreateOrUpdate method.
    var container = new BlobContainerClient(sasUri);
    BlobClient blob = container.GetBlobClient(Path.GetFileName(fileToUpload));

    // Use Storage API to upload the file into the container in storage.
    Console.WriteLine("Uploading a media file to the Asset...");
    await blob.UploadAsync(fileToUpload);

    return asset;
}

/// <summary>
/// Creates a StreamingLocator for the specified Asset and with the specified streaming policy name.
/// Once the StreamingLocator is created the output Asset is available to clients for playback.
/// </summary>
/// <param name="mediaServicesAccount">The Media Services client.</param>
/// <param name="assetName">The name of the output Asset.</param>
/// <param name="locatorName">The StreamingLocator name (unique in this case).</param>
/// <returns></returns>
static async Task<StreamingLocatorResource> CreateStreamingLocatorAsync(
    MediaServicesAccountResource mediaServicesAccount,
    string assetName,
    string locatorName)
{
    var locator = await mediaServicesAccount.GetStreamingLocators().CreateOrUpdateAsync(
        WaitUntil.Completed,
        locatorName,
        new StreamingLocatorData
        {
            AssetName = assetName,
            StreamingPolicyName = "Predefined_ClearStreamingOnly"
        });

    return locator.Value;
}

/// <summary>
/// Prints the streaming URLs.
/// </summary>
/// <param name="locator">The streaming locator.</param>
/// <param name="streamingEndpoint">The streaming endpoint.</param>
static async Task PrintStreamingUrlsAsync(
    StreamingLocatorResource locator,
    StreamingEndpointResource streamingEndpoint)
{
    var paths = await locator.GetStreamingPathsAsync();

    foreach (StreamingPath path in paths.Value.StreamingPaths)
    {
        Console.WriteLine($"The following formats are available for {path.StreamingProtocol.ToString().ToUpper()}:");
        foreach (string streamingFormatPath in path.Paths)
        {
            var uriBuilder = new UriBuilder()
            {
                Scheme = "https",
                Host = streamingEndpoint.Data.HostName,
                Path = streamingFormatPath
            };
            Console.WriteLine($"\t{uriBuilder}");
        }
        Console.WriteLine();
    }
}

/// <summary>
/// Creates the Server side .ism manifest files required to stream an Mp4 file uploaded to an asset with the proper encoding settings. 
/// </summary>
/// <returns>
/// A list of server side manifest files (.ism) created in the Asset folder. Typically this is only going to be a single .ism file. 
/// </returns>
static async Task<IList<string>> CreateServerManifestsAsync(MediaServicesAccountResource mediaServicesAccount, MediaAssetResource asset, StreamingLocatorResource locator, StreamingEndpointResource streamingEndpoint)
{

    // Create a short lived SAS URL to upload content into the Asset's container.  We use 5 minutes in this sample, but this can be a lot shorter.
    var assetContainerSas = asset.GetStorageContainerUrisAsync(new MediaAssetStorageContainerSasContent
    {
        Permissions = MediaAssetContainerPermission.ReadWriteDelete,
        ExpireOn = DateTime.Now.AddMinutes(5).ToUniversalTime()
    });

    var containerSasUrl = await assetContainerSas.FirstAsync();
    var storageContainer = new BlobContainerClient(containerSasUrl);

    // Create the Server Manifest .ism file here.  This is a SMIL 2.0 format XML file that points to the uploaded MP4 files in the asset container.
    // This file is required by the Streaming Endpoint to dynamically generate the HLS and DASH streams from the MP4 source file (when properly encoded.)
    GeneratedServerManifest serverManifest = await ManifestUtils.LoadAndUpdateManifestTemplateAsync(storageContainer);

    // Load the server manifest .ism content
    XDocument doc = XDocument.Parse(serverManifest.Content);

    // Upload the ism file to the Asset's container as blob
    BlobClient blob = storageContainer.GetBlobClient(serverManifest.FileName);
    using (var ms = new MemoryStream())
    {
        doc.Save(ms);
        ms.Position = 0;
        blob.Upload(ms);
    }

    // Get a manifest file list from the Storage container.
    // In this sectino we are going to check for the existence of a client manifest and determine if we need to generate a new one. 
    // If one exists, we do not generate it again. 

    var manifestFiles = await ManifestUtils.GetManifestFilesListFromStorageAsync(storageContainer);
    string ismcFileName = manifestFiles.FirstOrDefault(a => a.ToLower().Contains(".ismc"));
    string ismManifestFileName = manifestFiles.FirstOrDefault(a => a.ToLower().EndsWith(".ism"));

    // If there is no .ism then there's no reason to continue.  If there's no .ismc we need to add it.

    if (ismManifestFileName != null && ismcFileName == null)
    {
        Console.WriteLine("Asset {0} : it does not have an ISMC file.", asset.Data.Name);

        // let's try to read client manifest
        XDocument manifest = null;
        try
        {
            manifest = await GetClientManifestAsync(locator, streamingEndpoint);
        }
        catch (Exception)
        {
            Console.WriteLine("Error when trying to read client manifest for asset '{0}'.", asset.Data.Name);
            return null;
        }

        string ismcContentXml = manifest.ToString();
        if (ismcContentXml.Length == 0)
        {
            Console.WriteLine("Asset {0} : client manifest is empty.", asset.Data.Name);
            //error state, skip this asset
            return null;
        }

        if (ismcContentXml.IndexOf("<Protection>") > 0)
        {
            Console.WriteLine("Asset {0} : content is encrypted. Removing the protection header from the client manifest.", asset.Data.Name);
            //remove DRM from the ISCM manifest
            ismcContentXml = ManifestUtils.RemoveXmlNode(ismcContentXml);
        }

        string newIsmcFileName = ismManifestFileName.Substring(0, ismManifestFileName.IndexOf(".")) + ".ismc";
        await WriteStringToBlobAsync(ismcContentXml, newIsmcFileName, storageContainer);
        Console.WriteLine("Asset {0} : client manifest created.", asset.Data.Name);

        // Download the ISM so that we can modify it to include the ISMC file link.
        string ismXmlContent = await GetStringFromBlobAsync(storageContainer, ismManifestFileName);
        ismXmlContent = ManifestUtils.AddIsmcToIsm(ismXmlContent, newIsmcFileName);
        await WriteStringToBlobAsync(ismXmlContent, ismManifestFileName, storageContainer);
        Console.WriteLine("Asset {0} : server manifest updated.", asset.Data.Name);

        // update the ism to point to the ismc (download, modify, delete original, upload new)
    }

    // return the .ism manifest
    return (await ManifestUtils.GetManifestFilesListFromStorageAsync(storageContainer)).Where(a => a.ToLower().EndsWith(".ism")).ToList();
}


static async Task<XDocument> GetClientManifestAsync(StreamingLocatorResource locator, StreamingEndpointResource streamingEndpoint)
{
    Uri myuri = await ReturnSmoothStreamingUrlAsync(locator, streamingEndpoint);

    if (myuri != null)
    {
        return XDocument.Load(myuri.ToString());
    }
    else
    {
        throw new Exception("Streaming locator is null");
    }
}

static async Task WriteStringToBlobAsync(string ContentXml, string fileName, BlobContainerClient storageContainer)
{
    BlobClient blobClient = storageContainer.GetBlobClient(fileName);

    var content = Encoding.UTF8.GetBytes(ContentXml);
    using var ms = new MemoryStream(content);
    await blobClient.UploadAsync(ms, true);
}

static async Task<string> GetStringFromBlobAsync(BlobContainerClient storageContainer, string ismManifestFileName)
{
    BlobClient blobClient = storageContainer.GetBlobClient(ismManifestFileName);

    using var ms = new MemoryStream();
    await blobClient.DownloadToAsync(ms);
    return System.Text.Encoding.UTF8.GetString(ms.ToArray());
}

/// <summary>
/// Gets the smooth streaming Url.
/// </summary>
/// <param name="locator">The streaming locator.</param>
/// <param name="streamingEndpoint">The streaming endpoint.</param>
static async Task<Uri> ReturnSmoothStreamingUrlAsync(
    StreamingLocatorResource locator,
    StreamingEndpointResource streamingEndpoint)
{
    var paths = await locator.GetStreamingPathsAsync();

    var smooth = paths.Value.StreamingPaths.Where(p => p.StreamingProtocol == StreamingPolicyStreamingProtocol.SmoothStreaming).First();

    var urib = new UriBuilder()
    {
        Scheme = "https",
        Host = streamingEndpoint.Data.HostName,
        Path = smooth.Paths[0]
    };

    return urib.Uri;
}

/// <summary>
/// Delete the resources that were created.
/// </summary>
/// <param name="transform">The transform.</param>
/// <param name="job">The Job.</param>
/// <param name="inputAsset">The input Asset.</param>
/// <param name="outputAsset">The output Asset.</param>
/// <param name="streamingLocator">The streaming locator. </param>
/// <param name="stopEndpoint">Stop endpoint if true, keep endpoint running if false.</param>
/// <param name="streamingEndpoint">The streaming endpoint.</param>
/// <returns>A task.</returns>
static async Task CleanUpAsync(
    MediaTransformResource? transform,
    MediaJobResource? job,
    MediaAssetResource? inputAsset,
    MediaAssetResource? outputAsset,
    StreamingLocatorResource? streamingLocator,
    bool stopEndpoint,
    StreamingEndpointResource? streamingEndpoint)
{
    if (job != null)
    {
        await job.DeleteAsync(WaitUntil.Completed);
    }

    if (transform != null)
    {
        await transform.DeleteAsync(WaitUntil.Completed);
    }

    if (inputAsset != null)
    {
        await inputAsset.DeleteAsync(WaitUntil.Completed);
    }

    if (outputAsset != null)
    {
        await outputAsset.DeleteAsync(WaitUntil.Completed);
    }

    if (streamingLocator != null)
    {
        await streamingLocator.DeleteAsync(WaitUntil.Completed);
    }

    if (streamingEndpoint != null)
    {
        if (stopEndpoint)
        {
            // Because we started the endpoint, we'll stop it.
            await streamingEndpoint.StopAsync(WaitUntil.Completed);
        }
        else
        {
            // We will keep the endpoint running because it was not started by us. There are costs to keep it running.
            // Please refer https://azure.microsoft.com/en-us/pricing/details/media-services/ for pricing. 
            Console.WriteLine($"The Streaming Endpoint '{streamingEndpoint.Data.Name}' is running. To stop further billing for the Streaming Endpoint, please stop it using the Azure portal.");
        }
    }
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
