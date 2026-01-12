using System.Security.Cryptography;
using Grpc.Core;

namespace DocMaster.Agent.Grpc.Services;

public class StorageServiceImpl : StorageService.StorageServiceBase
{
    private readonly IPathBuilder _pathBuilder;
    private readonly ILogger<StorageServiceImpl> _logger;
    private const int DownloadChunkSize = 65536; // 64KB

    public StorageServiceImpl(IPathBuilder pathBuilder, ILogger<StorageServiceImpl> logger)
    {
        _pathBuilder = pathBuilder;
        _logger = logger;
    }

    public override async Task<UploadResponse> Upload(
        IAsyncStreamReader<UploadRequest> requestStream,
        ServerCallContext context)
    {
        UploadMetadata? metadata = null;
        string? filePath = null;
        FileStream? fileStream = null;
        long bytesWritten = 0;

        try
        {
            using var sha256 = SHA256.Create();

            await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
            {
                if (request.PayloadCase == UploadRequest.PayloadOneofCase.Metadata)
                {
                    metadata = request.Metadata;

                    // Determine file path
                    filePath = metadata.IsReplicated
                        ? _pathBuilder.GetReplicaPath(metadata.ObjectId)
                        : _pathBuilder.GetShardPath(metadata.ObjectId, metadata.ChunkIndex, metadata.ShardIndex);

                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(filePath)!;
                    Directory.CreateDirectory(directory);

                    // Open file for writing
                    fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                }
                else if (request.PayloadCase == UploadRequest.PayloadOneofCase.Chunk)
                {
                    if (fileStream == null)
                    {
                        return new UploadResponse
                        {
                            Success = false,
                            Error = "Metadata must be sent before chunks"
                        };
                    }

                    var data = request.Chunk.ToByteArray();
                    await fileStream.WriteAsync(data, context.CancellationToken);
                    sha256.TransformBlock(data, 0, data.Length, null, 0);
                    bytesWritten += data.Length;
                }
            }

            if (fileStream != null)
            {
                await fileStream.FlushAsync(context.CancellationToken);
                fileStream.Close();
                fileStream.Dispose();
            }

            sha256.TransformFinalBlock([], 0, 0);
            var checksum = "sha256:" + Convert.ToHexString(sha256.Hash!).ToLowerInvariant();

            _logger.LogDebug(
                "Uploaded {Bytes} bytes to {Path}",
                bytesWritten, filePath);

            return new UploadResponse
            {
                Success = true,
                Checksum = checksum,
                BytesWritten = bytesWritten
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed");

            fileStream?.Dispose();

            // Clean up partial file
            if (filePath != null && File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            return new UploadResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public override async Task Download(
        DownloadRequest request,
        IServerStreamWriter<DownloadResponse> responseStream,
        ServerCallContext context)
    {
        var filePath = request.IsReplicated
            ? _pathBuilder.GetReplicaPath(request.ObjectId)
            : _pathBuilder.GetShardPath(request.ObjectId, request.ChunkIndex, request.ShardIndex);

        if (!File.Exists(filePath))
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"File not found: {filePath}"));
        }

        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer = new byte[DownloadChunkSize];

        int bytesRead;
        while ((bytesRead = await fileStream.ReadAsync(buffer, context.CancellationToken)) > 0)
        {
            var chunk = bytesRead == buffer.Length
                ? buffer
                : buffer.AsMemory(0, bytesRead).ToArray();

            await responseStream.WriteAsync(new DownloadResponse
            {
                Chunk = Google.Protobuf.ByteString.CopyFrom(chunk)
            }, context.CancellationToken);
        }

        _logger.LogDebug("Downloaded {Path}", filePath);
    }

    public override Task<DeleteResponse> Delete(DeleteRequest request, ServerCallContext context)
    {
        var directory = _pathBuilder.GetObjectDirectory(request.ObjectId);
        var shardsDeleted = 0;

        try
        {
            if (Directory.Exists(directory))
            {
                var files = Directory.GetFiles(directory);
                shardsDeleted = files.Length;

                Directory.Delete(directory, recursive: true);

                _logger.LogDebug(
                    "Deleted {Count} files from {Directory}",
                    shardsDeleted, directory);
            }

            return Task.FromResult(new DeleteResponse
            {
                Success = true,
                ShardsDeleted = shardsDeleted
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete failed for {Directory}", directory);

            return Task.FromResult(new DeleteResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    public override Task<ExistsResponse> Exists(ExistsRequest request, ServerCallContext context)
    {
        var filePath = request.IsReplicated
            ? _pathBuilder.GetReplicaPath(request.ObjectId)
            : _pathBuilder.GetShardPath(request.ObjectId, request.ChunkIndex, request.ShardIndex);

        return Task.FromResult(new ExistsResponse
        {
            Exists = File.Exists(filePath)
        });
    }

    public override Task<HealthResponse> GetHealth(HealthRequest request, ServerCallContext context)
    {
        try
        {
            var pathBuilder = _pathBuilder as PathBuilder;
            var basePath = Environment.GetEnvironmentVariable("Agent__BasePath") ?? "/data";

            // Get drive info
            var driveInfo = new DriveInfo(Path.GetPathRoot(basePath) ?? "/");

            // Count objects (directories at the leaf level)
            var objectCount = 0;
            if (Directory.Exists(basePath))
            {
                objectCount = CountObjects(basePath);
            }

            return Task.FromResult(new HealthResponse
            {
                Healthy = true,
                TotalSpaceBytes = driveInfo.TotalSize,
                FreeSpaceBytes = driveInfo.AvailableFreeSpace,
                UsedSpaceBytes = driveInfo.TotalSize - driveInfo.AvailableFreeSpace,
                ObjectCount = objectCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check encountered an error");

            return Task.FromResult(new HealthResponse
            {
                Healthy = false
            });
        }
    }

    private static int CountObjects(string basePath)
    {
        // Count leaf directories (those containing actual data files)
        var count = 0;

        try
        {
            foreach (var l1 in Directory.EnumerateDirectories(basePath))
            {
                foreach (var l2 in Directory.EnumerateDirectories(l1))
                {
                    count += Directory.EnumerateDirectories(l2).Count();
                }
            }
        }
        catch
        {
            // Ignore enumeration errors
        }

        return count;
    }
}
