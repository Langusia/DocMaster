namespace DocMaster.Api.Services;

public interface IMimeDetector
{
    MimeDetectionResult Detect(
        byte[] header,
        byte[]? fullFirstChunk,
        string? claimedContentType,
        string? filenameHint);
}

public class MimeDetectionResult
{
    public required string ContentType { get; init; }
    public string? DetectedContentType { get; init; }
    public string? ClaimedContentType { get; init; }
    public string? DetectedExtension { get; init; }
    public DetectionMethod Method { get; init; }
    public bool IsMismatch { get; init; }
    public bool IsDangerousMismatch { get; init; }
}

public enum DetectionMethod
{
    MagicBytes,
    ZipInspection,
    Ole2Extension,
    Header,
    Extension,
    TextHeuristic,
    Fallback
}
