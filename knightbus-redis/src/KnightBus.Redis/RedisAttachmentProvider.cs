using System;
using System.IO;
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

    private const string ContentType = "contenttype";
    private const string FileName = "filename";

    public RedisAttachmentProvider(
        IConnectionMultiplexer multiplexer,
        IRedisConfiguration configuration
    )
    {
        _multiplexer = multiplexer;
        _configuration = configuration;
    }

    public async Task<IMessageAttachment> GetAttachmentAsync(
        string queueName,
        string id,
        CancellationToken cancellationToken = default(CancellationToken)
    )
    {
        var db = _multiplexer.GetDatabase(_configuration.DatabaseId);

        var metadataHash = await db.HashGetAllAsync(
                RedisQueueConventions.GetAttachmentMetadataKey(queueName, id)
            )
            .ConfigureAwait(false);
        var data = await db.StringGetAsync(
                RedisQueueConventions.GetAttachmentBinaryKey(queueName, id)
            )
            .ConfigureAwait(false);
        var metadata = metadataHash.ToStringDictionary();
        return new MessageAttachment(
            metadata[FileName],
            metadata[ContentType],
            new MemoryStream(data)
        );
    }

    public async Task UploadAttachmentAsync(
        string queueName,
        string id,
        IMessageAttachment attachment,
        CancellationToken cancellationToken = default(CancellationToken)
    )
    {
        var db = _multiplexer.GetDatabase(_configuration.DatabaseId);

        var hash = new HashEntry[]
        {
            new HashEntry(FileName, attachment.Filename),
            new HashEntry(ContentType, attachment.ContentType),
        };

        using (var memoryStream = new MemoryStream())
        {
            await attachment.Stream.CopyToAsync(memoryStream).ConfigureAwait(false);
            await Task.WhenAll(
                    db.HashSetAsync(
                        RedisQueueConventions.GetAttachmentMetadataKey(queueName, id),
                        hash
                    ),
                    db.StringSetAsync(
                        RedisQueueConventions.GetAttachmentBinaryKey(queueName, id),
                        memoryStream.ToArray()
                    )
                )
                .ConfigureAwait(false);
        }
    }

    public async Task<bool> DeleteAttachmentAsync(
        string queueName,
        string id,
        CancellationToken cancellationToken = default(CancellationToken)
    )
    {
        var db = _multiplexer.GetDatabase(_configuration.DatabaseId);

        try
        {
            await Task.WhenAll(
                    db.KeyDeleteAsync(
                        RedisQueueConventions.GetAttachmentMetadataKey(queueName, id)
                    ),
                    db.KeyDeleteAsync(RedisQueueConventions.GetAttachmentBinaryKey(queueName, id))
                )
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
