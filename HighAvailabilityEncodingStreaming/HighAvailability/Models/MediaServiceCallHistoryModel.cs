namespace HighAvailability.Models
{
    using System;
    using System.Net;

    /// <summary>
    /// Implements data model to store Media Service call history data
    /// </summary>
    public class MediaServiceCallHistoryModel
    {
        /// <summary>
        /// Unique id
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Call status
        /// </summary>
        public HttpStatusCode HttpStatus { get; set; }

        /// <summary>
        /// Azure Media Services instance account name
        /// </summary>
        public string MediaServiceAccountName { get; set; }

        /// <summary>
        /// Event time 
        /// </summary>
        public DateTimeOffset EventTime { get; set; }

        /// <summary>
        /// Azure Media Service call info
        /// </summary>
        public string CallInfo { get; set; }
    }
}
