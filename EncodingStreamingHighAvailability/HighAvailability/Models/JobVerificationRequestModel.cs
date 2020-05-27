namespace HighAvailability.Models
{
    /// <summary>
    /// Implements data model to store job verification request data
    /// </summary>
    public class JobVerificationRequestModel
    {
        /// <summary>
        /// Unique id
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Encoding job id
        /// </summary>
        public string JobId { get; set; }

        /// <summary>
        /// Azure Media Services instance account name
        /// </summary>
        public string MediaServiceAccountName { get; set; }

        /// <summary>
        /// Original JobRequestModel object, it is used to resubmit failed jobs
        /// </summary>
        public JobRequestModel OriginalJobRequestModel { get; set; }

        /// <summary>
        /// Encoding job output asset name
        /// </summary>
        public string JobOutputAssetName { get; set; }

        /// <summary>
        /// Encoding job name
        /// </summary>
        public string JobName { get; set; }

        /// <summary>
        /// Retry count for encoding job request
        /// </summary>
        public int RetryCount { get; set; }
    }
}
