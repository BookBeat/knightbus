using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using KnightBus.Core.Sagas;
using KnightBus.Core.Sagas.Exceptions;

namespace KnightBus.Azure.Storage.Sagas;

public class BlobSagaStore : ISagaStore
{
    private readonly BlobContainerClient _container;
    private const string ExpirationField = "expiration";

    public BlobSagaStore(string connectionString)
        : this(new StorageBusConfiguration(connectionString)) { }

    public BlobSagaStore(IStorageBusConfiguration configuration)
    {
        _container = AzureStorageClientFactory.CreateBlobContainerClient(
            configuration,
            "knightbus-sagas"
        );
    }

    public async Task<SagaData<T>> GetSaga<T>(string partitionKey, string id, CancellationToken ct)
    {
        var blob = _container.GetBlobClient(Filename(partitionKey, id));

        try
        {
            var properties = await blob.GetPropertiesAsync(cancellationToken: ct)
                .ConfigureAwait(false);
            var expiration = DateTimeOffset.Parse(properties.Value.Metadata[ExpirationField]);
            if (expiration < DateTime.UtcNow)
                throw new SagaNotFoundException(partitionKey, id);

            var downloadInfo = await blob.DownloadAsync(ct).ConfigureAwait(false);
            T data = await JsonSerializer
                .DeserializeAsync<T>(downloadInfo.Value.Content, cancellationToken: ct)
                .ConfigureAwait(false);
            return new SagaData<T>
            {
                Data = data,
                ConcurrencyStamp = downloadInfo.Value.Details.ETag.ToString(),
            };
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            throw new SagaNotFoundException(partitionKey, id);
        }
    }

    public async Task<SagaData<T>> Create<T>(
        string partitionKey,
        string id,
        T data,
        TimeSpan ttl,
        CancellationToken ct
    )
    {
        var blob = _container.GetBlobClient(Filename(partitionKey, id));
        var requestConditions = new BlobRequestConditions { IfNoneMatch = ETag.All };
        // If the saga already exists and has not expired thrown an SagaAlreadyStartedException
        try
        {
            var properties = await blob.GetPropertiesAsync(cancellationToken: ct)
                .ConfigureAwait(false);
            var expiration = DateTimeOffset.Parse(properties.Value.Metadata[ExpirationField]);
            if (expiration > DateTime.UtcNow)
                throw new SagaAlreadyStartedException(partitionKey, id);

            //Blob already exists, so we set that it is this specific blob we want to replace
            requestConditions = new BlobRequestConditions { IfMatch = properties.Value.ETag };
        }
        catch (RequestFailedException e) when (e.Status != 404)
        {
            throw;
        }
        catch (RequestFailedException e) when (e.Status == 404) { }

        SagaData<T> sagaData = new SagaData<T> { Data = data };
        try
        {
            await using var stream = GetStream(data);
            var blobInfo = await blob.UploadAsync(
                    stream,
                    new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" },
                        Metadata = new Dictionary<string, string>
                        {
                            {
                                ExpirationField,
                                DateTimeOffset
                                    .UtcNow.Add(ttl)
                                    .ToString(CultureInfo.InvariantCulture)
                            },
                        },
                        Conditions = requestConditions,
                    },
                    ct
                )
                .ConfigureAwait(false);
            sagaData.ConcurrencyStamp = blobInfo.Value.ETag.ToString();
        }
        catch (RequestFailedException e)
            when (e.Status == 404 && e.ErrorCode == "ContainerNotFound")
        {
            await _container.CreateIfNotExistsAsync(cancellationToken: ct).ConfigureAwait(false);
            await Create(partitionKey, id, data, ttl, ct);
        }
        catch (RequestFailedException e) when (e.Status == 412 || e.Status == 409)
        {
            //Request conditions failed indicating either that the blob already exists or that the blob we're trying to replace has another etag
            throw new SagaAlreadyStartedException(partitionKey, id);
        }

        return sagaData;
    }

    public async Task Update<T>(
        string partitionKey,
        string id,
        SagaData<T> sagaData,
        CancellationToken ct
    )
    {
        var blob = _container.GetBlobClient(Filename(partitionKey, id));
        try
        {
            await using (var stream = GetStream(sagaData.Data))
            {
                var properties = await blob.GetPropertiesAsync(cancellationToken: ct)
                    .ConfigureAwait(false);
                var response = await blob.UploadAsync(
                        stream,
                        new BlobUploadOptions
                        {
                            Metadata = properties.Value.Metadata,
                            Conditions = new AppendBlobRequestConditions
                            {
                                IfMatch = new ETag(sagaData.ConcurrencyStamp),
                            },
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                sagaData.ConcurrencyStamp = response.Value.ETag.ToString();
            }
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            throw new SagaNotFoundException(partitionKey, id);
        }
        catch (RequestFailedException e) when (e.Status == 412)
        {
            throw new SagaDataConflictException(partitionKey, id);
        }
    }

    public async Task Complete<T>(
        string partitionKey,
        string id,
        SagaData<T> sagaData,
        CancellationToken ct
    )
    {
        var blob = _container.GetBlobClient(Filename(partitionKey, id));
        try
        {
            var conditions = new AppendBlobRequestConditions
            {
                IfMatch = new ETag(sagaData.ConcurrencyStamp),
            };
            await blob.DeleteAsync(conditions: conditions, cancellationToken: ct)
                .ConfigureAwait(false);
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            throw new SagaNotFoundException(partitionKey, id);
        }
        catch (RequestFailedException e) when (e.Status == 412)
        {
            throw new SagaDataConflictException(partitionKey, id);
        }
    }

    public async Task Delete(string partitionKey, string id, CancellationToken ct)
    {
        var blob = _container.GetBlobClient(Filename(partitionKey, id));
        try
        {
            await blob.DeleteAsync(cancellationToken: ct).ConfigureAwait(false);
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            throw new SagaNotFoundException(partitionKey, id);
        }
    }

    private static string Filename(string partitionKey, string id) => $"{partitionKey}/{id}.json";

    private static Stream GetStream<T>(T data)
    {
        return new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(data));
    }
}
