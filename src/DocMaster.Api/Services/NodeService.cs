using DocMaster.Api.Data;
using DocMaster.Api.Data.Entities;
using DocMaster.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DocMaster.Api.Services;

public class NodeService : INodeService
{
    private readonly DocMasterDbContext _db;
    private readonly INodeCache _nodeCache;

    public NodeService(DocMasterDbContext db, INodeCache nodeCache)
    {
        _db = db;
        _nodeCache = nodeCache;
    }

    public async Task<Result<NodeResponse>> RegisterAsync(string name, string grpcAddress, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255)
        {
            return Result<NodeResponse>.Fail(ErrorCodes.InvalidKey, "Invalid node name");
        }

        if (string.IsNullOrWhiteSpace(grpcAddress) || grpcAddress.Length > 255)
        {
            return Result<NodeResponse>.Fail(ErrorCodes.InvalidKey, "Invalid gRPC address");
        }

        var node = new Node
        {
            Id = Ulid.NewUlid().ToString(),
            Name = name,
            GrpcAddress = grpcAddress,
            IsHealthy = true,
            ConsecutiveFailures = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Nodes.Add(node);
        await _db.SaveChangesAsync(ct);

        // Update cache
        _nodeCache.UpdateNode(new CachedNode
        {
            Id = node.Id,
            Name = node.Name,
            GrpcAddress = node.GrpcAddress,
            IsHealthy = node.IsHealthy,
            ConsecutiveFailures = node.ConsecutiveFailures
        });

        return Result<NodeResponse>.Ok(MapToResponse(node));
    }

    public async Task<Result<NodeResponse>> GetAsync(string id, CancellationToken ct)
    {
        var node = await _db.Nodes.FindAsync([id], ct);
        if (node == null)
        {
            return Result<NodeResponse>.Fail(ErrorCodes.NodeNotFound, $"Node '{id}' not found");
        }

        return Result<NodeResponse>.Ok(MapToResponse(node));
    }

    public async Task<Result<IReadOnlyList<NodeResponse>>> ListAsync(CancellationToken ct)
    {
        var nodes = await _db.Nodes
            .OrderBy(n => n.Name)
            .ToListAsync(ct);

        var result = nodes.Select(MapToResponse).ToList();
        return Result<IReadOnlyList<NodeResponse>>.Ok(result);
    }

    public async Task<Result<bool>> UnregisterAsync(string id, CancellationToken ct)
    {
        var node = await _db.Nodes.FindAsync([id], ct);
        if (node == null)
        {
            return Result<bool>.Fail(ErrorCodes.NodeNotFound, $"Node '{id}' not found");
        }

        // Check if node has data
        var hasShards = await _db.Shards.AnyAsync(s => s.NodeId == id, ct);
        var hasReplicas = await _db.Replicas.AnyAsync(r => r.NodeId == id, ct);

        if (hasShards || hasReplicas)
        {
            // Mark as unhealthy instead of deleting
            node.IsHealthy = false;
            node.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return Result<bool>.Fail(
                ErrorCodes.InternalError,
                "Node has data stored on it. Node marked as unhealthy but not deleted.");
        }

        _db.Nodes.Remove(node);
        await _db.SaveChangesAsync(ct);

        return Result<bool>.Ok(true);
    }

    private static NodeResponse MapToResponse(Node node)
    {
        return new NodeResponse(
            node.Id,
            node.Name,
            node.GrpcAddress,
            node.IsHealthy,
            node.TotalSpaceBytes,
            node.FreeSpaceBytes,
            node.UsedSpaceBytes,
            node.ObjectCount,
            node.ConsecutiveFailures,
            node.LastSeenAt,
            node.CreatedAt,
            node.UpdatedAt);
    }
}
