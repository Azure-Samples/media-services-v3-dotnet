namespace media_services_high_availability_shared.Services
{
    using media_services_high_availability_shared.Models;
    using Microsoft.Azure.EventGrid.Models;

    public interface IEventGridService
    {
        JobStatusModel? ParseEventData(EventGridEvent eventGridEvent);
    }
}
