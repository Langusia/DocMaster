namespace DocMaster.Api.Services;

public interface IStreamProcessor
{
    Task<ProcessedFile> ProcessAsync(
        Stream input,
        string key,
        string? claimedContentType,
        string? originalFilename,
        CancellationToken ct);
}

public class ProcessedFile
{
    public required string Checksum { get; init; }
    public long Size { get; init; }
    public required List<ChunkData> Chunks { get; init; }
    public required MimeDetectionResult MimeResult { get; init; }
}

public class ChunkData
{
    public int Index { get; init; }
    public required byte[] Data { get; init; }
    public required string Checksum { get; init; }
}
