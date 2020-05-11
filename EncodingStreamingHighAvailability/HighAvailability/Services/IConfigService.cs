namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IConfigService
    {
        string MediaServiceInstanceHealthTableName { get; }

        string JobStatusTableName { get; }

        string StreamProvisioningRequestQueueName { get; }

        string StorageAccountConnectionString { get; }

        string TableStorageAccountConnectionString { get; }

        string JobVerificationRequestQueueName { get; }

        string JobRequestQueueName { get; }

        string StreamProvisioningEventQueueName { get; }

        string FrontDoorHostName { get; }

        IDictionary<string, MediaServiceConfigurationModel> MediaServiceInstanceConfiguration { get; }

        Task LoadConfigurationAsync();
    }
}
