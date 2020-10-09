using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure;
using Azure.Storage;
using KnightBus.Core.Sagas;
using KnightBus.Core.Sagas.Exceptions;
using Newtonsoft.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.WindowsAzure.Storage.Blob;

namespace KnightBus.Azure.Storage.Sagas
{
    public class BlobSagaStore : ISagaStore
    {
        private readonly BlobContainerClient _container;
        private const string ExpirationField = "expiration";

        public BlobSagaStore(string connectionString)
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            _container = blobServiceClient.GetBlobContainerClient("knightbus-sagas");
        }

        public async Task<T> GetSaga<T>(string partitionKey, string id)
        {
            var blob = _container.GetBlobClient(Filename(partitionKey, id));

            try
            {
                var properties = await blob.GetPropertiesAsync().ConfigureAwait(false);
                var expiration = DateTimeOffset.Parse(properties.Value.Metadata[ExpirationField]);
                if (expiration < DateTime.UtcNow)
                    throw new SagaNotFoundException(partitionKey, id);

                var downloadInfo = await blob.DownloadAsync().ConfigureAwait(false);
                var serializer = new JsonSerializer();
                using (var streamReader = new StreamReader(downloadInfo.Value.Content))
                    return (T) serializer.Deserialize(streamReader, typeof(T));
            }
            catch (RequestFailedException e) when (e.Status == 404)
            {
                throw new SagaNotFoundException(partitionKey, id);
            }
        }

        public async Task<T> Create<T>(string partitionKey, string id, T sagaData, TimeSpan ttl)
        {
            var blob = _container.GetBlobClient(Filename(partitionKey, id));

            // If the saga already exists and has not expired thrown an SagaAlreadyStartedException
            try
            {
                var properties = await blob.GetPropertiesAsync().ConfigureAwait(false);
                var expiration = DateTimeOffset.Parse(properties.Value.Metadata[ExpirationField]);
                if (expiration > DateTime.UtcNow)
                    throw new SagaAlreadyStartedException(partitionKey, id);
            }
            catch (RequestFailedException e) when (e.Status != 404)
            {
                throw;
            }
            catch (RequestFailedException e) when (e.Status == 404)
            { }

            try
            {
                using (var stream = GetStream(sagaData))
                    await blob.UploadAsync(stream,
                        new BlobUploadOptions
                        {
                            HttpHeaders = new BlobHttpHeaders
                            {
                                ContentType = "application/json"
                            },
                            Metadata = new Dictionary<string, string>
                            {
                                {ExpirationField, DateTimeOffset.UtcNow.Add(ttl).ToString()}
                            },
                            Conditions = new BlobRequestConditions
                            {
                                IfNoneMatch = ETag.All
                            }
                        }).ConfigureAwait(false);
            }
            catch (RequestFailedException e) when (e.Status == 404 &&
                                                   e.ErrorCode == "ContainerNotFound")
            {
                await _container.CreateIfNotExistsAsync().ConfigureAwait(false);
                await Create(partitionKey, id, sagaData, ttl);
            }
            catch (RequestFailedException e) when (e.Status == 412)
            {
                //ETag was matched indicating the blob already exists
                throw new SagaAlreadyStartedException(partitionKey, id);
            }

            return sagaData;
        }

        public async Task Update<T>(string partitionKey, string id, T sagaData)
        {
            var blob = _container.GetBlobClient(Filename(partitionKey, id));
            try
            {
                using (var stream = GetStream(sagaData))
                {
                    var properties = await blob.GetPropertiesAsync().ConfigureAwait(false);
                    await blob.UploadAsync(stream, new BlobUploadOptions
                    {
                        Metadata = properties.Value.Metadata,
                        Conditions = new AppendBlobRequestConditions()
                    }).ConfigureAwait(false);
                }
            }
            catch (RequestFailedException e) when (e.Status  == 404)
            {
                throw new SagaNotFoundException(partitionKey, id);
            }
        }

        public async Task Complete(string partitionKey, string id)
        {
            var blob = _container.GetBlobClient(Filename(partitionKey, id));
            try
            {
                await blob.DeleteAsync().ConfigureAwait(false);
            }
            catch (RequestFailedException e) when (e.Status == 404)
            {
                throw new SagaNotFoundException(partitionKey, id);
            }
        }

        private static string Filename(string partitionKey, string id) => $"{partitionKey}/{id}.json";

        private static Stream GetStream<T>(T data)
        {
            return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data)));
        }
    }
}
