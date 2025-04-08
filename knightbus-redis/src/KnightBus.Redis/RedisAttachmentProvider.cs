using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis;

public class RedisAttachmentProvider : IMessageAttachmentProvider
{
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly IRedisConfiguration _configuration;

    internal const string ContentType = "contenttype";
    internal const string FileName = "filename";
    private static readonly HashSet<string> Keys = [FileName, ContentType];

    public RedisAttachmentProvider(IConnectionMultiplexer multiplexer, IRedisConfiguration configuration)
    {
        _multiplexer = multiplexer;
        _configuration = configuration;
    }
    public async Task<IMessageAttachment> GetAttachmentAsync(string queueName, string id, CancellationToken cancellationToken = default(CancellationToken))
    {
        var db = _multiplexer.GetDatabase(_configuration.DatabaseId);

        var metadataHash = await db.HashGetAllAsync(RedisQueueConventions.GetAttachmentMetadataKey(queueName, id)).ConfigureAwait(false);
        var data = await db.StringGetAsync(RedisQueueConventions.GetAttachmentBinaryKey(queueName, id)).ConfigureAwait(false);
        var metadata = metadataHash.ToStringDictionary();
        return new MessageAttachment(metadata[FileName], metadata[ContentType], new MemoryStream(data), metadata);
    }

    public async Task UploadAttachmentAsync(string queueName, string id, IMessageAttachment attachment, CancellationToken cancellationToken = default(CancellationToken))
    {
        var db = _multiplexer.GetDatabase(_configuration.DatabaseId);

        var hash = new List<HashEntry>
        {
            new HashEntry(FileName, attachment.Filename),
            new HashEntry(ContentType, attachment.ContentType),
        };

        foreach (var metadata in attachment.Metadata.Where(x => !Keys.Contains(x.Key)))
        {
            hash.Add(new HashEntry(metadata.Key, metadata.Value));
        }

        using (var memoryStream = new MemoryStream())
        {
            await attachment.Stream.CopyToAsync(memoryStream).ConfigureAwait(false);
            await Task.WhenAll(
                    db.HashSetAsync(RedisQueueConventions.GetAttachmentMetadataKey(queueName, id), hash.ToArray()),
                    db.StringSetAsync(RedisQueueConventions.GetAttachmentBinaryKey(queueName, id), memoryStream.ToArray()))
                .ConfigureAwait(false);
        }
    }

    public async Task<bool> DeleteAttachmentAsync(string queueName, string id, CancellationToken cancellationToken = default(CancellationToken))
    {
        var db = _multiplexer.GetDatabase(_configuration.DatabaseId);

        try
        {
            await Task.WhenAll(
                    db.KeyDeleteAsync(RedisQueueConventions.GetAttachmentMetadataKey(queueName, id)),
                    db.KeyDeleteAsync(RedisQueueConventions.GetAttachmentBinaryKey(queueName, id)))
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
