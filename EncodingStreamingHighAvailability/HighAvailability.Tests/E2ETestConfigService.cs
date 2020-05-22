namespace HighAvailability.Tests
{
    using HighAvailability.Models;
    using HighAvailability.Services;
    using Microsoft.Azure.KeyVault;
    using Microsoft.Azure.Services.AppAuthentication;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    public class E2ETestConfigService : IConfigService
    {
        private readonly string keyVaultName;
        private byte[] clearKeyStreamingKey;

        public E2ETestConfigService(string keyVaultName)
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

        public async Task LoadConfigurationAsync()
        {
            // Copy this from azure function configuration AMSConfiguration key. 
            // Use advanced edit in configuration screen to get value with encoded quotes
            var amsConfigurationString = "[{\"SubscriptionId\":\"465d1912-ea98-4b36-94d2-fabbf55fe648\",\"ResourceGroup\":\"ha-test2\",\"AccountName\":\"sipetrikamseastus\"},{\"SubscriptionId\":\"465d1912-ea98-4b36-94d2-fabbf55fe648\",\"ResourceGroup\":\"ha-test2\",\"AccountName\":\"sipetrikamswestus\"}]";
            var amsConfigurationList = JsonConvert.DeserializeObject<List<MediaServiceConfigurationModel>>(amsConfigurationString);
            this.MediaServiceInstanceConfiguration = amsConfigurationList.ToDictionary(i => i.AccountName);

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

                using (var rng = new RNGCryptoServiceProvider())
                {
                    this.clearKeyStreamingKey = new byte[40];
                    rng.GetBytes(this.clearKeyStreamingKey);
                    await keyVaultClient.SetSecretAsync($"https://{this.keyVaultName}.vault.azure.net", "ClearKeyStreamingKey", Convert.ToBase64String(this.clearKeyStreamingKey)).ConfigureAwait(false);
                }
            }
        }
    }
}
