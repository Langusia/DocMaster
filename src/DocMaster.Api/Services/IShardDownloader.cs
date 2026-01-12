namespace DocMaster.Api.Services;

public interface IShardDownloader
{
    Task<ShardDownloadResult> DownloadShardAsync(
        string objectId,
        int chunkIndex,
        int shardIndex,
        string nodeId,
        bool isReplicated,
        CancellationToken ct);
}

public class ShardDownloadResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public byte[]? Data { get; init; }
}
