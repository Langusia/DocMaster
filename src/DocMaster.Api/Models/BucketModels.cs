namespace DocMaster.Api.Models;

public record CreateBucketRequest(string Name);

public record BucketResponse(
    string Id,
    string Name,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record BucketListResponse(IReadOnlyList<BucketResponse> Buckets);
