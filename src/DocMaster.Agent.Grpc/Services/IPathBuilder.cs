namespace DocMaster.Agent.Grpc.Services;

public interface IPathBuilder
{
    string GetShardPath(string objectId, int chunkIndex, int shardIndex);
    string GetReplicaPath(string objectId);
    string GetObjectDirectory(string objectId);
}
