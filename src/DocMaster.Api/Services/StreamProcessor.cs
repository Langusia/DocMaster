using System.Security.Cryptography;
using DocMaster.Api.Configuration;
using Microsoft.Extensions.Options;

namespace DocMaster.Api.Services;

public class StreamProcessor : IStreamProcessor
{
    private readonly IMimeDetector _mimeDetector;
    private readonly ErasureCodingOptions _options;
    private const int HeaderSize = 512;

    public StreamProcessor(IMimeDetector mimeDetector, IOptions<ErasureCodingOptions> options)
    {
        _mimeDetector = mimeDetector;
        _options = options.Value;
    }

    public async Task<ProcessedFile> ProcessAsync(
        Stream input,
        string key,
        string? claimedContentType,
        string? originalFilename,
        CancellationToken ct)
    {
        using var sha256 = SHA256.Create();
        var chunks = new List<ChunkData>();
        var totalSize = 0L;
        var chunkIndex = 0;

        byte[]? headerBytes = null;
        byte[]? firstChunkForMime = null;

        var currentChunk = new MemoryStream();
        var buffer = new byte[81920]; // 80KB read buffer

        while (true)
        {
            var bytesRead = await input.ReadAsync(buffer, ct);
            if (bytesRead == 0)
                break;

            // Capture header for MIME detection
            if (headerBytes == null)
            {
                headerBytes = new byte[Math.Min(HeaderSize, bytesRead)];
                Array.Copy(buffer, headerBytes, headerBytes.Length);
            }

            // Update overall checksum
            sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            totalSize += bytesRead;

            // Write to current chunk
            await currentChunk.WriteAsync(buffer.AsMemory(0, bytesRead), ct);

            // Check if chunk is full
            while (currentChunk.Length >= _options.ChunkSizeBytes)
            {
                var chunkData = ExtractChunk(currentChunk, _options.ChunkSizeBytes);

                // Capture first chunk for ZIP inspection
                if (firstChunkForMime == null)
                {
                    firstChunkForMime = chunkData;
                }

                var chunkChecksum = ComputeChecksum(chunkData);
                chunks.Add(new ChunkData
                {
                    Index = chunkIndex++,
                    Data = chunkData,
                    Checksum = chunkChecksum
                });
            }
        }

        // Handle remaining data as final chunk
        if (currentChunk.Length > 0)
        {
            var chunkData = currentChunk.ToArray();

            // Capture first chunk for ZIP inspection if we haven't yet
            if (firstChunkForMime == null)
            {
                firstChunkForMime = chunkData;
            }

            var chunkChecksum = ComputeChecksum(chunkData);
            chunks.Add(new ChunkData
            {
                Index = chunkIndex,
                Data = chunkData,
                Checksum = chunkChecksum
            });
        }

        // Finalize overall checksum
        sha256.TransformFinalBlock([], 0, 0);
        var overallChecksum = "sha256:" + Convert.ToHexString(sha256.Hash!).ToLowerInvariant();

        // Determine filename hint
        var filenameHint = originalFilename ?? key;

        // Perform MIME detection
        var mimeResult = _mimeDetector.Detect(
            headerBytes ?? [],
            firstChunkForMime,
            claimedContentType,
            filenameHint);

        return new ProcessedFile
        {
            Checksum = overallChecksum,
            Size = totalSize,
            Chunks = chunks,
            MimeResult = mimeResult
        };
    }

    private static byte[] ExtractChunk(MemoryStream stream, int chunkSize)
    {
        var fullBuffer = stream.ToArray();
        var chunk = new byte[chunkSize];
        Array.Copy(fullBuffer, chunk, chunkSize);

        // Keep remaining data in stream
        stream.SetLength(0);
        if (fullBuffer.Length > chunkSize)
        {
            stream.Write(fullBuffer, chunkSize, fullBuffer.Length - chunkSize);
        }

        return chunk;
    }

    private static string ComputeChecksum(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
