// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace HighAvailability.AzureStorage.Models
{
    using HighAvailability.Models;
    using Microsoft.Azure.Cosmos.Table;
    using System;
    using System.Net;

    /// <summary>
    /// Implements table storage specific model for MediaServiceCallHistoryModel class
    /// </summary>
    public class MediaServiceCallHistoryModelTableEntity : TableEntity
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public MediaServiceCallHistoryModelTableEntity() : this(new MediaServiceCallHistoryModel())
        {
        }

        /// <summary>
        /// Constructor to create object from MediaServiceCallHistoryModel object
        /// </summary>
        /// <param name="mediaServiceCallHistoryModel">source object</param>
        public MediaServiceCallHistoryModelTableEntity(MediaServiceCallHistoryModel mediaServiceCallHistoryModel)
        {
            this.PartitionKey = mediaServiceCallHistoryModel.MediaServiceAccountName;
            this.RowKey = mediaServiceCallHistoryModel.Id;
            this.MediaServiceCallHistoryModelId = mediaServiceCallHistoryModel.Id;
            this.HttpStatus = (int)mediaServiceCallHistoryModel.HttpStatus;
            this.MediaServiceAccountName = mediaServiceCallHistoryModel.MediaServiceAccountName;
            this.EventTime = mediaServiceCallHistoryModel.EventTime;
            this.CallInfo = mediaServiceCallHistoryModel.CallInfo;
        }

        /// <summary>
        /// Unique Id
        /// </summary>
        public string MediaServiceCallHistoryModelId { get; set; }

        /// <summary>
        /// Job request submission status
        /// </summary>
        public int HttpStatus { get; set; }

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

        /// <summary>
        /// Creates MediaServiceCallHistoryModel object
        /// </summary>
        /// <returns>JobOutputStatusModel object</returns>
        public MediaServiceCallHistoryModel GetMediaServiceCallHistoryModel()
        {
            return new MediaServiceCallHistoryModel
            {
                Id = this.MediaServiceCallHistoryModelId,
                HttpStatus = (HttpStatusCode)this.HttpStatus,
                MediaServiceAccountName = this.MediaServiceAccountName,
                EventTime = this.EventTime,
                CallInfo = this.CallInfo
            };
        }
    }
}
