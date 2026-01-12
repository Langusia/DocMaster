namespace DocMaster.Api.Services;

public class NodeSelector : INodeSelector
{
    private readonly INodeCache _nodeCache;
    private readonly Random _random = new();

    public NodeSelector(INodeCache nodeCache)
    {
        _nodeCache = nodeCache;
    }

    public NodeSelectionResult SelectForErasureCodedWrite(int shardCount)
    {
        return SelectNodes(shardCount);
    }

    public NodeSelectionResult SelectForReplicatedWrite(int replicaCount)
    {
        return SelectNodes(replicaCount);
    }

    public SingleNodeResult SelectSingleNode(HashSet<string>? excludeNodeIds)
    {
        var healthyNodes = _nodeCache.GetHealthyNodes();

        if (excludeNodeIds != null && excludeNodeIds.Count > 0)
        {
            healthyNodes = healthyNodes.Where(n => !excludeNodeIds.Contains(n.Id)).ToList();
        }

        if (healthyNodes.Count == 0)
        {
            return new SingleNodeResult
            {
                Success = false,
                Error = "No healthy nodes available"
            };
        }

        var scored = ScoreNodes(healthyNodes);
        var selected = scored.OrderByDescending(s => s.Score).First().Node;

        return new SingleNodeResult
        {
            Success = true,
            Node = selected
        };
    }

    private NodeSelectionResult SelectNodes(int count)
    {
        var healthyNodes = _nodeCache.GetHealthyNodes();

        if (healthyNodes.Count < count)
        {
            return new NodeSelectionResult
            {
                Success = false,
                Error = $"Not enough healthy nodes. Required: {count}, Available: {healthyNodes.Count}",
                RequestedCount = count,
                AvailableCount = healthyNodes.Count
            };
        }

        var scored = ScoreNodes(healthyNodes);
        var selected = scored
            .OrderByDescending(s => s.Score)
            .Take(count)
            .Select(s => s.Node)
            .ToList();

        return new NodeSelectionResult
        {
            Success = true,
            SelectedNodes = selected,
            RequestedCount = count,
            AvailableCount = healthyNodes.Count
        };
    }

    private List<(CachedNode Node, double Score)> ScoreNodes(IReadOnlyList<CachedNode> nodes)
    {
        var result = new List<(CachedNode, double)>();

        foreach (var node in nodes)
        {
            var score = CalculateScore(node);
            result.Add((node, score));
        }

        return result;
    }

    private double CalculateScore(CachedNode node)
    {
        double score = 0;

        // Free space percentage (0-50 points)
        if (node.TotalSpaceBytes > 0 && node.FreeSpaceBytes.HasValue)
        {
            var freePercent = (double)node.FreeSpaceBytes.Value / node.TotalSpaceBytes.Value;
            score += freePercent * 50;
        }
        else
        {
            // No space info, assume 50% free
            score += 25;
        }

        // Penalty for recent failures (-10 per failure)
        score -= node.ConsecutiveFailures * 10;

        // Random factor for distribution (0-10 points)
        score += _random.NextDouble() * 10;

        return score;
    }
}
