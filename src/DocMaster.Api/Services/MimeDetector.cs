using System.IO.Compression;
using System.Text;

namespace DocMaster.Api.Services;

public class MimeDetector : IMimeDetector
{
    private static readonly Dictionary<string, (string MimeType, string Extension)> MagicBytes = new()
    {
        // Images
        { "89504E47", ("image/png", "png") },
        { "FFD8FF", ("image/jpeg", "jpg") },
        { "47494638", ("image/gif", "gif") },
        { "424D", ("image/bmp", "bmp") },
        { "49492A00", ("image/tiff", "tiff") },
        { "4D4D002A", ("image/tiff", "tiff") },
        { "52494646", ("image/webp", "webp") }, // RIFF header, needs further check for WEBP

        // Documents
        { "25504446", ("application/pdf", "pdf") },

        // Archives
        { "504B0304", ("application/zip", "zip") }, // ZIP/Office OOXML
        { "504B0506", ("application/zip", "zip") }, // Empty ZIP
        { "504B0708", ("application/zip", "zip") }, // Spanned ZIP

        // OLE2 (Legacy Office)
        { "D0CF11E0A1B11AE1", ("application/x-ole2", "ole2") },

        // Executables (dangerous)
        { "4D5A", ("application/x-msdownload", "exe") },
        { "7F454C46", ("application/x-executable", "elf") },
    };

    private static readonly Dictionary<string, string> ExtensionToMime = new(StringComparer.OrdinalIgnoreCase)
    {
        // Images
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".gif", "image/gif" },
        { ".bmp", "image/bmp" },
        { ".tiff", "image/tiff" },
        { ".tif", "image/tiff" },
        { ".webp", "image/webp" },
        { ".svg", "image/svg+xml" },
        { ".ico", "image/x-icon" },

        // Documents
        { ".pdf", "application/pdf" },
        { ".doc", "application/msword" },
        { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        { ".xls", "application/vnd.ms-excel" },
        { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        { ".ppt", "application/vnd.ms-powerpoint" },
        { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },

        // Text
        { ".txt", "text/plain" },
        { ".html", "text/html" },
        { ".htm", "text/html" },
        { ".css", "text/css" },
        { ".js", "text/javascript" },
        { ".json", "application/json" },
        { ".xml", "application/xml" },
        { ".csv", "text/csv" },
        { ".md", "text/markdown" },

        // Archives
        { ".zip", "application/zip" },
        { ".tar", "application/x-tar" },
        { ".gz", "application/gzip" },
        { ".7z", "application/x-7z-compressed" },
        { ".rar", "application/vnd.rar" },

        // Media
        { ".mp3", "audio/mpeg" },
        { ".mp4", "video/mp4" },
        { ".avi", "video/x-msvideo" },
        { ".mov", "video/quicktime" },
        { ".wav", "audio/wav" },

        // Other
        { ".exe", "application/x-msdownload" },
        { ".dll", "application/x-msdownload" },
    };

    private static readonly HashSet<string> DangerousTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/x-msdownload",
        "application/x-executable",
        "application/x-msdos-program",
    };

