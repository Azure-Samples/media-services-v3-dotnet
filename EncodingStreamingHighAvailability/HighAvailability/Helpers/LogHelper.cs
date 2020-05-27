﻿namespace HighAvailability.Helpers
{
    using Newtonsoft.Json;

    /// <summary>
    /// Implements helper methods for logging
    /// </summary>
    public static class LogHelper
    {
        /// <summary>
        /// Format any object for log output
        /// </summary>
        /// <typeparam name="T">Object type</typeparam>
        /// <param name="objectToFormat">Object to format</param>
        /// <returns>Formatted string</returns>
        public static string FormatObjectForLog<T>(T objectToFormat)
        {
            return JsonConvert.SerializeObject(objectToFormat);
        }
    }
}
