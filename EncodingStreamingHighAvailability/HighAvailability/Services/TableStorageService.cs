namespace HighAvailability.Services
{
    using Microsoft.Azure.Cosmos.Table;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class TableStorageService
    {
        private readonly CloudTable table;
        private const int takeCount = 100;

        public TableStorageService(CloudTable table)
        {
            this.table = table ?? throw new ArgumentNullException(nameof(table));
        }

        public async Task<T> CreateOrUpdateAsync<T>(T tableEntityModel) where T : TableEntity, new()
        {
            if (tableEntityModel == null)
            {
                throw new ArgumentNullException(nameof(tableEntityModel));
            }

            var insertOrMergeOperation = TableOperation.InsertOrMerge(tableEntityModel);

            var result = await this.table.ExecuteAsync(insertOrMergeOperation).ConfigureAwait(false);
            var tableEntityModelResult = result.Result as T;

            if (tableEntityModelResult == null)
            {
                throw new Exception("Got error callig Table API");
            }

            return tableEntityModelResult;
        }

        public async Task<T> GetAsync<T>(string partitionKey, string rowKey) where T : TableEntity, new()
        {
            var retrieveOperation = TableOperation.Retrieve<T>(partitionKey, rowKey);
            var result = await this.table.ExecuteAsync(retrieveOperation).ConfigureAwait(false);
            var tableEntityModel = result.Result as T;

            if (tableEntityModel == null)
            {
                throw new Exception("Got error callig Table API");
            }

            return tableEntityModel;
        }

        public async Task<T> MergeAsync<T>(T tableEntityModel) where T : TableEntity, new()
        {
            var mergeOperation = TableOperation.Merge(tableEntityModel);
            var result = await this.table.ExecuteAsync(mergeOperation).ConfigureAwait(false);
            var tableEntityModelResult = result.Result as T;

            if (tableEntityModelResult == null)
            {
                throw new Exception("Got error callig Table API");
            }

            return tableEntityModelResult;
        }

        public async Task<IEnumerable<T>> ListAsync<T>() where T : TableEntity, new()
        {
            return await this.QueryDataAsync(new TableQuery<T>()).ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> QueryDataAsync<T>(TableQuery<T> rangeQuery) where T : TableEntity, new()
        {
            if (rangeQuery == null)
            {
                throw new ArgumentNullException(nameof(rangeQuery));
            }

            rangeQuery.TakeCount = takeCount;
            var results = new List<T>();
            TableContinuationToken? token = null;
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
    }
}
