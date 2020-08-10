using System;
using System.Threading.Tasks;
using KnightBus.Core.Sagas;
using KnightBus.Core.Sagas.Exceptions;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace KnightBus.Azure.Storage.Sagas
{
    public class StorageTableSagaStore : ISagaStore
    {
        private readonly CloudTable _table;
        private const string TableName = "sagas";

        public StorageTableSagaStore(string connectionString)
        {
            var storageAccount = CloudStorageAccount.Parse(connectionString);
            var tableClient = storageAccount.CreateCloudTableClient();
            _table = tableClient.GetTableReference(TableName);
        }

        private async Task<TableResult> GetSagaTableRow(string partitionKey, string id)
        {
            var operation = TableOperation.Retrieve<SagaTableData>(partitionKey, id);

            try
            {
                var result = await _table.ExecuteAsync(operation).ConfigureAwait(false);
                return result;
            }
            catch (StorageException e) when (e.RequestInformation.HttpStatusCode == 404)
            {
                throw new SagaNotFoundException(partitionKey, id);
            }
        }

        public async Task<T> GetSaga<T>(string partitionKey, string id)
        {
            var result = await GetSagaTableRow(partitionKey, id).ConfigureAwait(false);
            var saga = (SagaTableData)result.Result;
            if (saga == null) throw new SagaNotFoundException(partitionKey, id);
            if (saga.Expiration < DateTime.UtcNow) throw new SagaNotFoundException(partitionKey, id);
            return JsonConvert.DeserializeObject<T>(saga.Json);
        }


        public async Task<T> Create<T>(string partitionKey, string id, T sagaData, TimeSpan ttl)
        {
            var operation = TableOperation.Insert(new SagaTableData
            {
                PartitionKey = partitionKey,
                RowKey = id,
                Expiration = DateTime.UtcNow.Add(ttl),
                Json = JsonConvert.SerializeObject(sagaData)
            });

            try
            {
                var result = await OptimisticExecuteAsync(() => _table.ExecuteAsync(operation)).ConfigureAwait(false);
                return JsonConvert.DeserializeObject<T>(((SagaTableData)result.Result).Json);
            }
            catch (StorageException e) when (e.RequestInformation.HttpStatusCode == 409)
            {
                try
                {
                    //Determine if Saga has expired
                    var existingRow = await GetSagaTableRow(partitionKey, id).ConfigureAwait(false);
                    var existingSaga = (SagaTableData)existingRow.Result;
                    if (existingSaga.Expiration < DateTime.UtcNow)
                    {
                        var deleteOperation = TableOperation.Delete(new SagaTableData { PartitionKey = partitionKey, RowKey = id, ETag = existingRow.Etag });
                        await _table.ExecuteAsync(deleteOperation).ConfigureAwait(false);
                        return await Create(partitionKey, id, sagaData, ttl).ConfigureAwait(false);
                    }
                }
                catch (SagaNotFoundException)
                {
                    return await Create(partitionKey, id, sagaData, ttl).ConfigureAwait(false);
                }
                catch (StorageException deleteException) when (deleteException.RequestInformation.HttpStatusCode == 404)
                {
                    return await Create(partitionKey, id, sagaData, ttl).ConfigureAwait(false);
                }

                throw new SagaAlreadyStartedException(partitionKey, id);
            }
        }

        public async Task Update<T>(string partitionKey, string id, T sagaData)
        {
            var operation = TableOperation.Replace(new SagaTableData
            {
                PartitionKey = partitionKey,
                RowKey = id,
                ETag = "*",
                Json = JsonConvert.SerializeObject(sagaData)
            });

            try
            {
                await _table.ExecuteAsync(operation).ConfigureAwait(false);
            }
            catch (StorageException e) when (e.RequestInformation.HttpStatusCode == 404)
            {
                throw new SagaNotFoundException(partitionKey, id);
            }
        }

        public async Task Complete(string partitionKey, string id)
        {
            var operation = TableOperation.Delete(new SagaTableData { PartitionKey = partitionKey, RowKey = id, ETag = "*" });
            await _table.ExecuteAsync(operation).ConfigureAwait(false);
        }

        private async Task<TableResult> OptimisticExecuteAsync(Func<Task<TableResult>> func)
        {
            try
            {
                return await func.Invoke().ConfigureAwait(false);
            }
            catch (StorageException e) when (e.RequestInformation.HttpStatusCode == 404)
            {
                await _table.CreateIfNotExistsAsync().ConfigureAwait(false);
                return await func.Invoke().ConfigureAwait(false);
            }
        }
    }
}
