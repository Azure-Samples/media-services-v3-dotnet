namespace HighAvailability.Models
{
    using Microsoft.Azure.Management.Media.Models;

    /// <summary>
    /// Implements data model to store job request data
    /// </summary>
    public class JobRequestModel
    {
        /// <summary>
        /// Unique id
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Encoding job name
        /// </summary>
        public string JobName { get; set; }

        /// <summary>
        /// Encoding job transform name
        /// </summary>
        public string TransformName { get; set; }

        /// <summary>
        /// Job inputs to process
        /// </summary>
        public JobInputs JobInputs { get; set; }

        /// <summary>
        /// Encoding job output asset name
        /// </summary>
        public string OutputAssetName { get; set; }
    }
}
