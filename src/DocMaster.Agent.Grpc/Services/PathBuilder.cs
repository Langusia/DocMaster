using DocMaster.Agent.Grpc.Configuration;
using Microsoft.Extensions.Options;

namespace DocMaster.Agent.Grpc.Services;

public class PathBuilder : IPathBuilder
{
    private readonly AgentOptions _options;

    public PathBuilder(IOptions<AgentOptions> options)
    {
        _options = options.Value;
    }

    public string GetShardPath(string objectId, int chunkIndex, int shardIndex)
    {
        var directory = GetObjectDirectory(objectId);
        return Path.Combine(directory, $"chunk_{chunkIndex}_shard_{shardIndex}");
    }

    public string GetReplicaPath(string objectId)
    {
        var directory = GetObjectDirectory(objectId);
        return Path.Combine(directory, "data");
    }

    public string GetObjectDirectory(string objectId)
    {
        // Build path with sharding directories
        // Example: /data/01/HQ/01HQX123ABCDEF456789/
        var pathParts = new List<string> { _options.BasePath };

        var offset = 0;
        for (var level = 0; level < _options.ShardLevelCount && offset + _options.ShardSymbolCount <= objectId.Length; level++)
        {
            pathParts.Add(objectId.Substring(offset, _options.ShardSymbolCount));
            offset += _options.ShardSymbolCount;
        }

        pathParts.Add(objectId);

        return Path.Combine(pathParts.ToArray());
    }
}
