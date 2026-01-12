using DocMaster.Agent.Grpc;
using DocMaster.Api.Configuration;
using Microsoft.Extensions.Options;

namespace DocMaster.Api.Services;

public class ShardDownloader : IShardDownloader
{
    private readonly IGrpcChannelFactory _channelFactory;
    private readonly INodeCache _nodeCache;
    private readonly UploadOptions _options;
    private readonly ILogger<ShardDownloader> _logger;

    public ShardDownloader(
        IGrpcChannelFactory channelFactory,
        INodeCache nodeCache,
        IOptions<UploadOptions> options,
        ILogger<ShardDownloader> logger)
    {
        _channelFactory = channelFactory;
        _nodeCache = nodeCache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ShardDownloadResult> DownloadShardAsync(
        string objectId,
        int chunkIndex,
        int shardIndex,
        string nodeId,
        bool isReplicated,
        CancellationToken ct)
    {
        var node = _nodeCache.GetNode(nodeId);
        if (node == null)
        {
            return new ShardDownloadResult
            {
                Success = false,
                Error = $"Node {nodeId} not found in cache"
            };
        }

        try
        {
            var channel = _channelFactory.GetChannel(node.GrpcAddress);
            var client = new StorageService.StorageServiceClient(channel);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.GrpcTimeoutSeconds));

            var request = new DownloadRequest
            {
                ObjectId = objectId,
                ChunkIndex = chunkIndex,
                ShardIndex = shardIndex,
                IsReplicated = isReplicated
            };

            using var call = client.Download(request, cancellationToken: cts.Token);
            using var ms = new MemoryStream();

            await foreach (var response in call.ResponseStream.ReadAllAsync(cts.Token))
            {
                await ms.WriteAsync(response.Chunk.Memory, cts.Token);
            }

            _nodeCache.MarkNodeSuccess(nodeId);

            return new ShardDownloadResult
            {
                Success = true,
                Data = ms.ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download shard from node {NodeId}", nodeId);
            _nodeCache.MarkNodeFailure(nodeId);

            return new ShardDownloadResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
}
