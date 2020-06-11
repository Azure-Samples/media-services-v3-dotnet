namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface to define all the properties and methods for configuration container.
    /// </summary>
    public interface IConfigService
    {
        /// <summary>
        /// Azure Table name to store Azure Media Service instance health information.
        /// </summary>
        string MediaServiceInstanceHealthTableName { get; }

        /// <summary>
        /// Azure Table name to store job output status records.
        /// </summary>
        string JobOutputStatusTableName { get; }

        /// <summary>
        /// Azure Table name to store Azure Media Service call history
        /// </summary>
        string MediaServiceCallHistoryTableName { get; }

        /// <summary>
        /// Azure Queue name to store provisioning requests.
        /// </summary>
        string ProvisioningRequestQueueName { get; }

        /// <summary>
        /// Azure Storage Account connection string. This account hosts Azure Queues.
        /// </summary>
        string StorageAccountConnectionString { get; }

        /// <summary>
        /// Cosmos Db connection string for table storage data. Media Service Instance health table and job output status table use this connection string.
        /// </summary>
        string TableStorageAccountConnectionString { get; }

        /// <summary>
        /// Azure Queue name to store job verification requests.
        /// </summary>
        string JobVerificationRequestQueueName { get; }

        /// <summary>
        /// Azure Queue name to store all incoming job requests.
        /// </summary>
        string JobRequestQueueName { get; }

        /// <summary>
        /// Azure Queue name to store provision completed events.
        /// </summary>
        string ProvisioningCompletedEventQueueName { get; }

        /// <summary>
        /// Azure Front Door host name. This is used to generate URLs to stream content.
        /// </summary>
        string FrontDoorHostName { get; }

        /// <summary>
        /// Expected max number of minutes required to complete encoding job. If job stays in process longer, it is marked as "stuck" and this information is used to determine instance health.
        /// </summary>
        int NumberOfMinutesInProcessToMarkJobStuck { get; }

        /// <summary>
        /// This value is used to determine how far back to go to load job status when instance health is calculated. 
        /// </summary>
        int TimeWindowToLoadJobsInMinutes { get; }

        /// <summary>
        ///This value is used to determine how far back to go to load Azure Media Services call history when instance health is calculated. 
        /// </summary>
        int TimeWindowToLoadMediaServiceCallsInMinutes { get; }

        /// <summary>
        /// This value is used to determine when to trigger manual job output status refresh from Azure Media Service API. Sometimes EventGridEvents are missing and manual refresh is required to correctly calculate Azure Media Service instance health.
        /// </summary>
        int TimeSinceLastUpdateToForceJobResyncInMinutes { get; }

        /// <summary>
        /// Success/Total job ratio threshold to determine when Azure Media Service instance is healthy.
        /// </summary>
        float SuccessRateForHealthyState { get; }

        /// <summary>
        /// Success/Total job ratio threshold to determine when Azure Media Service instance is unhealthy.
        /// </summary>
        float SuccessRateForUnHealthyState { get; }

        /// <summary>
        /// How far in future to trigger job verification logic. This time should be longer than expected job duration.
        /// </summary>
        int TimeDurationInMinutesToVerifyJobStatus { get; }

        /// <summary>
        /// Content key policy name for clear key streaming locator configuration.
        /// </summary>
        string ContentKeyPolicyName { get; }

        /// <summary>
        /// Token issuer for clear key streaming token.
        /// </summary>
        string TokenIssuer { get; }

        /// <summary>
        /// Token audience for clear key streaming token
        /// </summary>
        string TokenAudience { get; }

        /// <summary>
        /// Dictionary to store Azure Media Service instance configuration. Key is Azure Media Service account name.
        /// </summary>
        IDictionary<string, MediaServiceConfigurationModel> MediaServiceInstanceConfiguration { get; }

        /// <summary>
        /// Dictionary to store Azure Media Service storage account connection strings. Key is Azure Media Service account name.
        /// </summary>
        IDictionary<string, string> MediaServiceInstanceStorageAccountConnectionStrings { get; }

        /// <summary>
        /// Clear key streaming binary key data.
        /// </summary>
        /// <returns></returns>
        byte[] GetClearKeyStreamingKey();

        /// <summary>
        /// Loads configuration data.
        /// </summary>
        /// <returns></returns>
        Task LoadConfigurationAsync();
    }
}
