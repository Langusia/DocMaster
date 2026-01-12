using DocMaster.Api.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace DocMaster.Api.Tests.Services;

public class NodeSelectorTests
{
    [Fact]
    public void SelectForErasureCodedWrite_EnoughNodes_ReturnsSuccess()
    {
        var nodes = CreateHealthyNodes(12);
        var cache = new Mock<INodeCache>();
        cache.Setup(c => c.GetHealthyNodes()).Returns(nodes);

        var selector = new NodeSelector(cache.Object);

        var result = selector.SelectForErasureCodedWrite(9);

        result.Success.Should().BeTrue();
        result.SelectedNodes.Should().HaveCount(9);
        result.RequestedCount.Should().Be(9);
        result.AvailableCount.Should().Be(12);
    }

    [Fact]
    public void SelectForErasureCodedWrite_NotEnoughNodes_ReturnsFail()
    {
        var nodes = CreateHealthyNodes(6);
        var cache = new Mock<INodeCache>();
        cache.Setup(c => c.GetHealthyNodes()).Returns(nodes);

        var selector = new NodeSelector(cache.Object);

        var result = selector.SelectForErasureCodedWrite(9);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Not enough healthy nodes");
        result.RequestedCount.Should().Be(9);
        result.AvailableCount.Should().Be(6);
    }

    [Fact]
    public void SelectForReplicatedWrite_EnoughNodes_ReturnsSuccess()
    {
        var nodes = CreateHealthyNodes(6);
        var cache = new Mock<INodeCache>();
        cache.Setup(c => c.GetHealthyNodes()).Returns(nodes);

        var selector = new NodeSelector(cache.Object);

        var result = selector.SelectForReplicatedWrite(4);

        result.Success.Should().BeTrue();
        result.SelectedNodes.Should().HaveCount(4);
    }

    [Fact]
    public void SelectSingleNode_WithExclusions_ExcludesNodes()
    {
        var nodes = CreateHealthyNodes(3);
        var cache = new Mock<INodeCache>();
        cache.Setup(c => c.GetHealthyNodes()).Returns(nodes);

        var selector = new NodeSelector(cache.Object);
        var excludeIds = new HashSet<string> { nodes[0].Id, nodes[1].Id };

        var result = selector.SelectSingleNode(excludeIds);

        result.Success.Should().BeTrue();
        result.Node!.Id.Should().Be(nodes[2].Id);
    }

    [Fact]
    public void SelectSingleNode_NoHealthyNodes_ReturnsFail()
    {
        var cache = new Mock<INodeCache>();
        cache.Setup(c => c.GetHealthyNodes()).Returns(new List<CachedNode>());

        var selector = new NodeSelector(cache.Object);

        var result = selector.SelectSingleNode(null);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("No healthy nodes available");
    }

    [Fact]
    public void SelectForErasureCodedWrite_PrefersNodesWithMoreFreeSpace()
    {
        var nodes = new List<CachedNode>
        {
            new() { Id = "1", Name = "node1", GrpcAddress = "node1:5001", IsHealthy = true, TotalSpaceBytes = 100, FreeSpaceBytes = 10 },
            new() { Id = "2", Name = "node2", GrpcAddress = "node2:5001", IsHealthy = true, TotalSpaceBytes = 100, FreeSpaceBytes = 90 },
            new() { Id = "3", Name = "node3", GrpcAddress = "node3:5001", IsHealthy = true, TotalSpaceBytes = 100, FreeSpaceBytes = 50 }
        };

        var cache = new Mock<INodeCache>();
        cache.Setup(c => c.GetHealthyNodes()).Returns(nodes);

        var selector = new NodeSelector(cache.Object);

        // Run multiple times to account for randomness
        var selectedCounts = new Dictionary<string, int> { { "1", 0 }, { "2", 0 }, { "3", 0 } };
        for (var i = 0; i < 100; i++)
        {
            var result = selector.SelectForErasureCodedWrite(1);
            selectedCounts[result.SelectedNodes[0].Id]++;
        }

        // Node 2 (90% free) should be selected most often
        selectedCounts["2"].Should().BeGreaterThan(selectedCounts["1"]);
    }

    private static List<CachedNode> CreateHealthyNodes(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new CachedNode
            {
                Id = $"node-{i}",
                Name = $"node-{i}",
                GrpcAddress = $"node-{i}:5001",
                IsHealthy = true,
                TotalSpaceBytes = 1000000000,
                FreeSpaceBytes = 500000000
            })
            .ToList();
    }
}
