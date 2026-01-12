using DocMaster.Api.Models;

namespace DocMaster.Api.Services;

public interface INodeService
{
    Task<Result<NodeResponse>> RegisterAsync(string name, string grpcAddress, CancellationToken ct);
    Task<Result<NodeResponse>> GetAsync(string id, CancellationToken ct);
    Task<Result<IReadOnlyList<NodeResponse>>> ListAsync(CancellationToken ct);
    Task<Result<bool>> UnregisterAsync(string id, CancellationToken ct);
}
