#pragma warning disable CA1707 // Identifiers should not contain underscores
namespace media_services_high_availability_shared.Helpers
#pragma warning restore CA1707 // Identifiers should not contain underscores
{
    using media_services_high_availability_shared.Models;
    using Microsoft.Azure.Management.Media;
    using Microsoft.Azure.Management.Media.Models;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using Microsoft.Rest;
    using Microsoft.Rest.Azure.Authentication;
    using System;
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

            var credentials = await GetCredentialsAsync(config).ConfigureAwait(false);

            return new AzureMediaServicesClient(config.ArmEndpoint, credentials)
            {
                SubscriptionId = config.SubscriptionId,
            };
        }

        /// <summary>
        /// Create the ServiceClientCredentials object based on the credentials
        /// supplied in local configuration file.
        /// </summary>
        /// <param name="config">The parm is of type ConfigWrapper. This class reads values from local configuration file.</param>
        /// <returns></returns>
        // <GetCredentialsAsync>
        public static async Task<ServiceClientCredentials> GetCredentialsAsync(MediaServiceConfigurationModel config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var clientCredential = new ClientCredential(config.AadClientId, config.AadSecret);
            return await ApplicationTokenProvider.LoginSilentAsync(config.AadTenantId, clientCredential, ActiveDirectoryServiceSettings.Azure).ConfigureAwait(false);
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
    }
}
