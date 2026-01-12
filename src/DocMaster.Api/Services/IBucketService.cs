using DocMaster.Api.Models;

namespace DocMaster.Api.Services;

public interface IBucketService
{
    Task<Result<BucketResponse>> CreateAsync(string name, CancellationToken ct);
    Task<Result<BucketResponse>> GetAsync(string name, CancellationToken ct);
    Task<Result<IReadOnlyList<BucketResponse>>> ListAsync(CancellationToken ct);
    Task<Result<bool>> DeleteAsync(string name, CancellationToken ct);
}
