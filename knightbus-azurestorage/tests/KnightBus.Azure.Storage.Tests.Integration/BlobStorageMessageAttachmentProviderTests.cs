using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using FluentAssertions;
using KnightBus.Core;
using NUnit.Framework;

namespace KnightBus.Azure.Storage.Tests.Integration;

public class BlobStorageMessageAttachmentProviderTests
{
    private BlobStorageMessageAttachmentProvider _target;

    [SetUp]
    public void Setup()
    {
        _target = new BlobStorageMessageAttachmentProvider(StorageSetup.ConnectionString);
    }

    [Test]
    public async Task UploadAttachmentAsync_SavesMetadataToBlob()
    {
        // Arrange
        using var ms = new MemoryStream();
        var metadata = new Dictionary<string, string>
        {
            { "key", "value" },
            { "supports-uf8-values", "åäö ÅÄÖ hej" },
            { BlobStorageMessageAttachmentProvider.FileNameKey, "blabla" },
        };
        var attachment = new MessageAttachment(
            "filename.csv",
            MediaTypeNames.Text.Csv,
            ms,
            metadata
        );

        // Act
        var id = await _target.UploadAttachmentAsync("queue", attachment);

        // Assert
        var result = await _target.GetAttachmentAsync("queue", id);
        result
            .Metadata.Should()
            .BeEquivalentTo(
                new Dictionary<string, string>
                {
                    { "key", "value" },
                    { "supports-uf8-values", "åäö ÅÄÖ hej" },
                    { BlobStorageMessageAttachmentProvider.FileNameKey, "filename.csv" },
                }
            );
    }

    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public async Task GetAttachmentAsync_StreamShouldNotBeDisposed(bool useCompression)
    {
        // Arrange
        var options = new BlobStorageAttachmentOptions { EnableCompression = useCompression };
        var provider = new BlobStorageMessageAttachmentProvider(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            options
        );

        string id;
        using (var ms = new MemoryStream(Encoding.UTF8.GetBytes("Message")))
        {
            var attachment = new MessageAttachment("dispose.txt", MediaTypeNames.Text.Plain, ms);
            id = await provider.UploadAttachmentAsync("dispose-test", attachment);
        }

        // Act
        var result = await provider.GetAttachmentAsync("dispose-test", id);

        // Assert
        result.Stream.CanRead.Should().BeTrue();
    }

    [Test]
    public async Task Compression_Upload_ShouldHaveCorrectExtensionAndEncoding()
    {
        // Arrange
        var options = new BlobStorageAttachmentOptions { EnableCompression = true };
        var provider = new BlobStorageMessageAttachmentProvider(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            options
        );

        var originalContent = "Test content";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(originalContent));
        var attachment = new MessageAttachment("test.txt", MediaTypeNames.Text.Plain, ms);

        // Act
        var id = await provider.UploadAttachmentAsync("compression-test", attachment);

