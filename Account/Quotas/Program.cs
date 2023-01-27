﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Identity;
using Azure.Monitor.Query;
using Azure.ResourceManager.Media;

var quotas = new QuotaMetrics[]
{
    new QuotaMetrics("Assets", "AssetCount", "AssetQuota"),
    new QuotaMetrics("Content Key Polices", "ContentKeyPolicyCount", "ContentKeyPolicyQuota"),
    new QuotaMetrics("Streaming Policies", "StreamingPolicyCount", "StreamingPolicyQuota"),
    new QuotaMetrics("Live Events", "ChannelsAndLiveEventsCount", "MaxChannelsAndLiveEventsCount"),
    new QuotaMetrics("Running Live Events", "RunningChannelsAndLiveEventsCount", "MaxRunningChannelsAndLiveEventsCount"),
    new QuotaMetrics("Transforms", null, "TransformQuota"),
    new QuotaMetrics("Jobs", null, "JobQuota"),
    new QuotaMetrics("Jobs Scheduled", "JobsScheduled", null),
};

var allQuotaNames = quotas
    .Select(q => q.CountMetric)
    .Concat(quotas.Select(q => q.QuotaMetric))
    .Where(v => v != null);

var credential = new DefaultAzureCredential(
    new DefaultAzureCredentialOptions { ExcludeManagedIdentityCredential = true });

var mediaServicesResourceId = MediaServicesAccountResource.CreateResourceIdentifier(
    subscriptionId: "---set-your-subscription-id-here---",
    resourceGroupName: "---set-your-resource-group-name-here---",
    accountName: "---set-your-media-services-account-name-here---");

var metricsClient = new MetricsQueryClient(credential);

var results = await metricsClient.QueryResourceAsync(
    mediaServicesResourceId.ToString(),
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