// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace HighAvailability.AzureStorage.Models
{
    using HighAvailability.Models;
    using Microsoft.Azure.Cosmos.Table;
    using System;

    /// <summary>
    /// Implements table storage specific model for JobOutputStatusModel class.  
    /// </summary>
    public class JobOutputStatusModelTableEntity : TableEntity
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public JobOutputStatusModelTableEntity() : this(new JobOutputStatusModel())
        {
        }

        /// <summary>
        /// Constructor to create object from JobOutputStatusModel object
        /// </summary>
        /// <param name="jobOutputStatusModel">source object</param>
        public JobOutputStatusModelTableEntity(JobOutputStatusModel jobOutputStatusModel)
        {
            this.PartitionKey = jobOutputStatusModel.JobName;
            this.RowKey = jobOutputStatusModel.Id;
            this.JobOutputStatusId = jobOutputStatusModel.Id;
            this.JobName = jobOutputStatusModel.JobName;
            this.JobOutputAssetName = jobOutputStatusModel.JobOutputAssetName;
            this.JobOutputState = jobOutputStatusModel.JobOutputState.ToString();
            this.EventTime = jobOutputStatusModel.EventTime;
            this.MediaServiceAccountName = jobOutputStatusModel.MediaServiceAccountName;
            this.TransformName = jobOutputStatusModel.TransformName;
            this.IsSystemError = jobOutputStatusModel.IsSystemError;
        }

        /// <summary>
        /// Unique Id
        /// </summary>
        public string JobOutputStatusId { get; set; }

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
        public string JobOutputState { get; set; }

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
        public bool IsSystemError { get; set; }

        /// <summary>
        /// Creates JobOutputStatusModel object
        /// </summary>
        /// <returns>JobOutputStatusModel object</returns>
        public JobOutputStatusModel GetJobOutputStatusModel()
        {
            return new JobOutputStatusModel
            {
                Id = this.JobOutputStatusId,
                JobName = this.JobName,
                JobOutputAssetName = this.JobOutputAssetName,
                JobOutputState = this.JobOutputState,
                EventTime = this.EventTime,
                MediaServiceAccountName = this.MediaServiceAccountName,
                TransformName = this.TransformName,
                IsSystemError = this.IsSystemError
            };
        }
    }
}
