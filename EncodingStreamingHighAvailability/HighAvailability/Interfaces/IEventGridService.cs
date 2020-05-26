namespace HighAvailability.Interfaces
{
    using HighAvailability.Models;
    using Microsoft.Azure.EventGrid.Models;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Interface to define methods to work with EventGrid data
    /// </summary>
    public interface IEventGridService
    {
        /// <summary>
        /// This method parses data from EventGridEvent and creates JobOutputStatusModel.
        /// </summary>
        /// <param name="eventGridEvent">Data to parse</param>
        /// <param name="logger">logger to log data</param>
        /// <returns></returns>
        JobOutputStatusModel ParseEventData(EventGridEvent eventGridEvent, ILogger logger);
    }
}
