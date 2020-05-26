﻿namespace HighAvailability.Interfaces
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
        /// Parses data from EventGridEvent and creates JobOutputStatusModel.
        /// </summary>
        /// <param name="eventGridEvent">Data to parse</param>
        /// <param name="logger">Logger to log data</param>
        /// <returns></returns>
        JobOutputStatusModel ParseEventData(EventGridEvent eventGridEvent, ILogger logger);
    }
}
