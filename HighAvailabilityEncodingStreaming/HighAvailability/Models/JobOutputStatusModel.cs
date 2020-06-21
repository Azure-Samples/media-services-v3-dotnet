// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace HighAvailability.Models
{
    using Microsoft.Azure.Management.Media.Models;
    using System;

    /// <summary>
    /// Implements data model to store job output status data
    /// </summary>
    public class JobOutputStatusModel
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
        /// Encoding job output asset name
        /// </summary>
        public string JobOutputAssetName { get; set; }

        /// <summary>
        /// Encoding job output state
        /// </summary>
        public JobState JobOutputState { get; set; }

        /// <summary>
        /// Event DateTimeOffset
        /// </summary>
        public DateTimeOffset EventTime { get; set; }

        /// <summary>
        /// Azure Media Services instance account name
        /// </summary>
        public string MediaServiceAccountName { get; set; }

        /// <summary>
        /// Encoding job transform name
        /// </summary>
        public string TransformName { get; set; }

        /// <summary>
        /// If encoding job output failed, this indicates if it can be successfully resubmitted
        /// </summary>
        public bool HasRetriableError { get; set; }
    }
}
