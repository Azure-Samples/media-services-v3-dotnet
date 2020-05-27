namespace HighAvailability.AzureStorage.Services
{
    using Microsoft.Azure.Cosmos.Table;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Implements methods to store and load data using Azure Table Storage
    /// </summary>
    public class TableStorageService : ITableStorageService
    {
        /// <summary>
        /// Azure Table client
        /// </summary>
        private readonly CloudTable table;

        /// <summary>
        /// Max number of items to load in single call
        /// </summary>
        private const int takeCount = 10000;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="table">Azure Table client</param>
        public TableStorageService(CloudTable table)
        {
            this.table = table ?? throw new ArgumentNullException(nameof(table));
        }

        /// <summary>
        /// Creates a new record.
        /// </summary>
        /// <typeparam name="T">Record type, derived from TableEntity</typeparam>
        /// <param name="tableEntityModel">Record to store</param>
        /// <returns>Returns stored record</returns>
        public async Task<T> CreateOrUpdateAsync<T>(T tableEntityModel) where T : TableEntity, new()
        {
            var insertOrMergeOperation = TableOperation.InsertOrMerge(tableEntityModel);

            var result = await this.table.ExecuteAsync(insertOrMergeOperation).ConfigureAwait(false);
            var tableEntityModelResult = result.Result as T;

            if (tableEntityModelResult == null || !this.IsStatusCodeSuccess(result.HttpStatusCode))
            {
                throw new Exception("Got error callig Table API");
            }

            return tableEntityModelResult;
        }

        /// <summary>
        /// Gets record from storage uniquely identified by partition key and row key.
        /// </summary>
        /// <typeparam name="T">Record type, derived from TableEntity</typeparam>
        /// <param name="partitionKey">Partition key in storage</param>
        /// <param name="rowKey">Row key in storage</param>
        /// <returns>Returns retrieved record</returns>
        public async Task<T> GetAsync<T>(string partitionKey, string rowKey) where T : TableEntity, new()
        {
            var retrieveOperation = TableOperation.Retrieve<T>(partitionKey, rowKey);
            var result = await this.table.ExecuteAsync(retrieveOperation).ConfigureAwait(false);
            var tableEntityModel = result.Result as T;

            if (tableEntityModel == null || !this.IsStatusCodeSuccess(result.HttpStatusCode))
            {
                throw new Exception("Got error callig Table API");
            }

            return tableEntityModel;
        }

        /// <summary>
        /// Merges record with existing record in storage.
        /// </summary>
        /// <typeparam name="T">Record type, derived from TableEntity</typeparam>
        /// <param name="tableEntityModel">Record to merge</param>
        /// <returns>Merged record</returns>
        public async Task<T> MergeAsync<T>(T tableEntityModel) where T : TableEntity, new()
        {
            var mergeOperation = TableOperation.Merge(tableEntityModel);
            var result = await this.table.ExecuteAsync(mergeOperation).ConfigureAwait(false);
            var tableEntityModelResult = result.Result as T;

            if (tableEntityModelResult == null || !this.IsStatusCodeSuccess(result.HttpStatusCode))
            {
                throw new Exception("Got error callig Table API");
            }

            return tableEntityModelResult;
        }

        /// <summary>
        /// Lists all available records from storage.
        /// </summary>
        /// <typeparam name="T">Record type, derived from TableEntity</typeparam>
        /// <returns>List of all records in storage</returns>
        public async Task<IEnumerable<T>> ListAsync<T>() where T : TableEntity, new()
        {
            return await this.QueryDataAsync(new TableQuery<T>()).ConfigureAwait(false);
        }

        /// <summary>
        /// Queries storage for records that match provided criteria.
        /// </summary>
        /// <typeparam name="T">Record type, derived from TableEntity</typeparam>
        /// <param name="rangeQuery">query to run</param>
        /// <returns>List of records that match criteria</returns>
        public async Task<IEnumerable<T>> QueryDataAsync<T>(TableQuery<T> rangeQuery) where T : TableEntity, new()
        {
            if (rangeQuery == null)
            {
                throw new ArgumentNullException(nameof(rangeQuery));
            }

            // specify max number of items to pull in single call
            rangeQuery.TakeCount = takeCount;
            var results = new List<T>();
            TableContinuationToken token = null;
            do
            {
                // Execute the query, passing in the continuation token.
                // The first time this method is called, the continuation token is null. If there are more results, the call
                // populates the continuation token for use in the next call.
                var segment = await this.table.ExecuteQuerySegmentedAsync(rangeQuery, token).ConfigureAwait(false);

                // Save the continuation token for the next call to ExecuteQuerySegmentedAsync
                token = segment.ContinuationToken;

                results.AddRange(segment.Results);
            }
            while (token != null);

            return results;
        }

        /// <summary>
        /// Deletes specific record.
        /// </summary>
        /// <typeparam name="T">Record type, derived from TableEntity</typeparam>
        /// <param name="tableEntityModel">Record to delete</param>
        /// <returns>Async operation task</returns>
        public async Task DeleteAsync<T>(T tableEntityModel) where T : TableEntity, new()
        {
            var deleteOperation = TableOperation.Delete(tableEntityModel);
            var result = await this.table.ExecuteAsync(deleteOperation).ConfigureAwait(false);

            if (!this.IsStatusCodeSuccess(result.HttpStatusCode))
            {
                throw new Exception("Got error callig Table API");
            }
        }

        /// <summary>
        /// Deletes all records from storage
        /// </summary>
        /// <typeparam name="T">Record type, derived from TableEntity</typeparam>
        /// <returns>Async operation task</returns>
        public async Task DeleteAllAsync<T>() where T : TableEntity, new()
        {
            var allItems = await this.ListAsync<T>().ConfigureAwait(false);
            foreach (var item in allItems)
            {
                await this.DeleteAsync(item).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Checks if status code is success
        /// </summary>
        /// <param name="httpStatusCode">Status code to check</param>
        /// <returns>True if code is success, false otherwise</returns>
        private bool IsStatusCodeSuccess(int httpStatusCode)
        {
            return httpStatusCode >= 200 && httpStatusCode < 300;
        }
    }
}
