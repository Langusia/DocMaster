namespace DocMaster.Api.Data.Entities;

public class StorageObject
{
    public string Id { get; set; } = null!;
    public string BucketId { get; set; } = null!;
    public string Key { get; set; } = null!;
    public long SizeBytes { get; set; }
    public string Checksum { get; set; } = null!;

    // MIME detection results
    public string ContentType { get; set; } = null!;
    public string? DetectedContentType { get; set; }
    public string? ClaimedContentType { get; set; }
    public string? DetectedExtension { get; set; }
    public string? OriginalFilename { get; set; }

    // Storage info
    public StorageStrategy StorageStrategy { get; set; }
    public int ChunkCount { get; set; } = 1;
    public ObjectStatus Status { get; set; } = ObjectStatus.Healthy;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Bucket Bucket { get; set; } = null!;
    public ICollection<Chunk> Chunks { get; set; } = new List<Chunk>();
    public ICollection<Replica> Replicas { get; set; } = new List<Replica>();
}

public enum StorageStrategy
{
    Replicated,
    ErasureCoded
}

public enum ObjectStatus
{
    Uploading,
    Healthy,
    Degraded,
    Failed
}
