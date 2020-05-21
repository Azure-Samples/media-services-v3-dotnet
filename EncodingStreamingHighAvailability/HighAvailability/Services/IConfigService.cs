namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;

    public interface IConfigService
    {
        string MediaServiceInstanceHealthTableName { get; }

        string JobOutputStatusTableName { get; }

        string StreamProvisioningRequestQueueName { get; }

        string StorageAccountConnectionString { get; }

        string TableStorageAccountConnectionString { get; }

        string JobVerificationRequestQueueName { get; }

        string JobRequestQueueName { get; }

        string StreamProvisioningEventQueueName { get; }

        string FrontDoorHostName { get; }

        int NumberOfMinutesInProcessToMarkJobStuck { get; }

        int TimeWindowToLoadJobsInMinutes { get; }

        int TimeSinceLastUpdateToForceJobResyncInMinutes { get; }

        float SuccessRateForHealthyState { get; }

        float SuccessRateForUnHealthyState { get; }

        int TimeDurationInMinutesToVerifyJobStatus { get; }

        string ContentKeyPolicyName { get; }

        string TokenIssuer { get; }

        string TokenAudience { get; }

        IDictionary<string, MediaServiceConfigurationModel> MediaServiceInstanceConfiguration { get; }

        IDictionary<string, string> MediaServiceInstanceStorageAccountConnectionStrings { get; }

        byte[] GetClearKeyStreamingKey();

        Task LoadConfigurationAsync();
    }
}
