using DocMaster.Api.Configuration;
using DocMaster.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DocMaster.Api.Tests.Services;

public class StreamProcessorTests
{
    private readonly StreamProcessor _processor;
    private readonly Mock<IMimeDetector> _mimeDetectorMock;

    public StreamProcessorTests()
    {
        _mimeDetectorMock = new Mock<IMimeDetector>();
        _mimeDetectorMock.Setup(m => m.Detect(
                It.IsAny<byte[]>(),
                It.IsAny<byte[]?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns(new MimeDetectionResult
            {
                ContentType = "application/octet-stream",
                Method = DetectionMethod.Fallback
            });

        var options = Options.Create(new ErasureCodingOptions
        {
            ChunkSizeBytes = 1024 * 1024 // 1MB for testing
        });

        _processor = new StreamProcessor(_mimeDetectorMock.Object, options);
    }

    [Fact]
    public async Task ProcessAsync_SmallFile_ReturnsSingleChunk()
    {
        var data = new byte[1024]; // 1KB
        Random.Shared.NextBytes(data);
        using var stream = new MemoryStream(data);

        var result = await _processor.ProcessAsync(stream, "test.bin", null, null, CancellationToken.None);

        result.Size.Should().Be(1024);
        result.Chunks.Should().HaveCount(1);
        result.Chunks[0].Index.Should().Be(0);
        result.Chunks[0].Data.Should().BeEquivalentTo(data);
        result.Checksum.Should().StartWith("sha256:");
    }

    [Fact]
    public async Task ProcessAsync_LargeFile_ReturnsMultipleChunks()
    {
        var data = new byte[3 * 1024 * 1024]; // 3MB
        Random.Shared.NextBytes(data);
        using var stream = new MemoryStream(data);

        var result = await _processor.ProcessAsync(stream, "large.bin", null, null, CancellationToken.None);

        result.Size.Should().Be(3 * 1024 * 1024);
        result.Chunks.Should().HaveCount(3); // 3 chunks of 1MB each
        result.Chunks[0].Index.Should().Be(0);
        result.Chunks[1].Index.Should().Be(1);
        result.Chunks[2].Index.Should().Be(2);
    }

    [Fact]
    public async Task ProcessAsync_EmptyFile_ReturnsEmptyChunks()
    {
        using var stream = new MemoryStream();

        var result = await _processor.ProcessAsync(stream, "empty.bin", null, null, CancellationToken.None);

        result.Size.Should().Be(0);
        result.Chunks.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_SameData_ReturnsSameChecksum()
    {
        var data = new byte[1024];
        Array.Fill(data, (byte)'A');

        using var stream1 = new MemoryStream(data);
        using var stream2 = new MemoryStream(data);

        var result1 = await _processor.ProcessAsync(stream1, "test1.bin", null, null, CancellationToken.None);
        var result2 = await _processor.ProcessAsync(stream2, "test2.bin", null, null, CancellationToken.None);

        result1.Checksum.Should().Be(result2.Checksum);
    }

    [Fact]
    public async Task ProcessAsync_CallsMimeDetector()
    {
        var data = new byte[512];
        Random.Shared.NextBytes(data);
        using var stream = new MemoryStream(data);

        await _processor.ProcessAsync(stream, "test.bin", "application/pdf", "document.pdf", CancellationToken.None);

        _mimeDetectorMock.Verify(m => m.Detect(
            It.IsAny<byte[]>(),
            It.IsAny<byte[]?>(),
            "application/pdf",
            "document.pdf"), Times.Once);
    }
}
