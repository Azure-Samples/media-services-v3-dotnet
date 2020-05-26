namespace HighAvailability.AzureStorage.Services
{
    using Microsoft.Azure.Cosmos.Table;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class TableStorageService : ITableStorageService
    {
        private readonly CloudTable table;
        private const int takeCount = 10000;

        public TableStorageService(CloudTable table)
        {
            this.table = table ?? throw new ArgumentNullException(nameof(table));
        }

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

        public async Task DeleteAsync<T>(T tableEntityModel) where T : TableEntity, new()
        {
            var deleteOperation = TableOperation.Delete(tableEntityModel);
            var result = await this.table.ExecuteAsync(deleteOperation).ConfigureAwait(false);

            if (!this.IsStatusCodeSuccess(result.HttpStatusCode))
            {
                throw new Exception("Got error callig Table API");
            }
        }

        public async Task DeleteAllAsync<T>() where T : TableEntity, new()
        {
            var allItems = await this.ListAsync<T>().ConfigureAwait(false);
            foreach (var item in allItems)
            {
                await this.DeleteAsync(item).ConfigureAwait(false);
            }
        }

        private bool IsStatusCodeSuccess(int httpStatusCode)
        {
            return httpStatusCode >= 200 && httpStatusCode < 300;
        }
    }
}