    public MimeDetectionResult Detect(
        byte[] header,
        byte[]? fullFirstChunk,
        string? claimedContentType,
        string? filenameHint)
    {
        // Try magic bytes first
        var magicResult = DetectByMagicBytes(header);
        if (magicResult != null)
        {
            // Handle ZIP files - might be Office OOXML
            if (magicResult.Value.MimeType == "application/zip" && fullFirstChunk != null)
            {
                var officeResult = TryDetectOfficeOoxml(fullFirstChunk);
                if (officeResult != null)
                {
                    return BuildResult(
                        officeResult.Value.MimeType,
                        officeResult.Value.MimeType,
                        officeResult.Value.Extension,
                        DetectionMethod.ZipInspection,
                        claimedContentType);
                }
            }

            // Handle OLE2 - need extension for legacy Office
            if (magicResult.Value.MimeType == "application/x-ole2" && filenameHint != null)
            {
                var ext = Path.GetExtension(filenameHint)?.ToLowerInvariant();
                var oleResult = DetectLegacyOfficeByExtension(ext);
                if (oleResult != null)
                {
                    return BuildResult(
                        oleResult.Value.MimeType,
                        oleResult.Value.MimeType,
                        oleResult.Value.Extension,
                        DetectionMethod.Ole2Extension,
                        claimedContentType);
                }
            }

            // Handle WEBP (needs RIFF + WEBP check)
            if (magicResult.Value.MimeType == "image/webp" && header.Length >= 12)
            {
                var webpMarker = Encoding.ASCII.GetString(header, 8, 4);
                if (webpMarker != "WEBP")
                {
                    // Not actually WEBP, it's some other RIFF format
                    magicResult = null;
                }
            }

            if (magicResult != null)
            {
                return BuildResult(
                    magicResult.Value.MimeType,
                    magicResult.Value.MimeType,
                    magicResult.Value.Extension,
                    DetectionMethod.MagicBytes,
                    claimedContentType);
            }
        }

        // Try Content-Type header
        if (!string.IsNullOrWhiteSpace(claimedContentType))
        {
            var ext = GetExtensionForMimeType(claimedContentType);
            return BuildResult(
                claimedContentType,
                null,
                ext,
                DetectionMethod.Header,
                claimedContentType);
        }

        // Try extension from filename
        if (!string.IsNullOrWhiteSpace(filenameHint))
        {
            var ext = Path.GetExtension(filenameHint);
            if (!string.IsNullOrEmpty(ext) && ExtensionToMime.TryGetValue(ext, out var mimeType))
            {
                return BuildResult(
                    mimeType,
                    null,
                    ext.TrimStart('.'),
                    DetectionMethod.Extension,
                    claimedContentType);
            }
        }

        // Try text heuristic
        if (header.Length > 0 && IsLikelyText(header))
        {
            return BuildResult(
                "text/plain",
                "text/plain",
                "txt",
                DetectionMethod.TextHeuristic,
                claimedContentType);
        }

        // Default fallback
        return BuildResult(
            "application/octet-stream",
            null,
            null,
            DetectionMethod.Fallback,
            claimedContentType);
    }

    private (string MimeType, string Extension)? DetectByMagicBytes(byte[] header)
    {
        if (header.Length < 2)
            return null;

        var hex = Convert.ToHexString(header);

        foreach (var (magic, result) in MagicBytes)
        {
            if (hex.StartsWith(magic, StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }
        }

        return null;
    }

    private (string MimeType, string Extension)? TryDetectOfficeOoxml(byte[] data)
    {
        try
        {
            using var stream = new MemoryStream(data);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            // Check for Office OOXML markers
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName.ToLowerInvariant();

                if (name.StartsWith("word/"))
                    return ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx");

                if (name.StartsWith("xl/"))
                    return ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx");

                if (name.StartsWith("ppt/"))
                    return ("application/vnd.openxmlformats-officedocument.presentationml.presentation", "pptx");
            }
        }
        catch
        {
            // Not a valid ZIP or couldn't read
        }

        return null;
    }

    private (string MimeType, string Extension)? DetectLegacyOfficeByExtension(string? ext)
    {
        return ext switch
        {
            ".doc" => ("application/msword", "doc"),
            ".xls" => ("application/vnd.ms-excel", "xls"),
            ".ppt" => ("application/vnd.ms-powerpoint", "ppt"),
            _ => null
        };
    }

    private bool IsLikelyText(byte[] data)
    {
        if (data.Length == 0)
            return false;

        var printableCount = 0;
        foreach (var b in data)
        {
            // Consider printable ASCII + common whitespace
            if ((b >= 32 && b <= 126) || b == 9 || b == 10 || b == 13)
            {
                printableCount++;
            }
        }

        // 85% threshold
        return (double)printableCount / data.Length >= 0.85;
    }

    private string? GetExtensionForMimeType(string mimeType)
    {
        foreach (var (ext, mime) in ExtensionToMime)
        {
            if (mime.Equals(mimeType, StringComparison.OrdinalIgnoreCase))
            {
                return ext.TrimStart('.');
            }
        }

        return null;
    }

    private MimeDetectionResult BuildResult(
        string contentType,
        string? detectedContentType,
        string? detectedExtension,
        DetectionMethod method,
        string? claimedContentType)
    {
        var isMismatch = claimedContentType != null &&
            !string.Equals(contentType, claimedContentType, StringComparison.OrdinalIgnoreCase);

        var isDangerous = detectedContentType != null &&
            DangerousTypes.Contains(detectedContentType) &&
            claimedContentType != null &&
            !DangerousTypes.Contains(claimedContentType);

        return new MimeDetectionResult
        {
            ContentType = contentType,
            DetectedContentType = detectedContentType,
            ClaimedContentType = claimedContentType,
            DetectedExtension = detectedExtension,
            Method = method,
            IsMismatch = isMismatch,
            IsDangerousMismatch = isDangerous
        };
    }
}
