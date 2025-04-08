using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using NUnit.Framework;

namespace KnightBus.Redis.Tests.Integration;

public class RedisAttachmentProviderTests
{
    private RedisAttachmentProvider _target;

    [SetUp]
    public void Setup()
    {
        _target = new RedisAttachmentProvider(RedisTestBase.Multiplexer, RedisTestBase.Configuration);
    }

    [Test]
    public async Task UploadAttachmentAsync_SavesMetadataToBlob()
    {
        // Arrange
        var ms = new MemoryStream();
        var metadata = new Dictionary<string, string> { { "key", "value" }, { "utf8-values", "åäö ÅÄÖ" }, { RedisAttachmentProvider.ContentType, "blabla" }, { RedisAttachmentProvider.FileName, "blabla" }};
        var attachment = new MessageAttachment("filename.csv", MediaTypeNames.Text.Csv, ms, metadata);
        var id = Guid.NewGuid().ToString("N");
        
        // Act
        await _target.UploadAttachmentAsync("queue", id, attachment);
        
        // Assert
        var result = await _target.GetAttachmentAsync("queue", id);
        result.Metadata.Should().BeEquivalentTo(new Dictionary<string,string>
        {
            { "key", "value"},
            { "utf8-values", "åäö ÅÄÖ" },
            { RedisAttachmentProvider.FileName, "filename.csv" },
            { RedisAttachmentProvider.ContentType, "text/csv" },
        });
    }
}
