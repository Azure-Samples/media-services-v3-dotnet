namespace HighAvailability.Models
{
    using Newtonsoft.Json;
    using System;
    using System.Net;

    /// <summary>
    /// Implements data model to store Media Service call history data
    /// </summary>
    public class MediaServiceCallHistoryModel
    {
        /// <summary>
        /// Json settings to deserialize data using full type names. 
        /// </summary>
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

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
        /// Azure Media Service call name
        /// </summary>
        public string CallName { get; set; }

        /// <summary>
        /// Context data associated with call
        /// </summary>
        public string ContextData { get; set; }

        /// <summary>
        /// Formats to json and sets context data 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="contextData"></param>
        public void SetContextData<T>(T contextData)
        {
            this.ContextData = JsonConvert.SerializeObject(contextData, jsonSettings);
        }
    }
}
