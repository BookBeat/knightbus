using System;
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
        var id = Guid.NewGuid().ToString("N");

        // Act
        await _target.UploadAttachmentAsync("queue", id, attachment);

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
    public async Task Compression_UploadAndDownload_RoundTripsCorrectly()
    {
        // Arrange
        var options = new BlobStorageAttachmentOptions { EnableCompression = true };
        var provider = new BlobStorageMessageAttachmentProvider(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            options
        );

        var originalContent = "This is test content that should be compressed and decompressed correctly.";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(originalContent));
        var attachment = new MessageAttachment("test.txt", MediaTypeNames.Text.Plain, ms);
        var id = Guid.NewGuid().ToString("N");

        // Act
        await provider.UploadAttachmentAsync("compression-test", id, attachment);
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
        var providerNoCompression = new BlobStorageMessageAttachmentProvider(StorageSetup.ConnectionString);

        var originalContent = "Uncompressed content for backwards compatibility test.";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(originalContent));
        var attachment = new MessageAttachment("uncompressed.txt", MediaTypeNames.Text.Plain, ms);
        var id = Guid.NewGuid().ToString("N");

        await providerNoCompression.UploadAttachmentAsync("compat-test", id, attachment);

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

        var originalContent = "Compressed content that should be readable by non-compression provider.";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(originalContent));
        var attachment = new MessageAttachment("compressed.txt", MediaTypeNames.Text.Plain, ms);
        var id = Guid.NewGuid().ToString("N");

        await providerWithCompression.UploadAttachmentAsync("compat-test-2", id, attachment);

        // Act - Read with compression-disabled provider (should still decompress based on metadata)
        var providerNoCompression = new BlobStorageMessageAttachmentProvider(StorageSetup.ConnectionString);
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
        var originalContent = string.Join("", Enumerable.Repeat("This is repeated text for compression. ", 100));
        var originalSize = Encoding.UTF8.GetByteCount(originalContent);
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(originalContent));
        var attachment = new MessageAttachment("compressible.txt", MediaTypeNames.Text.Plain, ms);
        var id = Guid.NewGuid().ToString("N");

        // Act
        await provider.UploadAttachmentAsync("size-test", id, attachment);

        // Assert - Check blob size is smaller than original
        var blobClient = new BlobClient(StorageSetup.ConnectionString, "size-test", id);
        var properties = await blobClient.GetPropertiesAsync();
        properties.Value.ContentLength.Should().BeLessThan(originalSize);
    }

    [Test]
    public async Task Compression_MetadataPreservedWithCompression()
    {
        // Arrange
        var options = new BlobStorageAttachmentOptions { EnableCompression = true };
        var provider = new BlobStorageMessageAttachmentProvider(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            options
        );

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes("Content with metadata"));
        var metadata = new Dictionary<string, string>
        {
            { "custom-key", "custom-value" },
            { "another-key", "another-value" },
        };
        var attachment = new MessageAttachment("meta.txt", MediaTypeNames.Text.Plain, ms, metadata);
        var id = Guid.NewGuid().ToString("N");

        // Act
        await provider.UploadAttachmentAsync("metadata-test", id, attachment);
        var result = await provider.GetAttachmentAsync("metadata-test", id);

        // Assert
        result.Metadata.Should().Contain("custom-key", "custom-value");
        result.Metadata.Should().Contain("another-key", "another-value");
        result.Metadata.Should().Contain(BlobStorageMessageAttachmentProvider.FileNameKey, "meta.txt");
        result.Metadata.Should().Contain(BlobStorageMessageAttachmentProvider.CompressionKey, BlobStorageMessageAttachmentProvider.CompressionValueGzip);
    }

    [Test]
    public async Task Compression_DifferentCompressionLevelsWork()
    {
        // Arrange
        var optionsFastest = new BlobStorageAttachmentOptions
        {
            EnableCompression = true,
            CompressionLevel = CompressionLevel.Fastest
        };
        var provider = new BlobStorageMessageAttachmentProvider(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            optionsFastest
        );

        var originalContent = string.Join("", Enumerable.Repeat("Test content for fastest compression. ", 50));
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(originalContent));
        var attachment = new MessageAttachment("fastest.txt", MediaTypeNames.Text.Plain, ms);
        var id = Guid.NewGuid().ToString("N");

        // Act
        await provider.UploadAttachmentAsync("level-test", id, attachment);
        var result = await provider.GetAttachmentAsync("level-test", id);

        // Assert
        using var reader = new StreamReader(result.Stream);
        var downloadedContent = await reader.ReadToEndAsync();
        downloadedContent.Should().Be(originalContent);
    }
}
