namespace DocMaster.Api.Data.Entities;

public class Shard
{
    public string Id { get; set; } = null!;
    public string ChunkId { get; set; } = null!;
    public int ShardIndex { get; set; }
    public string NodeId { get; set; } = null!;
    public long SizeBytes { get; set; }
    public string Checksum { get; set; } = null!;
    public ShardStatus Status { get; set; } = ShardStatus.Healthy;

    public Chunk Chunk { get; set; } = null!;
    public Node Node { get; set; } = null!;
}

public enum ShardStatus
{
    Healthy,
    Missing,
    Corrupted
}
