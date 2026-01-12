using DocMaster.Agent.Grpc;
using DocMaster.Api.Configuration;
using DocMaster.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DocMaster.Api.Services;

public class NodeHealthService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IGrpcChannelFactory _channelFactory;
    private readonly INodeCache _nodeCache;
    private readonly NodeHealthOptions _options;
    private readonly ILogger<NodeHealthService> _logger;

    public NodeHealthService(
        IServiceProvider serviceProvider,
        IGrpcChannelFactory channelFactory,
        INodeCache nodeCache,
        IOptions<NodeHealthOptions> options,
        ILogger<NodeHealthService> logger)
    {
        _serviceProvider = serviceProvider;
        _channelFactory = channelFactory;
        _nodeCache = nodeCache;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial load of nodes from database
        await LoadNodesFromDatabaseAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllNodesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during node health check cycle");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
        }
    }

    private async Task LoadNodesFromDatabaseAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocMasterDbContext>();

        var nodes = await db.Nodes.AsNoTracking().ToListAsync(ct);

        var cachedNodes = nodes.Select(n => new CachedNode
        {
            Id = n.Id,
            Name = n.Name,
            GrpcAddress = n.GrpcAddress,
            IsHealthy = n.IsHealthy,
            TotalSpaceBytes = n.TotalSpaceBytes,
            FreeSpaceBytes = n.FreeSpaceBytes,
            ConsecutiveFailures = n.ConsecutiveFailures,
            LastSeenAt = n.LastSeenAt
        }).ToList();

        _nodeCache.UpdateNodes(cachedNodes);
        _logger.LogInformation("Loaded {Count} nodes from database", nodes.Count);
    }

    private async Task CheckAllNodesAsync(CancellationToken ct)
    {
        var nodes = _nodeCache.GetAllNodes();
        if (nodes.Count == 0)
            return;

        var tasks = nodes.Select(node => CheckNodeAsync(node, ct));
        await Task.WhenAll(tasks);
    }

    private async Task CheckNodeAsync(CachedNode node, CancellationToken ct)
    {
        try
        {
            var channel = _channelFactory.GetChannel(node.GrpcAddress);
            var client = new StorageService.StorageServiceClient(channel);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var response = await client.GetHealthAsync(new HealthRequest(), cancellationToken: cts.Token);

            await UpdateNodeHealthAsync(node.Id, true, response, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Health check failed for node {NodeId}", node.Id);
            await UpdateNodeHealthAsync(node.Id, false, null, ct);
        }
    }

    private async Task UpdateNodeHealthAsync(
        string nodeId,
        bool isHealthy,
        HealthResponse? health,
        CancellationToken ct)
    {
        var node = _nodeCache.GetNode(nodeId);
        if (node == null)
            return;

        var failures = isHealthy ? 0 : node.ConsecutiveFailures + 1;
        var healthy = isHealthy || failures < _options.MaxConsecutiveFailures;

        var updated = new CachedNode
        {
            Id = node.Id,
            Name = node.Name,
            GrpcAddress = node.GrpcAddress,
            IsHealthy = healthy,
            TotalSpaceBytes = health?.TotalSpaceBytes ?? node.TotalSpaceBytes,
            FreeSpaceBytes = health?.FreeSpaceBytes ?? node.FreeSpaceBytes,
            ConsecutiveFailures = failures,
            LastSeenAt = isHealthy ? DateTime.UtcNow : node.LastSeenAt
        };

        _nodeCache.UpdateNode(updated);

        // Persist to database
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocMasterDbContext>();

        var dbNode = await db.Nodes.FindAsync([nodeId], ct);
        if (dbNode != null)
        {
            dbNode.IsHealthy = healthy;
            dbNode.ConsecutiveFailures = failures;
            dbNode.UpdatedAt = DateTime.UtcNow;

            if (isHealthy && health != null)
            {
                dbNode.TotalSpaceBytes = health.TotalSpaceBytes;
                dbNode.FreeSpaceBytes = health.FreeSpaceBytes;
                dbNode.UsedSpaceBytes = health.UsedSpaceBytes;
                dbNode.ObjectCount = health.ObjectCount;
                dbNode.LastSeenAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
