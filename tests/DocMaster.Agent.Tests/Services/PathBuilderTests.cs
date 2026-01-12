using DocMaster.Agent.Grpc.Configuration;
using DocMaster.Agent.Grpc.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DocMaster.Agent.Tests.Services;

public class PathBuilderTests
{
    private readonly PathBuilder _pathBuilder;

    public PathBuilderTests()
    {
        var options = Options.Create(new AgentOptions
        {
            BasePath = "/data",
            ShardSymbolCount = 2,
            ShardLevelCount = 2
        });

        _pathBuilder = new PathBuilder(options);
    }

    [Fact]
    public void GetObjectDirectory_ReturnsCorrectPath()
    {
        var objectId = "01HQX123ABCDEF456789";

        var result = _pathBuilder.GetObjectDirectory(objectId);

        result.Should().Be("/data/01/HQ/01HQX123ABCDEF456789");
    }

    [Fact]
    public void GetShardPath_ReturnsCorrectPath()
    {
        var objectId = "01HQX123ABCDEF456789";

        var result = _pathBuilder.GetShardPath(objectId, 0, 3);

        result.Should().Be("/data/01/HQ/01HQX123ABCDEF456789/chunk_0_shard_3");
    }

    [Fact]
    public void GetReplicaPath_ReturnsCorrectPath()
    {
        var objectId = "01HQX123ABCDEF456789";

        var result = _pathBuilder.GetReplicaPath(objectId);

        result.Should().Be("/data/01/HQ/01HQX123ABCDEF456789/data");
    }

    [Fact]
    public void GetShardPath_MultipleChunks_ReturnsCorrectPaths()
    {
        var objectId = "01ABC987DEF654321XYZ";

        var result0 = _pathBuilder.GetShardPath(objectId, 0, 0);
        var result1 = _pathBuilder.GetShardPath(objectId, 1, 5);
        var result2 = _pathBuilder.GetShardPath(objectId, 2, 8);

        result0.Should().Be("/data/01/AB/01ABC987DEF654321XYZ/chunk_0_shard_0");
        result1.Should().Be("/data/01/AB/01ABC987DEF654321XYZ/chunk_1_shard_5");
        result2.Should().Be("/data/01/AB/01ABC987DEF654321XYZ/chunk_2_shard_8");
    }

    [Fact]
    public void GetObjectDirectory_ShortObjectId_HandlesGracefully()
    {
        var objectId = "AB"; // Very short ID

        var result = _pathBuilder.GetObjectDirectory(objectId);

        // Should only have one level since ID is too short
        result.Should().Be("/data/AB/AB");
    }

    [Fact]
    public void GetObjectDirectory_DifferentBasePath_UsesCorrectBase()
    {
        var options = Options.Create(new AgentOptions
        {
            BasePath = "/mnt/storage",
            ShardSymbolCount = 2,
            ShardLevelCount = 2
        });

        var pathBuilder = new PathBuilder(options);
        var objectId = "01HQX123ABCDEF456789";

        var result = pathBuilder.GetObjectDirectory(objectId);

        result.Should().Be("/mnt/storage/01/HQ/01HQX123ABCDEF456789");
    }
}
