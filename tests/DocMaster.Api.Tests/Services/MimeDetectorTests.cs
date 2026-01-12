using DocMaster.Api.Services;
using FluentAssertions;
using Xunit;

namespace DocMaster.Api.Tests.Services;

public class MimeDetectorTests
{
    private readonly MimeDetector _detector = new();

    [Fact]
    public void Detect_PngMagicBytes_ReturnsPng()
    {
        // PNG magic bytes: 89 50 4E 47 0D 0A 1A 0A
        var header = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        var result = _detector.Detect(header, null, null, null);

        result.ContentType.Should().Be("image/png");
        result.DetectedExtension.Should().Be("png");
        result.Method.Should().Be(DetectionMethod.MagicBytes);
    }

    [Fact]
    public void Detect_JpegMagicBytes_ReturnsJpeg()
    {
        // JPEG magic bytes: FF D8 FF
        var header = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };

        var result = _detector.Detect(header, null, null, null);

        result.ContentType.Should().Be("image/jpeg");
        result.DetectedExtension.Should().Be("jpg");
        result.Method.Should().Be(DetectionMethod.MagicBytes);
    }

    [Fact]
    public void Detect_PdfMagicBytes_ReturnsPdf()
    {
        // PDF magic bytes: 25 50 44 46 (%PDF)
        var header = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };

        var result = _detector.Detect(header, null, null, null);

        result.ContentType.Should().Be("application/pdf");
        result.DetectedExtension.Should().Be("pdf");
        result.Method.Should().Be(DetectionMethod.MagicBytes);
    }

    [Fact]
    public void Detect_ExeMagicBytes_ReturnsExecutable()
    {
        // EXE magic bytes: 4D 5A (MZ)
        var header = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };

        var result = _detector.Detect(header, null, null, null);

        result.ContentType.Should().Be("application/x-msdownload");
        result.DetectedExtension.Should().Be("exe");
    }

    [Fact]
    public void Detect_ExeClaimedAsImage_MarksDangerous()
    {
        // EXE magic bytes: 4D 5A (MZ)
        var header = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };

        var result = _detector.Detect(header, null, "image/png", null);

        result.ContentType.Should().Be("application/x-msdownload");
        result.IsMismatch.Should().BeTrue();
        result.IsDangerousMismatch.Should().BeTrue();
    }

    [Fact]
    public void Detect_UnknownBytes_FallsBackToContentType()
    {
        var header = new byte[] { 0x00, 0x00, 0x00, 0x00 };

        var result = _detector.Detect(header, null, "application/custom", null);

        result.ContentType.Should().Be("application/custom");
        result.Method.Should().Be(DetectionMethod.Header);
    }

    [Fact]
    public void Detect_UnknownBytesWithExtension_FallsBackToExtension()
    {
        var header = new byte[] { 0x00, 0x00, 0x00, 0x00 };

        var result = _detector.Detect(header, null, null, "document.json");

        result.ContentType.Should().Be("application/json");
        result.Method.Should().Be(DetectionMethod.Extension);
    }

    [Fact]
    public void Detect_TextContent_DetectsAsText()
    {
        // ASCII text
        var header = "Hello, this is a plain text file with some content.\n"u8.ToArray();

        var result = _detector.Detect(header, null, null, null);

        result.ContentType.Should().Be("text/plain");
        result.Method.Should().Be(DetectionMethod.TextHeuristic);
    }

    [Fact]
    public void Detect_UnknownBinary_ReturnsOctetStream()
    {
        // Random binary data
        var header = new byte[] { 0x00, 0x01, 0x02, 0x80, 0x81, 0x82, 0x83, 0x84 };

        var result = _detector.Detect(header, null, null, null);

        result.ContentType.Should().Be("application/octet-stream");
        result.Method.Should().Be(DetectionMethod.Fallback);
    }

    [Fact]
    public void Detect_GifMagicBytes_ReturnsGif()
    {
        // GIF magic bytes: 47 49 46 38 (GIF8)
        var header = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x00, 0x00 };

        var result = _detector.Detect(header, null, null, null);

        result.ContentType.Should().Be("image/gif");
        result.DetectedExtension.Should().Be("gif");
    }
}
