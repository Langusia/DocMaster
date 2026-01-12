using System.Collections.Immutable;
using DocMaster.Api.Configuration;
using Microsoft.Extensions.Options;

namespace DocMaster.Api.Services;

public class NodeCache : INodeCache
{
    private readonly NodeHealthOptions _options;
    private ImmutableDictionary<string, CachedNode> _nodes = ImmutableDictionary<string, CachedNode>.Empty;
    private readonly object _lock = new();

    public NodeCache(IOptions<NodeHealthOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<CachedNode> GetAllNodes()
    {
        return _nodes.Values.ToList();
    }

    public IReadOnlyList<CachedNode> GetHealthyNodes()
    {
        return _nodes.Values.Where(n => n.IsHealthy).ToList();
    }

    public CachedNode? GetNode(string nodeId)
    {
        return _nodes.TryGetValue(nodeId, out var node) ? node : null;
    }

    public void UpdateNodes(IEnumerable<CachedNode> nodes)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, CachedNode>();
        foreach (var node in nodes)
        {
            builder[node.Id] = node;
        }

        Interlocked.Exchange(ref _nodes, builder.ToImmutable());
    }

    public void UpdateNode(CachedNode node)
    {
        lock (_lock)
        {
            _nodes = _nodes.SetItem(node.Id, node);
        }
    }

    public void MarkNodeFailure(string nodeId)
    {
        lock (_lock)
        {
            if (_nodes.TryGetValue(nodeId, out var node))
            {
                var failures = node.ConsecutiveFailures + 1;
                var isHealthy = failures < _options.MaxConsecutiveFailures;

                var updated = new CachedNode
                {
                    Id = node.Id,
                    Name = node.Name,
                    GrpcAddress = node.GrpcAddress,
                    IsHealthy = isHealthy,
                    TotalSpaceBytes = node.TotalSpaceBytes,
                    FreeSpaceBytes = node.FreeSpaceBytes,
                    ConsecutiveFailures = failures,
                    LastSeenAt = node.LastSeenAt
                };

                _nodes = _nodes.SetItem(nodeId, updated);
            }
        }
    }

    public void MarkNodeSuccess(string nodeId)
    {
        lock (_lock)
        {
            if (_nodes.TryGetValue(nodeId, out var node))
            {
                var updated = new CachedNode
                {
                    Id = node.Id,
                    Name = node.Name,
                    GrpcAddress = node.GrpcAddress,
                    IsHealthy = true,
                    TotalSpaceBytes = node.TotalSpaceBytes,
                    FreeSpaceBytes = node.FreeSpaceBytes,
                    ConsecutiveFailures = 0,
                    LastSeenAt = DateTime.UtcNow
                };

                _nodes = _nodes.SetItem(nodeId, updated);
            }
        }
    }
}
