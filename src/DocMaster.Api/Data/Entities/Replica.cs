namespace DocMaster.Api.Data.Entities;

public class Replica
{
    public string Id { get; set; } = null!;
    public string ObjectId { get; set; } = null!;
    public string NodeId { get; set; } = null!;
    public string Checksum { get; set; } = null!;
    public ReplicaStatus Status { get; set; } = ReplicaStatus.Healthy;

    public StorageObject Object { get; set; } = null!;
    public Node Node { get; set; } = null!;
}

public enum ReplicaStatus
{
    Healthy,
    Missing,
    Corrupted
}
