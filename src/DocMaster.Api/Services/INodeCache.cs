namespace DocMaster.Api.Services;

public interface INodeCache
{
    IReadOnlyList<CachedNode> GetAllNodes();
    IReadOnlyList<CachedNode> GetHealthyNodes();
    CachedNode? GetNode(string nodeId);
    void UpdateNodes(IEnumerable<CachedNode> nodes);
    void UpdateNode(CachedNode node);
    void MarkNodeFailure(string nodeId);
    void MarkNodeSuccess(string nodeId);
}

public class CachedNode
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string GrpcAddress { get; init; }
    public bool IsHealthy { get; set; }
    public long? TotalSpaceBytes { get; set; }
    public long? FreeSpaceBytes { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTime? LastSeenAt { get; set; }
}
