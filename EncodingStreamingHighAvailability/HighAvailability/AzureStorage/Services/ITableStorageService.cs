namespace HighAvailability.AzureStorage.Services
{
    using Microsoft.Azure.Cosmos.Table;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface ITableStorageService
    {
        Task<T> CreateOrUpdateAsync<T>(T tableEntityModel) where T : TableEntity, new();
        Task<T> GetAsync<T>(string partitionKey, string rowKey) where T : TableEntity, new();
        Task<IEnumerable<T>> ListAsync<T>() where T : TableEntity, new();
        Task<T> MergeAsync<T>(T tableEntityModel) where T : TableEntity, new();
        Task<IEnumerable<T>> QueryDataAsync<T>(TableQuery<T> rangeQuery) where T : TableEntity, new();
        Task DeleteAsync<T>(T tableEntityModel) where T : TableEntity, new();
        Task DeleteAllAsync<T>() where T : TableEntity, new();
    }
}