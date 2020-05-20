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
    using System.Threading.Tasks;

    public class E2ETestConfigService : IConfigService
    {
        private readonly string keyVaultName;

        public E2ETestConfigService(string keyVaultName)
        {
            this.keyVaultName = keyVaultName ?? throw new ArgumentNullException(nameof(keyVaultName));
            this.MediaServiceInstanceHealthTableName = "MediaServiceInstanceHealth";
            this.JobOutputStatusTableName = "JobOutputStatus";
            this.StreamProvisioningRequestQueueName = "stream-provisioning-requests";
            this.JobVerificationRequestQueueName = "job-verification-requests";
            this.JobRequestQueueName = "job-requests";
            this.StreamProvisioningEventQueueName = "stream-provisioning-events";
            this.MediaServiceInstanceConfiguration = new Dictionary<string, MediaServiceConfigurationModel>();
            this.StorageAccountConnectionString = string.Empty;
            this.TableStorageAccountConnectionString = string.Empty;
            this.FrontDoorHostName = "contoso.com";
            this.NumberOfMinutesInProcessToMarkJobStuck = 60;
            this.TimeWindowToLoadJobsInMinutes = 11480;
            this.TimeSinceLastUpdateToForceJobResyncInMinutes = 60;
            this.SuccessRateForHealthyState = 0.9f;
            this.SuccessRateForUnHealthyState = 0.7f;
            this.TimeDurationInMinutesToVerifyJobStatus = 10;
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

        public IDictionary<string, MediaServiceConfigurationModel> MediaServiceInstanceConfiguration { get; private set; }

        public async Task LoadConfigurationAsync()
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            using (var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback)))
            {
                var secret = await keyVaultClient.GetSecretAsync($"https://{this.keyVaultName}.vault.azure.net/secrets/StorageAccountConnectionString").ConfigureAwait(false);
                this.StorageAccountConnectionString = secret.Value;

                secret = await keyVaultClient.GetSecretAsync($"https://{this.keyVaultName}.vault.azure.net/secrets/TableStorageAccountConnectionString").ConfigureAwait(false);
                this.TableStorageAccountConnectionString = secret.Value;
            }

            // Copy this from azure function configuration AMSConfiguration key. 
            // Use advanced edit in configuration screen to get value with encoded quotes
            var amsConfigurationString = "[{\"SubscriptionId\":\"465d1912-ea98-4b36-94d2-fabbf55fe648\",\"ResourceGroup\":\"ha-test2\",\"AccountName\":\"sipetrikamseastus\"},{\"SubscriptionId\":\"465d1912-ea98-4b36-94d2-fabbf55fe648\",\"ResourceGroup\":\"ha-test2\",\"AccountName\":\"sipetrikamswestus\"}]";
            var amsConfigurationList = JsonConvert.DeserializeObject<List<MediaServiceConfigurationModel>>(amsConfigurationString);
            this.MediaServiceInstanceConfiguration = amsConfigurationList.ToDictionary(i => i.AccountName);
        }
    }
}
