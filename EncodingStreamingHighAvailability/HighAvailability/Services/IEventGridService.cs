namespace HighAvailability.Services
{
    using HighAvailability.Models;
    using Microsoft.Azure.EventGrid.Models;
    using Microsoft.Extensions.Logging;

    public interface IEventGridService
    {
        JobOutputStatusModel ParseEventData(EventGridEvent eventGridEvent, ILogger logger);
    }
}
