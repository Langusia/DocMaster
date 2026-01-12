using DocMaster.Api.Models;

namespace DocMaster.Api.Services;

public interface IObjectService
{
    Task<Result<UploadResponse>> UploadAsync(
        string bucket,
        string key,
        Stream data,
        string? contentType,
        string? originalFilename,
        CancellationToken ct);

    Task<Result<Stream>> DownloadAsync(string bucket, string key, CancellationToken ct);
    Task<Result<ObjectInfo>> GetInfoAsync(string bucket, string key, CancellationToken ct);
    Task<Result<bool>> DeleteAsync(string bucket, string key, CancellationToken ct);
    Task<Result<IReadOnlyList<ObjectInfo>>> ListAsync(string bucket, CancellationToken ct);
}

public class Result<T>
{
    public bool Success { get; init; }
    public T? Value { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static Result<T> Ok(T value) => new() { Success = true, Value = value };
    public static Result<T> Fail(string code, string message) => new() { Success = false, ErrorCode = code, ErrorMessage = message };
}
