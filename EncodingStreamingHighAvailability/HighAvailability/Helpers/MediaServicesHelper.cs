namespace HighAvailability.Helpers
{
    using HighAvailability.Models;
    using Microsoft.Azure.EventGrid.Models;
    using Microsoft.Azure.Management.Media;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.Azure.Services.AppAuthentication;
    using Microsoft.Rest;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public static class MediaServicesHelper
    {
        /// <summary>
        /// Creates the AzureMediaServicesClient object based on the credentials
        /// supplied in local configuration file.
        /// </summary>
        /// <param name="config">The parm is of type ConfigWrapper. This class reads values from local configuration file.</param>
        /// <returns></returns>
        // <CreateMediaServicesClient>
        public static async Task<IAzureMediaServicesClient> CreateMediaServicesClientAsync(MediaServiceConfigurationModel config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            // Authenticate to Azure.
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var accessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com").ConfigureAwait(false);
            ServiceClientCredentials credentials = new TokenCredentials(accessToken);

            // Establish a connection to Media Services.
            return new AzureMediaServicesClient(credentials)
            {
                SubscriptionId = config.SubscriptionId
            };
        }

        public static async Task<Transform> EnsureTransformExists(IAzureMediaServicesClient client, string resourceGroupName, string accountName, string transformName, Preset preset)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            var transform = client.Transforms.Get(resourceGroupName, accountName, transformName);

            if (transform == null)
            {
                var outputs = new TransformOutput[]
                {
                    new TransformOutput(preset),
                };

                transform = await client.Transforms.CreateOrUpdateAsync(resourceGroupName, accountName, transformName, outputs).ConfigureAwait(false);
            }

            return transform;
        }

        public static bool IsSystemError(Job job, string jobOutputAssetName)
        {
            if (job.State == JobState.Error)
            {
                foreach (var jobOutput in job.Outputs)
                {
                    if (jobOutput is JobOutputAsset)
                    {
                        var jobOutputAsset = (JobOutputAsset)jobOutput;
                        if (jobOutputAsset.State == JobState.Error && jobOutputAsset.AssetName.Equals(jobOutputAssetName, StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (jobOutputAsset.Error.Retry == JobRetry.MayRetry)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public static JobState GetJobOutputState(Job job, string jobOutputAssetName)
        {
            foreach (var jobOutput in job.Outputs)
            {
                if (jobOutput is JobOutputAsset)
                {
                    var jobOutputAsset = (JobOutputAsset)jobOutput;
                    if (jobOutputAsset.AssetName.Equals(jobOutputAssetName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return jobOutputAsset.State;
                    }
                }
            }

            return null;
        }

        public static bool IsSystemError(MediaJobOutputAsset jobOutput)
        {
            if (jobOutput.State == MediaJobState.Error)
            {
                if (jobOutput.Error.Retry == MediaJobRetry.MayRetry)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
