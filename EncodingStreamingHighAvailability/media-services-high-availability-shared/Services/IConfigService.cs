namespace media_services_high_availability_shared.Services
{
    using media_services_high_availability_shared.Models;
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
