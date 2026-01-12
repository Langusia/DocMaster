namespace DocMaster.Api.Services;

public interface IShardUploader
{
    Task<ShardUploadResult> UploadShardAsync(
        string objectId,
        int chunkIndex,
        int shardIndex,
        byte[] data,
        IReadOnlyList<CachedNode> preferredNodes,
        bool isReplicated,
        CancellationToken ct);

    Task<ShardUploadResult> UploadReplicaAsync(
        string objectId,
        byte[] data,
        CachedNode node,
        CancellationToken ct);
}

public class ShardUploadResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? NodeId { get; init; }
    public string? Checksum { get; init; }
    public long BytesWritten { get; init; }
}
