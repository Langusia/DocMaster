using System.Text.RegularExpressions;
using DocMaster.Api.Data;
using DocMaster.Api.Data.Entities;
using DocMaster.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DocMaster.Api.Services;

public partial class BucketService : IBucketService
{
    private readonly DocMasterDbContext _db;

    public BucketService(DocMasterDbContext db)
    {
        _db = db;
    }

    public async Task<Result<BucketResponse>> CreateAsync(string name, CancellationToken ct)
    {
        // Validate name
        if (!IsValidBucketName(name))
        {
            return Result<BucketResponse>.Fail(
                ErrorCodes.InvalidBucketName,
                "Bucket name must be 3-63 characters, lowercase alphanumeric + hyphens, no consecutive hyphens");
        }

        // Check for existing
        var exists = await _db.Buckets.AnyAsync(b => b.Name == name, ct);
        if (exists)
        {
            return Result<BucketResponse>.Fail(ErrorCodes.BucketAlreadyExists, $"Bucket '{name}' already exists");
        }

        var bucket = new Bucket
        {
            Id = Ulid.NewUlid().ToString(),
            Name = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Buckets.Add(bucket);
        await _db.SaveChangesAsync(ct);

        return Result<BucketResponse>.Ok(MapToResponse(bucket));
    }

    public async Task<Result<BucketResponse>> GetAsync(string name, CancellationToken ct)
    {
        var bucket = await _db.Buckets.FirstOrDefaultAsync(b => b.Name == name, ct);
        if (bucket == null)
        {
            return Result<BucketResponse>.Fail(ErrorCodes.BucketNotFound, $"Bucket '{name}' not found");
        }

        return Result<BucketResponse>.Ok(MapToResponse(bucket));
    }

    public async Task<Result<IReadOnlyList<BucketResponse>>> ListAsync(CancellationToken ct)
    {
        var buckets = await _db.Buckets
            .OrderBy(b => b.Name)
            .ToListAsync(ct);

        var result = buckets.Select(MapToResponse).ToList();
        return Result<IReadOnlyList<BucketResponse>>.Ok(result);
    }

    public async Task<Result<bool>> DeleteAsync(string name, CancellationToken ct)
    {
        var bucket = await _db.Buckets
            .Include(b => b.Objects)
            .FirstOrDefaultAsync(b => b.Name == name, ct);

        if (bucket == null)
        {
            return Result<bool>.Fail(ErrorCodes.BucketNotFound, $"Bucket '{name}' not found");
        }

        if (bucket.Objects.Count != 0)
        {
            return Result<bool>.Fail(ErrorCodes.BucketNotEmpty, $"Bucket '{name}' is not empty");
        }

        _db.Buckets.Remove(bucket);
        await _db.SaveChangesAsync(ct);

        return Result<bool>.Ok(true);
    }

    private static bool IsValidBucketName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length < 3 || name.Length > 63)
            return false;

        return BucketNameRegex().IsMatch(name);
    }

    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$")]
    private static partial Regex BucketNameRegex();

    private static BucketResponse MapToResponse(Bucket bucket)
    {
        return new BucketResponse(bucket.Id, bucket.Name, bucket.CreatedAt, bucket.UpdatedAt);
    }
}
