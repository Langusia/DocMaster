namespace DocMaster.Api.Models;

public record RegisterNodeRequest(string Name, string GrpcAddress);

public record NodeResponse(
    string Id,
    string Name,
    string GrpcAddress,
    bool IsHealthy,
    long? TotalSpaceBytes,
    long? FreeSpaceBytes,
    long? UsedSpaceBytes,
    int? ObjectCount,
    int ConsecutiveFailures,
    DateTime? LastSeenAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record NodeListResponse(IReadOnlyList<NodeResponse> Nodes);