        // Assert
        id.Should().EndWith(".brotli");
        var properties = await new BlobClient(
            StorageSetup.ConnectionString,
            "compression-test",
            id
        ).GetPropertiesAsync();
        properties.Value.ContentEncoding.Should().Be("br");
    }

    [Test]
    public async Task Compression_UploadAndDownload_RoundTripsCorrectly()
    {
        // Arrange
        var options = new BlobStorageAttachmentOptions { EnableCompression = true };
        var provider = new BlobStorageMessageAttachmentProvider(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            options
        );

        var originalContent =
            "This is test content that should be compressed and decompressed correctly.";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(originalContent));
        var attachment = new MessageAttachment("test.txt", MediaTypeNames.Text.Plain, ms);

        // Act
        var id = await provider.UploadAttachmentAsync("compression-test", attachment);
        var result = await provider.GetAttachmentAsync("compression-test", id);

        // Assert
        using var reader = new StreamReader(result.Stream);
        var downloadedContent = await reader.ReadToEndAsync();
        downloadedContent.Should().Be(originalContent);
        result.Filename.Should().Be("test.txt");
        result.ContentType.Should().Be(MediaTypeNames.Text.Plain);
    }

    [Test]
    public async Task Compression_UncompressedBlobReadableWithCompressionEnabled()
    {
        // Arrange - Upload without compression
        var providerNoCompression = new BlobStorageMessageAttachmentProvider(
            StorageSetup.ConnectionString
        );

        var originalContent = "Uncompressed content for backwards compatibility test.";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(originalContent));
        var attachment = new MessageAttachment("uncompressed.txt", MediaTypeNames.Text.Plain, ms);

        var id = await providerNoCompression.UploadAttachmentAsync("compat-test", attachment);

        // Act - Read with compression-enabled provider
        var providerWithCompression = new BlobStorageMessageAttachmentProvider(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            new BlobStorageAttachmentOptions { EnableCompression = true }
        );
        var result = await providerWithCompression.GetAttachmentAsync("compat-test", id);

        // Assert
        using var reader = new StreamReader(result.Stream);
        var downloadedContent = await reader.ReadToEndAsync();
        downloadedContent.Should().Be(originalContent);
    }

    [Test]
    public async Task Compression_CompressedBlobReadableWithCompressionDisabled()
    {
        // Arrange - Upload with compression
        var providerWithCompression = new BlobStorageMessageAttachmentProvider(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            new BlobStorageAttachmentOptions { EnableCompression = true }
        );

        var originalContent =
            "Compressed content that should be readable by non-compression provider.";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(originalContent));
        var attachment = new MessageAttachment("compressed.txt", MediaTypeNames.Text.Plain, ms);

        var id = await providerWithCompression.UploadAttachmentAsync("compat-test-2", attachment);

        // Act - Read with compression-disabled provider (should still decompress based on metadata)
        var providerNoCompression = new BlobStorageMessageAttachmentProvider(
            StorageSetup.ConnectionString
        );
        var result = await providerNoCompression.GetAttachmentAsync("compat-test-2", id);

        // Assert
        using var reader = new StreamReader(result.Stream);
        var downloadedContent = await reader.ReadToEndAsync();
        downloadedContent.Should().Be(originalContent);
    }

    [Test]
    public async Task Compression_LargeIncompressibleAttachment_RoundTripsCorrectly()
    {
        // Arrange - incompressible data large enough that upload and download actually stream
        var provider = new BlobStorageMessageAttachmentProvider(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            new BlobStorageAttachmentOptions { EnableCompression = true }
        );

        var originalContent = new byte[8 * 1024 * 1024];
        new Random(42).NextBytes(originalContent);
        using var ms = new MemoryStream(originalContent);
        var attachment = new MessageAttachment("large.bin", MediaTypeNames.Application.Octet, ms);

        // Act
        var id = await provider.UploadAttachmentAsync("large-test", attachment);
        var result = await provider.GetAttachmentAsync("large-test", id);

        // Assert - large enough to overflow the single-request threshold into staged blocks
        using var downloaded = new MemoryStream();
        await result.Stream.CopyToAsync(downloaded);
        downloaded.ToArray().Should().Equal(originalContent);
        var blocks = await new BlockBlobClient(
            StorageSetup.ConnectionString,
            "large-test",
            id
        ).GetBlockListAsync();
        blocks.Value.CommittedBlocks.Should().NotBeEmpty();
    }

    [Test]
    public async Task Compression_SmallAttachment_UploadsAsSingleBlob()
    {
        // Arrange
        var provider = new BlobStorageMessageAttachmentProvider(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            new BlobStorageAttachmentOptions { EnableCompression = true }
        );

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes("Small payload"));
        var attachment = new MessageAttachment("small.txt", MediaTypeNames.Text.Plain, ms);

        // Act
        var id = await provider.UploadAttachmentAsync("single-shot-test", attachment);

        // Assert - a blob created by a single PutBlob has no committed block list
        var blocks = await new BlockBlobClient(
            StorageSetup.ConnectionString,
            "single-shot-test",
            id
        ).GetBlockListAsync();
        blocks.Value.CommittedBlocks.Should().BeEmpty();
    }

    [Test]
    public async Task Compression_PreservesUncompressedLength()
    {
        // Arrange
        var provider = new BlobStorageMessageAttachmentProvider(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            new BlobStorageAttachmentOptions { EnableCompression = true }
        );

        var originalContent = Encoding.UTF8.GetBytes("Content whose length must survive the trip");
        using var ms = new MemoryStream(originalContent);
        var attachment = new MessageAttachment("length.txt", MediaTypeNames.Text.Plain, ms);

        // Act
        var id = await provider.UploadAttachmentAsync("length-test", attachment);
        var result = await provider.GetAttachmentAsync("length-test", id);

        // Assert - the decompressing stream cannot report a length, so it comes from metadata
        result.Length.Should().Be(originalContent.Length);
    }

    [Test]
    public async Task Compression_SourceNotAtStart_ReportsDeliveredLength()
    {
        // Arrange - only the bytes after Position are uploaded, and Length must agree
        var provider = new BlobStorageMessageAttachmentProvider(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            new BlobStorageAttachmentOptions { EnableCompression = true }
        );

        var payload = new byte[500];
        new Random(42).NextBytes(payload);
        using var ms = new MemoryStream(payload);
        ms.Position = 100;
        var attachment = new MessageAttachment("mid.bin", MediaTypeNames.Application.Octet, ms);

        // Act
        var id = await provider.UploadAttachmentAsync("length-test", attachment);
        var result = await provider.GetAttachmentAsync("length-test", id);

        // Assert
        using var downloaded = new MemoryStream();
        await result.Stream.CopyToAsync(downloaded);
        downloaded.Length.Should().Be(400);
        result.Length.Should().Be(400);
    }

    [Test]
    public async Task Compression_NonSeekableSource_UploadsFullyAndReportsZeroLength()
    {
        // Arrange
        var provider = new BlobStorageMessageAttachmentProvider(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            new BlobStorageAttachmentOptions { EnableCompression = true }
        );

        var payload = Encoding.UTF8.GetBytes("Payload from a non-seekable source");
        var attachment = new MessageAttachment(
            "nonseekable.txt",
            MediaTypeNames.Text.Plain,
            new NonSeekableReadStream(new MemoryStream(payload))
        );

        // Act
        var id = await provider.UploadAttachmentAsync("length-test", attachment);
        var result = await provider.GetAttachmentAsync("length-test", id);

        // Assert - 0 is the documented Length for non-seekable streams
        using var downloaded = new MemoryStream();
        await result.Stream.CopyToAsync(downloaded);
        downloaded.ToArray().Should().Equal(payload);
        result.Length.Should().Be(0);
    }

    private sealed class NonSeekableReadStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            inner.Read(buffer, offset, count);

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }

    [Test]
    public async Task Compression_UploadPreservesUserMetadata()
    {
        // Arrange
        var provider = new BlobStorageMessageAttachmentProvider(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            new BlobStorageAttachmentOptions { EnableCompression = true }
        );

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes("Message"));
        var attachment = new MessageAttachment(
            "meta.txt",
            MediaTypeNames.Text.Plain,
            ms,
            new Dictionary<string, string>
            {
                { "key", "value" },
                { "supports-uf8-values", "åäö ÅÄÖ hej" },
            }
        );

        // Act
        var id = await provider.UploadAttachmentAsync("meta-test", attachment);
        var result = await provider.GetAttachmentAsync("meta-test", id);

        // Assert - the surface must not depend on whether compression is enabled
        result
            .Metadata.Should()
            .BeEquivalentTo(
                new Dictionary<string, string>
                {
                    { "key", "value" },
                    { "supports-uf8-values", "åäö ÅÄÖ hej" },
                    { BlobStorageMessageAttachmentProvider.FileNameKey, "meta.txt" },
                }
            );
    }

    [Test]
    public async Task Compression_StoresAllMetadataBase64EncodedForOlderReaders()
    {
        // Arrange - readers predating UncompressedLength base64-decode every value
        // except Filename, so a raw value there makes the attachment unreadable
        var provider = new BlobStorageMessageAttachmentProvider(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            new BlobStorageAttachmentOptions { EnableCompression = true }
        );

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes("Message"));
        var attachment = new MessageAttachment("compat.txt", MediaTypeNames.Text.Plain, ms);

        // Act
        var id = await provider.UploadAttachmentAsync("base64-test", attachment);

        // Assert
        var properties = await new BlobClient(
            StorageSetup.ConnectionString,
            "base64-test",
            id
        ).GetPropertiesAsync();
        foreach (var (key, value) in properties.Value.Metadata)
        {
            if (key == BlobStorageMessageAttachmentProvider.FileNameKey)
                continue;
            var decode = () => Convert.FromBase64String(value);
            decode.Should().NotThrow($"'{key}' must be decodable by an older reader");
        }
    }

    [Test]
    public async Task UploadAttachmentAsync_UserMetadataNamedUncompressedLength_RoundTripsWithoutCompression()
    {
        // Arrange - the key is only reserved on compressed blobs
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes("Message"));
        var attachment = new MessageAttachment(
            "collide.txt",
            MediaTypeNames.Text.Plain,
            ms,
            new Dictionary<string, string> { { "UncompressedLength", "mine" } }
        );

        // Act
        var id = await _target.UploadAttachmentAsync("collide-test", attachment);
        var result = await _target.GetAttachmentAsync("collide-test", id);

        // Assert
        result
            .Metadata.Should()
            .BeEquivalentTo(
                new Dictionary<string, string>
                {
                    { "UncompressedLength", "mine" },
                    { BlobStorageMessageAttachmentProvider.FileNameKey, "collide.txt" },
                }
            );
    }

    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public async Task UploadAttachmentAsync_RecreatesContainerDeletedAfterCaching(
        bool useCompression
    )
    {
        // Arrange - prime the provider's container cache, then delete the container
        var containerName = useCompression ? "recreate-compressed" : "recreate-plain";
        var provider = new BlobStorageMessageAttachmentProvider(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            new BlobStorageAttachmentOptions { EnableCompression = useCompression }
        );
        using (var first = new MemoryStream(Encoding.UTF8.GetBytes("first")))
        {
            await provider.UploadAttachmentAsync(
                containerName,
                new MessageAttachment("first.txt", MediaTypeNames.Text.Plain, first)
            );
        }
        await new BlobContainerClient(StorageSetup.ConnectionString, containerName).DeleteAsync();

        // Act
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes("second"));
        var id = await provider.UploadAttachmentAsync(
            containerName,
            new MessageAttachment("second.txt", MediaTypeNames.Text.Plain, ms)
        );

        // Assert
        var result = await provider.GetAttachmentAsync(containerName, id);
        using var reader = new StreamReader(result.Stream);
        (await reader.ReadToEndAsync()).Should().Be("second");
    }

    [Test]
    public async Task Compression_CancelledMidUpload_ThrowsWithoutHanging()
    {
        // Arrange - the source cancels the token partway through being read
        var provider = new BlobStorageMessageAttachmentProvider(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            new BlobStorageAttachmentOptions { EnableCompression = true }
        );

        // Cancel past the single-request threshold so the staged-block path is the one
        // that has to unwind
        using var cts = new CancellationTokenSource();
        await using var source = new CancellingRandomStream(
            length: 64 * 1024 * 1024,
            cancelAfterBytes: 8 * 1024 * 1024,
            cts
        );
        var attachment = new MessageAttachment(
            "cancel.bin",
            MediaTypeNames.Application.Octet,
            source
        );

        // Act
        var uploadTask = provider.UploadAttachmentAsync("cancel-test", attachment, cts.Token);

        // Assert
        var completed = await Task.WhenAny(uploadTask, Task.Delay(TimeSpan.FromSeconds(30)));
        completed.Should().Be(uploadTask, "a cancelled upload must fail, not hang");
        var awaitUpload = () => uploadTask;
        await awaitUpload.Should().ThrowAsync<OperationCanceledException>();
        source.BytesRead.Should().BeLessThan(source.Length);

        var container = new BlobContainerClient(StorageSetup.ConnectionString, "cancel-test");
        var leftovers = new List<string>();
        await foreach (var blob in container.GetBlobsAsync())
            leftovers.Add(blob.Name);
        leftovers.Should().BeEmpty("a failed upload must not leave a blob behind");
    }

    private sealed class CancellingRandomStream(
        long length,
        long cancelAfterBytes,
        CancellationTokenSource cts
    ) : Stream
    {
        private readonly Random _random = new(42);
        private long _position;

        public long BytesRead => _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = (int)Math.Min(count, length - _position);
            if (read <= 0)
                return 0;
            _random.NextBytes(buffer.AsSpan(offset, read));
            _position += read;
            if (_position >= cancelAfterBytes)
                cts.Cancel();
            return read;
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
