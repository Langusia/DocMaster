namespace DocMaster.Api.Models;

public record UploadResponse(
    string Id,
    string Bucket,
    string Key,
    long Size,
    string Checksum,
    string ContentType,
    string StorageStrategy,
    DateTime CreatedAt);

public record ObjectInfo(
    string Id,
    string Bucket,
    string Key,
    long Size,
    string Checksum,
    string ContentType,
    string? DetectedContentType,
    string? DetectedExtension,
    string? OriginalFilename,
    string StorageStrategy,
    string Status,
    int ChunkCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record ObjectListResponse(IReadOnlyList<ObjectInfo> Objects);

public record ObjectStatusResponse(
    string Id,
    string Bucket,
    string Key,
    string Status,
    string StorageStrategy,
    int TotalShards,
    int HealthyShards,
    int MissingShards,
    int CorruptedShards,
    IReadOnlyList<ShardInfo>? Shards,
    IReadOnlyList<ReplicaInfo>? Replicas);

public record ShardInfo(
    int ChunkIndex,
    int ShardIndex,
    string NodeId,
    string NodeName,
    string Status);

public record ReplicaInfo(
    string NodeId,
    string NodeName,
    string Status);

public record HealResponse(
    string Id,
    bool Success,
    string? Error,
    int ShardsHealed,
    int ReplicasHealed);
