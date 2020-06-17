// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace HighAvailability.AzureStorage.Services
{
    using Microsoft.Azure.Cosmos.Table;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Generic interface to define methods to store and load data using Azure Table Storage
    /// </summary>
    public interface ITableStorageService
    {
        /// <summary>
        /// Creates a new record.
        /// </summary>
        /// <typeparam name="T">Record type, derived from TableEntity</typeparam>
        /// <param name="tableEntityModel">Record to store</param>
        /// <returns>Returns stored record</returns>
        Task<T> CreateOrUpdateAsync<T>(T tableEntityModel) where T : TableEntity, new();

        /// <summary>
        /// Gets record from storage uniquely identified by partition key and row key.
        /// </summary>
        /// <typeparam name="T">Record type, derived from TableEntity</typeparam>
        /// <param name="partitionKey">Partition key in storage</param>
        /// <param name="rowKey">Row key in storage</param>
        /// <returns>Returns retrieved record</returns>
        Task<T> GetAsync<T>(string partitionKey, string rowKey) where T : TableEntity, new();

        /// <summary>
        /// Lists all available records from storage.
        /// </summary>
        /// <typeparam name="T">Record type, derived from TableEntity</typeparam>
        /// <returns>List of all records in storage</returns>
        Task<IEnumerable<T>> ListAsync<T>() where T : TableEntity, new();

        /// <summary>
        /// Merges record with existing record in storage.
        /// </summary>
        /// <typeparam name="T">Record type, derived from TableEntity</typeparam>
        /// <param name="tableEntityModel">Record to merge</param>
        /// <returns>Merged record</returns>
        Task<T> MergeAsync<T>(T tableEntityModel) where T : TableEntity, new();

        /// <summary>
        /// Queries storage for records that match provided criteria.
        /// </summary>
        /// <typeparam name="T">Record type, derived from TableEntity</typeparam>
        /// <param name="rangeQuery">query to run</param>
        /// <returns>List of records that match criteria</returns>
        Task<IEnumerable<T>> QueryDataAsync<T>(TableQuery<T> rangeQuery) where T : TableEntity, new();

        /// <summary>
        /// Deletes specific record.
        /// </summary>
        /// <typeparam name="T">Record type, derived from TableEntity</typeparam>
        /// <param name="tableEntityModel">Record to delete</param>
        /// <returns>Async operation task</returns>
        Task DeleteAsync<T>(T tableEntityModel) where T : TableEntity, new();

        /// <summary>
        /// Deletes all records from storage
        /// </summary>
        /// <typeparam name="T">Record type, derived from TableEntity</typeparam>
        /// <returns>Async operation task</returns>
        Task DeleteAllAsync<T>() where T : TableEntity, new();
    }
}