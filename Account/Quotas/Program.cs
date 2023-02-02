// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Identity;
using Azure.Monitor.Query;
using Azure.ResourceManager.Media;
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

var quotas = new QuotaMetrics[]
{
                new QuotaMetrics("Assets", "AssetCount", "AssetQuota"),
                new QuotaMetrics("Content Key Polices", "ContentKeyPolicyCount", "ContentKeyPolicyQuota"),
                new QuotaMetrics("Streaming Policies", "StreamingPolicyCount", "StreamingPolicyQuota"),
                new QuotaMetrics("Live Events", "ChannelsAndLiveEventsCount", "MaxChannelsAndLiveEventsCount"),
                new QuotaMetrics("Running Live Events", "RunningChannelsAndLiveEventsCount", "MaxRunningChannelsAndLiveEventsCount"),
                new QuotaMetrics("Transforms", null, "TransformQuota"),
                new QuotaMetrics("Jobs", null, "JobQuota"),
                new QuotaMetrics("Jobs Scheduled", "JobsScheduled", null)
};

var allQuotaNames = quotas
    .Select(q => q.CountMetric)
    .Concat(quotas.Select(q => q.QuotaMetric))
    .Where(v => v != null);

var credential = new DefaultAzureCredential(
    new DefaultAzureCredentialOptions { ExcludeManagedIdentityCredential = true });

var MediaServicesResource = MediaServicesAccountResource.CreateResourceIdentifier(
    subscriptionId: options.AZURE_SUBSCRIPTION_ID.ToString(),
    resourceGroupName: options.AZURE_RESOURCE_GROUP,
    accountName: options.AZURE_MEDIA_SERVICES_ACCOUNT_NAME);

var metricsClient = new MetricsQueryClient(credential);

var results = await metricsClient.QueryResourceAsync(
    MediaServicesResource.ToString(),
    allQuotaNames);

var values = results.Value.Metrics.ToDictionary(
    m => m.Name,
    m => m.TimeSeries.Last().Values.Last().Average);

var formatString = "{0,-20}{1,10}{2,10}";
Console.WriteLine(formatString, "Resource", "Current", "Quota");
Console.WriteLine(formatString, "--------", "----------", "--------");

foreach (var quota in quotas)
{
    var countValue = quota.CountMetric != null ? values[quota.CountMetric] : null;
    var quotaValue = quota.QuotaMetric != null ? values[quota.QuotaMetric] : null;

    Console.WriteLine(formatString, quota.Name, countValue, quotaValue);
}
record QuotaMetrics(string Name, string? CountMetric, string? QuotaMetric);

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
