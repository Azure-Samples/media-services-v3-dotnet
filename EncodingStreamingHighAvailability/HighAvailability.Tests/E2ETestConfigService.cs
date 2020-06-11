namespace HighAvailability.Tests
{
    using HighAvailability.Interfaces;
    using HighAvailability.Models;
    using Microsoft.Azure.KeyVault;
    using Microsoft.Azure.Services.AppAuthentication;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    /// <summary>
    /// This class is a configuration container for test project only. It is not use in main sample.
    /// </summary>
    public class E2ETestConfigService : IConfigService
    {
        /// <summary>
        /// KeyVault name to load configuration
        /// </summary>
        private readonly string keyVaultName;

        /// <summary>
        /// Binary streaming key used for clear key streaming.
        /// </summary>
        private byte[] clearKeyStreamingKey;

        /// <summary>
        /// Construct config container and load default settings.
        /// </summary>
        /// <param name="keyVaultName"></param>
        public E2ETestConfigService(string keyVaultName)
        {
            this.keyVaultName = keyVaultName ?? throw new ArgumentNullException(nameof(keyVaultName));
            this.MediaServiceInstanceHealthTableName = "MediaServiceInstanceHealth";
            this.JobOutputStatusTableName = "JobOutputStatus";
            this.MediaServiceCallHistoryTableName = "MediaServiceCallHistory";
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
            this.TimeWindowToLoadMediaServiceCallsInMinutes = 480;
            this.TimeSinceLastUpdateToForceJobResyncInMinutes = 60;
            this.SuccessRateForHealthyState = 0.9f;
            this.SuccessRateForUnHealthyState = 0.7f;
            this.TimeDurationInMinutesToVerifyJobStatus = 10;
            this.ContentKeyPolicyName = "TestPolicyName";
            this.TokenAudience = "TestTokenAudience";
            this.TokenIssuer = "TestTokenIssuer";
        }

        /// <summary>
        /// Azure Table name to store Azure Media Service instance health information.
        /// </summary>
        public string MediaServiceInstanceHealthTableName { get; private set; }

        /// <summary>
        /// Azure Table name to store job output status records.
        /// </summary>
        public string JobOutputStatusTableName { get; private set; }

        /// <summary>
        /// Azure Table name to store Azure Media Service call history
        /// </summary>
        public string MediaServiceCallHistoryTableName { get; private set; }

        /// <summary>
        /// Azure Storage Account connection string. This account hosts Azure Queues.
        /// </summary>
        public string StorageAccountConnectionString { get; private set; }

        /// <summary>
        /// Cosmos Db connection string for table storage data. Media Service Instance health table and job output status table use this connection string.
        /// </summary>
        public string TableStorageAccountConnectionString { get; private set; }

        /// <summary>
        /// Azure Queue name to store provisioning requests.
        /// </summary>
        public string ProvisioningRequestQueueName { get; private set; }

        /// <summary>
        /// Azure Queue name to store job verification requests.
        /// </summary>
        public string JobVerificationRequestQueueName { get; private set; }

        /// <summary>
        /// Azure Queue name to store all incoming job requests.
        /// </summary>
        public string JobRequestQueueName { get; private set; }

        /// <summary>
        /// Azure Queue name to store provision completed events.
        /// </summary>
        public string ProvisioningCompletedEventQueueName { get; private set; }

        /// <summary>
        /// Azure Front Door host name. This is used to generate URLs to stream content.
        /// </summary>
        public string FrontDoorHostName { get; private set; }

        /// <summary>
        /// Expected max number of minutes required to complete encoding job. If job stays in process longer, it is marked as "stuck" and this information is used to determine instance health.
        /// </summary>
        public int NumberOfMinutesInProcessToMarkJobStuck { get; private set; }

        /// <summary>
        /// This value is used to determine how far back to go to load job status when instance health is calculated. 
        /// </summary>
        public int TimeWindowToLoadJobsInMinutes { get; private set; }

        /// <summary>
        ///This value is used to determine how far back to go to load Azure Media Services call history when instance health is calculated. 
        /// </summary>
        public int TimeWindowToLoadMediaServiceCallsInMinutes { get; private set; }

        /// <summary>
        /// This value is used to determine when to trigger manual job output status refresh from Azure Media Service API. Sometimes EventGridEvents are missing and manual refresh is required to correctly calculate Azure Media Service instance health.
        /// </summary>
        public int TimeSinceLastUpdateToForceJobResyncInMinutes { get; private set; }

        /// <summary>
        /// Success/Total job ratio threshold to determine when Azure Media Service instance is healthy.
        /// </summary>
        public float SuccessRateForHealthyState { get; private set; }

        /// <summary>
        /// Success/Total job ratio threshold to determine when Azure Media Service instance is unhealthy.
        /// </summary>
        public float SuccessRateForUnHealthyState { get; private set; }

        /// <summary>
        /// How far in future to trigger job verification logic. This time should be longer than expected job duration.
        /// </summary>
        public int TimeDurationInMinutesToVerifyJobStatus { get; private set; }

        /// <summary>
        /// Content key policy name for clear key streaming locator configuration.
        /// </summary>
        public string ContentKeyPolicyName { get; private set; }

        /// <summary>
        /// Token issuer for clear key streaming token.
        /// </summary>
        public string TokenIssuer { get; private set; }

        /// <summary>
        /// Token audience for clear key streaming token
        /// </summary>
        public string TokenAudience { get; private set; }

        /// <summary>
        /// Dictionary to store Azure Media Service instance configuration. Key is Azure Media Service account name.
        /// </summary>
        public IDictionary<string, MediaServiceConfigurationModel> MediaServiceInstanceConfiguration { get; private set; }

        /// <summary>
        /// Dictionary to store Azure Media Service storage account connection strings. Key is Azure Media Service account name.
        /// </summary>
        public IDictionary<string, string> MediaServiceInstanceStorageAccountConnectionStrings { get; private set; }

        /// <summary>
        /// Clear key streaming binary key data.
        /// </summary>
        /// <returns></returns>
        public byte[] GetClearKeyStreamingKey()
        {
            return this.clearKeyStreamingKey;
        }

        /// <summary>
        /// Load configuration from environment properties and keyvault
        /// </summary>
        /// <returns>Task of async operation</returns>
        public async Task LoadConfigurationAsync()
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            using var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
            var secret = await keyVaultClient.GetSecretAsync($"https://{this.keyVaultName}.vault.azure.net/secrets/AMSConfiguration").ConfigureAwait(false);

            var amsConfigurationList = JsonConvert.DeserializeObject<List<MediaServiceConfigurationModel>>(secret.Value);
            this.MediaServiceInstanceConfiguration = amsConfigurationList.ToDictionary(i => i.AccountName);

            secret = await keyVaultClient.GetSecretAsync($"https://{this.keyVaultName}.vault.azure.net/secrets/StorageAccountConnectionString").ConfigureAwait(false);
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
