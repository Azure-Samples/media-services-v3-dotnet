namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using Microsoft.Azure.KeyVault;
    using Microsoft.Azure.Services.AppAuthentication;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;

    public class ConfigService : IConfigService
    {
        private readonly string keyVaultName;
        private readonly string AMSConfigurationKeyName = "AMSConfiguration";
        private readonly string FrontDoorHostNameKeyName = "FrontDoorHostName";

        private byte[] clearKeyStreamingKey;

        public ConfigService(string keyVaultName)
        {
            this.keyVaultName = keyVaultName ?? throw new ArgumentNullException(nameof(keyVaultName));
            this.MediaServiceInstanceHealthTableName = "MediaServiceInstanceHealth";
            this.JobOutputStatusTableName = "JobOutputStatus";
            this.StreamProvisioningRequestQueueName = "stream-provisioning-requests";
            this.JobVerificationRequestQueueName = "job-verification-requests";
            this.JobRequestQueueName = "job-requests";
            this.StreamProvisioningEventQueueName = "stream-provisioning-events";
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

        public string StreamProvisioningRequestQueueName { get; private set; }

        public string JobVerificationRequestQueueName { get; private set; }

        public string JobRequestQueueName { get; private set; }

        public string StreamProvisioningEventQueueName { get; private set; }

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

        public async Task LoadConfigurationAsync()
        {
            var amsConfigurationString = Environment.GetEnvironmentVariable(this.AMSConfigurationKeyName);

            if (amsConfigurationString == null)
            {
                throw new Exception($"Function confo does not have {this.AMSConfigurationKeyName} value");
            }

            var amsConfigurationList = JsonConvert.DeserializeObject<List<MediaServiceConfigurationModel>>(amsConfigurationString);
            this.MediaServiceInstanceConfiguration = amsConfigurationList.ToDictionary(i => i.AccountName);

            this.FrontDoorHostName = Environment.GetEnvironmentVariable(this.FrontDoorHostNameKeyName);

            if (this.FrontDoorHostName == null)
            {
                throw new Exception($"Function confo does not have {this.FrontDoorHostNameKeyName} value");
            }

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
