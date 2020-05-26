namespace HighAvailability.Services
{
    using HighAvailability.Interfaces;
    using HighAvailability.Models;
    using Microsoft.Azure.KeyVault;
    using Microsoft.Azure.Services.AppAuthentication;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Configuration container implementation.
    /// </summary>
    public class ConfigService : IConfigService
    {
        /// <summary>
        /// KeyVault name to load configuration
        /// </summary>
        private readonly string keyVaultName;

        /// <summary>
        /// Environment property name to load Azure Media Service configuration data.
        /// </summary>
        private readonly string AMSConfigurationKeyName = "AMSConfiguration";

        /// <summary>
        /// Environment property name to load Azure Front Door host name.
        /// </summary>
        private readonly string FrontDoorHostNameKeyName = "FrontDoorHostName";

        /// <summary>
        /// Binary sreaming key used for clear key streaming.
        /// </summary>
        private byte[] clearKeyStreamingKey;

        /// <summary>
        /// Construct config container and load default settings.
        /// </summary>
        /// <param name="keyVaultName"></param>
        public ConfigService(string keyVaultName)
        {
            this.keyVaultName = keyVaultName ?? throw new ArgumentNullException(nameof(keyVaultName));
            this.MediaServiceInstanceHealthTableName = "MediaServiceInstanceHealth";
            this.JobOutputStatusTableName = "JobOutputStatus";
            this.ProvisioningRequestQueueName = "provisioning-requests";
            this.JobVerificationRequestQueueName = "job-verification-requests";
            this.JobRequestQueueName = "job-requests";
            this.ProvisioningCompletedEventQueueName = "provisioning-completed-events";
            this.MediaServiceInstanceConfiguration = new Dictionary<string, MediaServiceConfigurationModel>();
            this.MediaServiceInstanceStorageAccountConnectionStrings = new Dictionary<string, string>();
            this.StorageAccountConnectionString = string.Empty;
            this.TableStorageAccountConnectionString = string.Empty;
            this.FrontDoorHostName = string.Empty;
            this.NumberOfMinutesInProcessToMarkJobStuck = 60;
            this.TimeWindowToLoadJobsInMinutes = 11480;
            this.TimeSinceLastUpdateToForceJobResyncInMinutes = 60;
            this.SuccessRateForHealthyState = 0.9f;
            this.SuccessRateForUnHealthyState = 0.7f;
            this.TimeDurationInMinutesToVerifyJobStatus = 10;
            this.ContentKeyPolicyName = "TestPolicyName";
            this.TokenAudience = "TestTokenAudience";
            this.TokenIssuer = "TestTokenIssuer";
        }

        public string MediaServiceInstanceHealthTableName { get; private set; }

        public string JobOutputStatusTableName { get; private set; }

        public string StorageAccountConnectionString { get; private set; }

        public string TableStorageAccountConnectionString { get; private set; }

        public string ProvisioningRequestQueueName { get; private set; }

        public string JobVerificationRequestQueueName { get; private set; }

        public string JobRequestQueueName { get; private set; }

        public string ProvisioningCompletedEventQueueName { get; private set; }

        public string FrontDoorHostName { get; private set; }

        public int NumberOfMinutesInProcessToMarkJobStuck { get; private set; }

        public int TimeWindowToLoadJobsInMinutes { get; private set; }

        public int TimeSinceLastUpdateToForceJobResyncInMinutes { get; private set; }

        public float SuccessRateForHealthyState { get; private set; }

        public float SuccessRateForUnHealthyState { get; private set; }

        public int TimeDurationInMinutesToVerifyJobStatus { get; private set; }

        public string ContentKeyPolicyName { get; private set; }

        public string TokenIssuer { get; private set; }

        public string TokenAudience { get; private set; }

        public IDictionary<string, MediaServiceConfigurationModel> MediaServiceInstanceConfiguration { get; private set; }

        public IDictionary<string, string> MediaServiceInstanceStorageAccountConnectionStrings { get; private set; }

        public byte[] GetClearKeyStreamingKey()
        {
            return this.clearKeyStreamingKey;
        }

        /// <summary>
        /// Load configuration from environment properties and keyvault
        /// </summary>
        /// <returns></returns>
        public async Task LoadConfigurationAsync()
        {
            // this value is set by ARM deployment script
            var amsConfigurationString = Environment.GetEnvironmentVariable(this.AMSConfigurationKeyName);

            if (amsConfigurationString == null)
            {
                throw new Exception($"Function confo does not have {this.AMSConfigurationKeyName} value");
            }

            var amsConfigurationList = JsonConvert.DeserializeObject<List<MediaServiceConfigurationModel>>(amsConfigurationString);
            this.MediaServiceInstanceConfiguration = amsConfigurationList.ToDictionary(i => i.AccountName);

            // this value is set by ARM deployment script
            this.FrontDoorHostName = Environment.GetEnvironmentVariable(this.FrontDoorHostNameKeyName);

            if (this.FrontDoorHostName == null)
            {
                throw new Exception($"Function confo does not have {this.FrontDoorHostNameKeyName} value");
            }

            // All keyvault secrets are set by ARM deployment script
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            using (var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback)))
            {
                var secret = await keyVaultClient.GetSecretAsync($"https://{this.keyVaultName}.vault.azure.net/secrets/StorageAccountConnectionString").ConfigureAwait(false);
                this.StorageAccountConnectionString = secret.Value;

                secret = await keyVaultClient.GetSecretAsync($"https://{this.keyVaultName}.vault.azure.net/secrets/TableStorageAccountConnectionString").ConfigureAwait(false);
                this.TableStorageAccountConnectionString = secret.Value;

                foreach (var configuration in this.MediaServiceInstanceConfiguration)
                {
                    secret = await keyVaultClient.GetSecretAsync($"https://{this.keyVaultName}.vault.azure.net/secrets/AMSStorageAccountConnectionString-{configuration.Value.AccountName}").ConfigureAwait(false);
                    this.MediaServiceInstanceStorageAccountConnectionStrings.Add(configuration.Value.AccountName, secret.Value);
                }

                secret = await keyVaultClient.GetSecretAsync($"https://{this.keyVaultName}.vault.azure.net/secrets/ClearKeyStreamingKey").ConfigureAwait(false);
                this.clearKeyStreamingKey = Convert.FromBase64String(secret.Value);
            }
        }
    }
}
