using DocMaster.Api.Configuration;
using DocMaster.Api.Data;
using DocMaster.Api.Data.Entities;
using DocMaster.Api.Models;
using DocMaster.ErasureCoding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DocMaster.Api.Services;

public class ObjectService : IObjectService
{
    private readonly DocMasterDbContext _db;
    private readonly IStreamProcessor _streamProcessor;
    private readonly IErasureCoder _erasureCoder;
    private readonly INodeSelector _nodeSelector;
    private readonly IShardUploader _shardUploader;
    private readonly IShardDownloader _shardDownloader;
    private readonly INodeCache _nodeCache;
    private readonly IGrpcChannelFactory _channelFactory;
    private readonly ErasureCodingOptions _ecOptions;
    private readonly MimeDetectionOptions _mimeOptions;
    private readonly ILogger<ObjectService> _logger;
    private const int ReplicaCount = 4;

    public ObjectService(
        DocMasterDbContext db,
        IStreamProcessor streamProcessor,
        IErasureCoder erasureCoder,
        INodeSelector nodeSelector,
        IShardUploader shardUploader,
        IShardDownloader shardDownloader,
        INodeCache nodeCache,
        IGrpcChannelFactory channelFactory,
        IOptions<ErasureCodingOptions> ecOptions,
        IOptions<MimeDetectionOptions> mimeOptions,
        ILogger<ObjectService> logger)
    {
        _db = db;
        _streamProcessor = streamProcessor;
        _erasureCoder = erasureCoder;
        _nodeSelector = nodeSelector;
        _shardUploader = shardUploader;
        _shardDownloader = shardDownloader;
        _nodeCache = nodeCache;
        _channelFactory = channelFactory;
        _ecOptions = ecOptions.Value;
        _mimeOptions = mimeOptions.Value;
        _logger = logger;
    }

