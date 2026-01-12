namespace DocMaster.Api.Data.Entities;

public class Node
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string GrpcAddress { get; set; } = null!;

    // Health info
    public bool IsHealthy { get; set; } = true;
    public long? TotalSpaceBytes { get; set; }
    public long? FreeSpaceBytes { get; set; }
    public long? UsedSpaceBytes { get; set; }
    public int? ObjectCount { get; set; }
    public int ConsecutiveFailures { get; set; }

    public DateTime? LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Shard> Shards { get; set; } = new List<Shard>();
    public ICollection<Replica> Replicas { get; set; } = new List<Replica>();
}
