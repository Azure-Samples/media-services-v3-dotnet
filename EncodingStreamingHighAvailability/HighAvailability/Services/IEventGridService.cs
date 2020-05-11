namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using Microsoft.Azure.EventGrid.Models;

    public interface IEventGridService
    {
        JobStatusModel? ParseEventData(EventGridEvent eventGridEvent);
    }
}