    public async Task<Result<UploadResponse>> UploadAsync(
        string bucketName,
        string key,
        Stream data,
        string? contentType,
        string? originalFilename,
        CancellationToken ct)
    {
        // Validate bucket exists
        var bucket = await _db.Buckets.FirstOrDefaultAsync(b => b.Name == bucketName, ct);
        if (bucket == null)
        {
            return Result<UploadResponse>.Fail(ErrorCodes.BucketNotFound, $"Bucket '{bucketName}' not found");
        }

        // Validate key
        if (string.IsNullOrWhiteSpace(key) || key.Length > 1024 || key.Contains('\0'))
        {
            return Result<UploadResponse>.Fail(ErrorCodes.InvalidKey, "Invalid object key");
        }

        // Process the stream
        var processed = await _streamProcessor.ProcessAsync(data, key, contentType, originalFilename, ct);

        // Check file size
        if (processed.Size > _ecOptions.MaxFileSizeBytes)
        {
            return Result<UploadResponse>.Fail(
                ErrorCodes.ObjectTooLarge,
                $"File size {processed.Size} exceeds maximum {_ecOptions.MaxFileSizeBytes}");
        }

        // Security check
        if (_mimeOptions.RejectDangerousMismatches && processed.MimeResult.IsDangerousMismatch)
        {
            return Result<UploadResponse>.Fail(
                ErrorCodes.DangerousContentType,
                $"Dangerous content type mismatch detected. Claimed: {processed.MimeResult.ClaimedContentType}, Detected: {processed.MimeResult.DetectedContentType}");
        }

        // Check for existing object (overwrite)
        var existingObject = await _db.Objects
            .FirstOrDefaultAsync(o => o.BucketId == bucket.Id && o.Key == key, ct);

        if (existingObject != null)
        {
            await DeleteObjectInternalAsync(existingObject, ct);
        }

        // Determine storage strategy
        var useReplication = processed.Size <= _ecOptions.SmallObjectThreshold;
        var strategy = useReplication ? StorageStrategy.Replicated : StorageStrategy.ErasureCoded;

        // Create object record
        var objectId = Ulid.NewUlid().ToString();
        var storageObject = new StorageObject
        {
            Id = objectId,
            BucketId = bucket.Id,
            Key = key,
            SizeBytes = processed.Size,
            Checksum = processed.Checksum,
            ContentType = processed.MimeResult.ContentType,
            DetectedContentType = processed.MimeResult.DetectedContentType,
            ClaimedContentType = processed.MimeResult.ClaimedContentType,
            DetectedExtension = processed.MimeResult.DetectedExtension,
            OriginalFilename = originalFilename,
            StorageStrategy = strategy,
            ChunkCount = processed.Chunks.Count,
            Status = ObjectStatus.Uploading,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Objects.Add(storageObject);
        await _db.SaveChangesAsync(ct);

        try
        {
            if (useReplication)
            {
                var result = await UploadReplicatedAsync(objectId, processed, ct);
                if (!result.Success)
                {
                    return Result<UploadResponse>.Fail(result.ErrorCode!, result.ErrorMessage!);
                }
            }
            else
            {
                var result = await UploadErasureCodedAsync(objectId, processed, ct);
                if (!result.Success)
                {
                    return Result<UploadResponse>.Fail(result.ErrorCode!, result.ErrorMessage!);
                }
            }

            // Mark as healthy
            storageObject.Status = ObjectStatus.Healthy;
            await _db.SaveChangesAsync(ct);

            return Result<UploadResponse>.Ok(new UploadResponse(
                objectId,
                bucketName,
                key,
                processed.Size,
                processed.Checksum,
                processed.MimeResult.ContentType,
                strategy.ToString().ToLowerInvariant(),
                storageObject.CreatedAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed for object {ObjectId}", objectId);
            storageObject.Status = ObjectStatus.Failed;
            await _db.SaveChangesAsync(ct);
            throw;
        }
    }

    private async Task<Result<bool>> UploadReplicatedAsync(string objectId, ProcessedFile processed, CancellationToken ct)
    {
        var selection = _nodeSelector.SelectForReplicatedWrite(ReplicaCount);
        if (!selection.Success)
        {
            return Result<bool>.Fail(ErrorCodes.InsufficientNodes, selection.Error!);
        }

        // All chunks combined for replicated objects
        var fullData = processed.Chunks.SelectMany(c => c.Data).ToArray();

        var uploadTasks = selection.SelectedNodes.Select(async node =>
        {
            var result = await _shardUploader.UploadReplicaAsync(objectId, fullData, node, ct);
            if (result.Success)
            {
                var replica = new Replica
                {
                    Id = Ulid.NewUlid().ToString(),
                    ObjectId = objectId,
                    NodeId = node.Id,
                    Checksum = result.Checksum!,
                    Status = ReplicaStatus.Healthy
                };

                return replica;
            }

            return null;
        });

        var replicas = await Task.WhenAll(uploadTasks);
        var successfulReplicas = replicas.Where(r => r != null).ToList();

        if (successfulReplicas.Count < 1)
        {
            return Result<bool>.Fail(ErrorCodes.UploadFailed, "Failed to create any replicas");
        }

        _db.Replicas.AddRange(successfulReplicas!);
        await _db.SaveChangesAsync(ct);

        return Result<bool>.Ok(true);
    }

    private async Task<Result<bool>> UploadErasureCodedAsync(string objectId, ProcessedFile processed, CancellationToken ct)
    {
        var totalShards = _ecOptions.TotalShards;
        var selection = _nodeSelector.SelectForErasureCodedWrite(totalShards);
        if (!selection.Success)
        {
            return Result<bool>.Fail(ErrorCodes.InsufficientNodes, selection.Error!);
        }


        foreach (var chunk in processed.Chunks)
        {
            // Create chunk record
            var chunkEntity = new Chunk
            {
                Id = Ulid.NewUlid().ToString(),
                ObjectId = objectId,
                ChunkIndex = chunk.Index,
                SizeBytes = chunk.Data.Length,
                Checksum = chunk.Checksum
            };

            _db.Chunks.Add(chunkEntity);
            await _db.SaveChangesAsync(ct);

            // Encode to shards
            var shards = _erasureCoder.Encode(chunk.Data);

            // Upload shards in parallel
            var uploadTasks = shards.Select(async (shardData, shardIndex) =>
            {
                var result = await _shardUploader.UploadShardAsync(
                    objectId,
                    chunk.Index,
                    shardIndex,
                    shardData,
                    selection.SelectedNodes,
                    false,
                    ct);

                if (result.Success)
                {
                    return new Shard
                    {
                        Id = Ulid.NewUlid().ToString(),
                        ChunkId = chunkEntity.Id,
                        ShardIndex = shardIndex,
                        NodeId = result.NodeId!,
                        SizeBytes = shardData.Length,
                        Checksum = result.Checksum!,
                        Status = ShardStatus.Healthy
                    };
                }

                return null;
            });

            var shardResults = await Task.WhenAll(uploadTasks);
            var successfulShards = shardResults.Where(s => s != null).ToList();

            if (successfulShards.Count < _ecOptions.DataShards)
            {
                return Result<bool>.Fail(
                    ErrorCodes.UploadFailed,
                    $"Only {successfulShards.Count} shards uploaded, need at least {_ecOptions.DataShards}");
            }

            _db.Shards.AddRange(successfulShards!);
            await _db.SaveChangesAsync(ct);
        }

        return Result<bool>.Ok(true);
    }

    public async Task<Result<Stream>> DownloadAsync(string bucketName, string key, CancellationToken ct)
    {
        var obj = await GetObjectAsync(bucketName, key, ct);
        if (obj == null)
        {
            return Result<Stream>.Fail(ErrorCodes.ObjectNotFound, $"Object '{key}' not found in bucket '{bucketName}'");
        }

        if (obj.StorageStrategy == StorageStrategy.Replicated)
        {
            return await DownloadReplicatedAsync(obj, ct);
        }
        else
        {
            return await DownloadErasureCodedAsync(obj, ct);
        }
    }

    private async Task<Result<Stream>> DownloadReplicatedAsync(StorageObject obj, CancellationToken ct)
    {
        var replicas = await _db.Replicas
            .Where(r => r.ObjectId == obj.Id && r.Status == ReplicaStatus.Healthy)
            .ToListAsync(ct);

        foreach (var replica in replicas)
        {
            var node = _nodeCache.GetNode(replica.NodeId);
            if (node == null || !node.IsHealthy)
                continue;

            var result = await _shardDownloader.DownloadShardAsync(
                obj.Id, 0, 0, replica.NodeId, true, ct);

            if (result.Success && result.Data != null)
            {
                return Result<Stream>.Ok(new MemoryStream(result.Data));
            }
        }

        return Result<Stream>.Fail(ErrorCodes.DownloadFailed, "No healthy replicas available");
    }

    private async Task<Result<Stream>> DownloadErasureCodedAsync(StorageObject obj, CancellationToken ct)
    {
        var chunks = await _db.Chunks
            .Where(c => c.ObjectId == obj.Id)
            .OrderBy(c => c.ChunkIndex)
            .Include(c => c.Shards)
            .ToListAsync(ct);

        var outputStream = new MemoryStream();

        foreach (var chunk in chunks)
        {
            var healthyShards = chunk.Shards
                .Where(s => s.Status == ShardStatus.Healthy)
                .ToList();

            // Download data shards
            var downloadTasks = healthyShards
                .Take(_ecOptions.DataShards + 2) // Get a few extra in case some fail
                .Select(async shard =>
                {
                    var result = await _shardDownloader.DownloadShardAsync(
                        obj.Id, chunk.ChunkIndex, shard.ShardIndex, shard.NodeId, false, ct);

                    return result.Success ? (shard.ShardIndex, result.Data) : (shard.ShardIndex, null);
                });

            var results = await Task.WhenAll(downloadTasks);
            var validShards = results
                .Where(r => r.Data != null)
                .ToDictionary(r => r.ShardIndex, r => r.Data!);

            if (validShards.Count < _ecOptions.DataShards)
            {
                return Result<Stream>.Fail(
                    ErrorCodes.DownloadFailed,
                    $"Only {validShards.Count} shards available, need {_ecOptions.DataShards}");
            }

            // Decode chunk - build shard matrix and present array
            var totalShards = _ecOptions.DataShards + _ecOptions.ParityShards;
            var shardMatrix = new byte[totalShards][];
            var presentShards = new bool[totalShards];

            foreach (var (shardIndex, data) in validShards)
            {
                shardMatrix[shardIndex] = data;
                presentShards[shardIndex] = true;
            }

            var chunkData = _erasureCoder.Decode(shardMatrix, presentShards, (int)chunk.SizeBytes);
            await outputStream.WriteAsync(chunkData, ct);
        }

        outputStream.Position = 0;
        return Result<Stream>.Ok(outputStream);
    }

    public async Task<Result<ObjectInfo>> GetInfoAsync(string bucketName, string key, CancellationToken ct)
    {
        var obj = await GetObjectAsync(bucketName, key, ct);
        if (obj == null)
        {
            return Result<ObjectInfo>.Fail(ErrorCodes.ObjectNotFound, $"Object '{key}' not found in bucket '{bucketName}'");
        }

        return Result<ObjectInfo>.Ok(MapToObjectInfo(obj, bucketName));
    }

    public async Task<Result<bool>> DeleteAsync(string bucketName, string key, CancellationToken ct)
    {
        var obj = await GetObjectAsync(bucketName, key, ct);
        if (obj == null)
        {
            return Result<bool>.Fail(ErrorCodes.ObjectNotFound, $"Object '{key}' not found in bucket '{bucketName}'");
        }

        await DeleteObjectInternalAsync(obj, ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<IReadOnlyList<ObjectInfo>>> ListAsync(string bucketName, CancellationToken ct)
    {
        var bucket = await _db.Buckets.FirstOrDefaultAsync(b => b.Name == bucketName, ct);
        if (bucket == null)
        {
            return Result<IReadOnlyList<ObjectInfo>>.Fail(ErrorCodes.BucketNotFound, $"Bucket '{bucketName}' not found");
        }

        var objects = await _db.Objects
            .Where(o => o.BucketId == bucket.Id)
            .OrderBy(o => o.Key)
            .ToListAsync(ct);

        var result = objects.Select(o => MapToObjectInfo(o, bucketName)).ToList();
        return Result<IReadOnlyList<ObjectInfo>>.Ok(result);
    }

    private async Task<StorageObject?> GetObjectAsync(string bucketName, string key, CancellationToken ct)
    {
        return await _db.Objects
            .Include(o => o.Bucket)
            .FirstOrDefaultAsync(o => o.Bucket.Name == bucketName && o.Key == key, ct);
    }

    private async Task DeleteObjectInternalAsync(StorageObject obj, CancellationToken ct)
    {
        // Delete from storage nodes
        if (obj.StorageStrategy == StorageStrategy.Replicated)
        {
            var replicas = await _db.Replicas.Where(r => r.ObjectId == obj.Id).ToListAsync(ct);
            foreach (var replica in replicas)
            {
                try
                {
                    var node = _nodeCache.GetNode(replica.NodeId);
                    if (node != null)
                    {
                        await DeleteFromNodeAsync(obj.Id, node, ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete replica from node {NodeId}", replica.NodeId);
                }
            }
        }
        else
        {
            var shards = await _db.Shards
                .Include(s => s.Chunk)
                .Where(s => s.Chunk.ObjectId == obj.Id)
                .ToListAsync(ct);

            var nodeIds = shards.Select(s => s.NodeId).Distinct();
            foreach (var nodeId in nodeIds)
            {
                try
                {
                    var node = _nodeCache.GetNode(nodeId);
                    if (node != null)
                    {
                        await DeleteFromNodeAsync(obj.Id, node, ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete shards from node {NodeId}", nodeId);
                }
            }
        }

        // Delete database records
        _db.Objects.Remove(obj);
        await _db.SaveChangesAsync(ct);
    }

    private async Task DeleteFromNodeAsync(string objectId, CachedNode node, CancellationToken ct)
    {
        var channel = _channelFactory.GetChannel(node.GrpcAddress);
        var client = new DocMaster.Agent.Grpc.StorageService.StorageServiceClient(channel);

        var response = await client.DeleteAsync(new DocMaster.Agent.Grpc.DeleteRequest
        {
            ObjectId = objectId
        }, cancellationToken: ct);

        if (!response.Success)
        {
            _logger.LogWarning("Failed to delete object {ObjectId} from node {NodeId}: {Error}",
                objectId, node.Id, response.Error);
        }
        else
        {
            _logger.LogDebug("Deleted {ShardsDeleted} shards for object {ObjectId} from node {NodeId}",
                response.ShardsDeleted, objectId, node.Id);
        }
    }

    private static ObjectInfo MapToObjectInfo(StorageObject obj, string bucketName)
    {
        return new ObjectInfo(
            obj.Id,
            bucketName,
            obj.Key,
            obj.SizeBytes,
            obj.Checksum,
            obj.ContentType,
            obj.DetectedContentType,
            obj.DetectedExtension,
            obj.OriginalFilename,
            obj.StorageStrategy == StorageStrategy.Replicated ? "replicated" : "erasure_coded",
            obj.Status.ToString().ToLowerInvariant(),
            obj.ChunkCount,
            obj.CreatedAt,
            obj.UpdatedAt);
    }
}
