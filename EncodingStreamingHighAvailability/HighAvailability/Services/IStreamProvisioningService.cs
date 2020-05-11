namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using System.Threading.Tasks;

    /// <summary>
    /// This will implement streaming locators provisioning logic. We may need to have several different implementations, maybe one for no encryption scenario, another for encrypted scenario. Not sure if that is worth separating that way.
    /// We may also have different logic to determine behavior if one region is unhealthy. We may want to provision only to healthy instance and come back to unhealthy
    /// </summary>
    public interface IStreamProvisioningService
    {
        // Provision streaming endppoint for a given encoded asset. This operation should be idempotent, we may need to rerun this if one of the instances is not healthy. 
        Task ProvisionStreamAsync(StreamProvisioningRequestModel streamProvisioningRequest);
    }
}
