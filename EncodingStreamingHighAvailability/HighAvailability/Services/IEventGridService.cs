namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using Microsoft.Azure.EventGrid.Models;
    using Microsoft.Extensions.Logging;

    public interface IEventGridService
    {
        JobStatusModel ParseEventData(EventGridEvent eventGridEvent, ILogger logger);
    }
}
