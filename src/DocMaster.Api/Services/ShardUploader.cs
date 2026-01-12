using DocMaster.Agent.Grpc;
using DocMaster.Api.Configuration;
using Microsoft.Extensions.Options;

namespace DocMaster.Api.Services;

public class ShardUploader : IShardUploader
{
    private readonly IGrpcChannelFactory _channelFactory;
    private readonly INodeCache _nodeCache;
    private readonly INodeSelector _nodeSelector;
    private readonly UploadOptions _options;
    private readonly ILogger<ShardUploader> _logger;
    private const int StreamChunkSize = 65536; // 64KB

    public ShardUploader(
        IGrpcChannelFactory channelFactory,
        INodeCache nodeCache,
        INodeSelector nodeSelector,
        IOptions<UploadOptions> options,
        ILogger<ShardUploader> logger)
    {
        _channelFactory = channelFactory;
        _nodeCache = nodeCache;
        _nodeSelector = nodeSelector;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ShardUploadResult> UploadShardAsync(
        string objectId,
        int chunkIndex,
        int shardIndex,
        byte[] data,
        IReadOnlyList<CachedNode> preferredNodes,
        bool isReplicated,
        CancellationToken ct)
    {
        var triedNodes = new HashSet<string>();
        var nodeIndex = shardIndex; // Start with the preferred node for this shard

        for (var attempt = 0; attempt < _options.MaxNodeAttempts; attempt++)
        {
            CachedNode? node = null;

            // Try preferred nodes first
            if (nodeIndex < preferredNodes.Count)
            {
                node = preferredNodes[nodeIndex];
            }
            else
            {
                // Fall back to selecting a new node
                var selection = _nodeSelector.SelectSingleNode(triedNodes);
                if (!selection.Success || selection.Node == null)
                {
                    break;
                }

                node = selection.Node;
            }

            triedNodes.Add(node.Id);
            nodeIndex++;

            var result = await TryUploadToNodeAsync(objectId, chunkIndex, shardIndex, data, node, isReplicated, ct);
            if (result.Success)
            {
                _nodeCache.MarkNodeSuccess(node.Id);
                return result;
            }

            _logger.LogWarning(
                "Failed to upload shard to node {NodeId}: {Error}",
                node.Id, result.Error);
            _nodeCache.MarkNodeFailure(node.Id);
        }

        return new ShardUploadResult
        {
            Success = false,
            Error = $"All {_options.MaxNodeAttempts} upload attempts failed"
        };
    }

    public async Task<ShardUploadResult> UploadReplicaAsync(
        string objectId,
        byte[] data,
        CachedNode node,
        CancellationToken ct)
    {
        return await TryUploadToNodeAsync(objectId, 0, 0, data, node, true, ct);
    }

    private async Task<ShardUploadResult> TryUploadToNodeAsync(
        string objectId,
        int chunkIndex,
        int shardIndex,
        byte[] data,
        CachedNode node,
        bool isReplicated,
        CancellationToken ct)
    {
        try
        {
            var channel = _channelFactory.GetChannel(node.GrpcAddress);
            var client = new StorageService.StorageServiceClient(channel);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.GrpcTimeoutSeconds));

            using var call = client.Upload(cancellationToken: cts.Token);

            // Send metadata first
            await call.RequestStream.WriteAsync(new UploadRequest
            {
                Metadata = new UploadMetadata
                {
                    ObjectId = objectId,
                    ChunkIndex = chunkIndex,
                    ShardIndex = shardIndex,
                    IsReplicated = isReplicated
                }
            }, cts.Token);

            // Stream data in chunks
            var offset = 0;
            while (offset < data.Length)
            {
                var size = Math.Min(StreamChunkSize, data.Length - offset);
                var chunk = new byte[size];
                Array.Copy(data, offset, chunk, 0, size);

                await call.RequestStream.WriteAsync(new UploadRequest
                {
                    Chunk = Google.Protobuf.ByteString.CopyFrom(chunk)
                }, cts.Token);

                offset += size;
            }

            await call.RequestStream.CompleteAsync();
            var response = await call.ResponseAsync;

            if (!response.Success)
            {
                return new ShardUploadResult
                {
                    Success = false,
                    Error = response.Error,
                    NodeId = node.Id
                };
            }

            return new ShardUploadResult
            {
                Success = true,
                NodeId = node.Id,
                Checksum = response.Checksum,
                BytesWritten = response.BytesWritten
            };
        }
        catch (Exception ex)
        {
            return new ShardUploadResult
            {
                Success = false,
                Error = ex.Message,
                NodeId = node.Id
            };
        }
    }
}
