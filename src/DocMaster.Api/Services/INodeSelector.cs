namespace DocMaster.Api.Services;

public interface INodeSelector
{
    NodeSelectionResult SelectForErasureCodedWrite(int shardCount);
    NodeSelectionResult SelectForReplicatedWrite(int replicaCount);
    SingleNodeResult SelectSingleNode(HashSet<string>? excludeNodeIds);
}

public class NodeSelectionResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<CachedNode> SelectedNodes { get; init; } = [];
    public int RequestedCount { get; init; }
    public int AvailableCount { get; init; }
}

public class SingleNodeResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public CachedNode? Node { get; init; }
}
