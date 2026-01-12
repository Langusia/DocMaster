using DocMaster.Api.Data;
using DocMaster.Api.Data.Entities;
using DocMaster.Api.Models;
using DocMaster.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocMaster.Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly DocMasterDbContext _db;
    private readonly INodeCache _nodeCache;

    public AdminController(DocMasterDbContext db, INodeCache nodeCache)
    {
        _db = db;
        _nodeCache = nodeCache;
    }

    [HttpGet("objects/{id}/status")]
    public async Task<IActionResult> GetObjectStatus(string id, CancellationToken ct)
    {
        var obj = await _db.Objects
            .Include(o => o.Bucket)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (obj == null)
        {
            return NotFound(new ErrorResponse(new ErrorDetail(
                ErrorCodes.ObjectNotFound,
                $"Object '{id}' not found",
                HttpContext.TraceIdentifier)));
        }

        var response = await BuildObjectStatusResponseAsync(obj, ct);
        return Ok(response);
    }

    [HttpPost("objects/{id}/heal")]
    public async Task<IActionResult> HealObject(string id, CancellationToken ct)
    {
        var obj = await _db.Objects.FindAsync([id], ct);
        if (obj == null)
        {
            return NotFound(new ErrorResponse(new ErrorDetail(
                ErrorCodes.ObjectNotFound,
                $"Object '{id}' not found",
                HttpContext.TraceIdentifier)));
        }

        // TODO: Implement actual healing logic
        // For now, return a placeholder response
        return Ok(new HealResponse(id, true, null, 0, 0));
    }

    private async Task<ObjectStatusResponse> BuildObjectStatusResponseAsync(StorageObject obj, CancellationToken ct)
    {
        List<ShardInfo>? shardInfos = null;
        List<ReplicaInfo>? replicaInfos = null;
        var totalShards = 0;
        var healthyShards = 0;
        var missingShards = 0;
        var corruptedShards = 0;

        if (obj.StorageStrategy == StorageStrategy.ErasureCoded)
        {
            var shards = await _db.Shards
                .Include(s => s.Chunk)
                .Include(s => s.Node)
                .Where(s => s.Chunk.ObjectId == obj.Id)
                .ToListAsync(ct);

            totalShards = shards.Count;
            healthyShards = shards.Count(s => s.Status == ShardStatus.Healthy);
            missingShards = shards.Count(s => s.Status == ShardStatus.Missing);
            corruptedShards = shards.Count(s => s.Status == ShardStatus.Corrupted);

            shardInfos = shards.Select(s => new ShardInfo(
                s.Chunk.ChunkIndex,
                s.ShardIndex,
                s.NodeId,
                s.Node.Name,
                s.Status.ToString().ToLowerInvariant()
            )).ToList();
        }
        else
        {
            var replicas = await _db.Replicas
                .Include(r => r.Node)
                .Where(r => r.ObjectId == obj.Id)
                .ToListAsync(ct);

            totalShards = replicas.Count;
            healthyShards = replicas.Count(r => r.Status == ReplicaStatus.Healthy);
            missingShards = replicas.Count(r => r.Status == ReplicaStatus.Missing);
            corruptedShards = replicas.Count(r => r.Status == ReplicaStatus.Corrupted);

            replicaInfos = replicas.Select(r => new ReplicaInfo(
                r.NodeId,
                r.Node.Name,
                r.Status.ToString().ToLowerInvariant()
            )).ToList();
        }

        return new ObjectStatusResponse(
            obj.Id,
            obj.Bucket.Name,
            obj.Key,
            obj.Status.ToString().ToLowerInvariant(),
            obj.StorageStrategy == StorageStrategy.Replicated ? "replicated" : "erasure_coded",
            totalShards,
            healthyShards,
            missingShards,
            corruptedShards,
            shardInfos,
            replicaInfos);
    }
}
