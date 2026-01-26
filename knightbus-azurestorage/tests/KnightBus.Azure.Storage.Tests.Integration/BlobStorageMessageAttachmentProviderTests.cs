using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
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
        result.Stream.CanRead.Should().NotBe(false);
        result.Stream.CanSeek.Should().NotBe(false);
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
    public async Task Compression_CompressedBlobIsSmallerThanOriginal()
    {
        // Arrange
        var options = new BlobStorageAttachmentOptions { EnableCompression = true };
        var provider = new BlobStorageMessageAttachmentProvider(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            options
        );

        // Create highly compressible content (repeated text)
        var originalContent = string.Join(
            "",
            Enumerable.Repeat("This is repeated text for compression. ", 100)
        );
        var originalSize = Encoding.UTF8.GetByteCount(originalContent);
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(originalContent));
        var attachment = new MessageAttachment("compressible.txt", MediaTypeNames.Text.Plain, ms);

        // Act
        var id = await provider.UploadAttachmentAsync("size-test", attachment);

        // Assert - Check blob size is smaller than original
        var blobClient = new BlobClient(StorageSetup.ConnectionString, "size-test", id);
        var properties = await blobClient.GetPropertiesAsync();
        properties.Value.ContentLength.Should().BeLessThan(originalSize);
    }

    [Test]
    public async Task Compression_DifferentCompressionLevelsWork()
    {
        // Arrange
        var optionsFastest = new BlobStorageAttachmentOptions
        {
            EnableCompression = true,
            CompressionLevel = CompressionLevel.Optimal,
        };
        var provider = new BlobStorageMessageAttachmentProvider(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            optionsFastest
        );

        var originalContent = string.Join(
            "",
            Enumerable.Repeat("Test content for fastest compression. ", 50)
        );
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(originalContent));
        var attachment = new MessageAttachment("fastest.txt", MediaTypeNames.Text.Plain, ms);

        // Act
        var id = await provider.UploadAttachmentAsync("level-test", attachment);
        var result = await provider.GetAttachmentAsync("level-test", id);

        // Assert
        using var reader = new StreamReader(result.Stream);
        var downloadedContent = await reader.ReadToEndAsync();
        downloadedContent.Should().Be(originalContent);
    }
}
