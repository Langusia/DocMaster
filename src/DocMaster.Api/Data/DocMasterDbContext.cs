using DocMaster.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocMaster.Api.Data;

public class DocMasterDbContext : DbContext
{
    public DocMasterDbContext(DbContextOptions<DocMasterDbContext> options)
        : base(options)
    {
    }

    public DbSet<Bucket> Buckets => Set<Bucket>();
    public DbSet<StorageObject> Objects => Set<StorageObject>();
    public DbSet<Chunk> Chunks => Set<Chunk>();
    public DbSet<Shard> Shards => Set<Shard>();
    public DbSet<Replica> Replicas => Set<Replica>();
    public DbSet<Node> Nodes => Set<Node>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Bucket
        modelBuilder.Entity<Bucket>(entity =>
        {
            entity.ToTable("buckets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(26);
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.Name).IsUnique();
        });

        // StorageObject
        modelBuilder.Entity<StorageObject>(entity =>
        {
            entity.ToTable("objects");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(26);
            entity.Property(e => e.BucketId).HasColumnName("bucket_id").HasMaxLength(26).IsRequired();
            entity.Property(e => e.Key).HasColumnName("key").HasMaxLength(1024).IsRequired();
            entity.Property(e => e.SizeBytes).HasColumnName("size_bytes");
            entity.Property(e => e.Checksum).HasColumnName("checksum").HasMaxLength(64).IsRequired();

            entity.Property(e => e.ContentType).HasColumnName("content_type").HasMaxLength(255).IsRequired();
            entity.Property(e => e.DetectedContentType).HasColumnName("detected_content_type").HasMaxLength(255);
            entity.Property(e => e.ClaimedContentType).HasColumnName("claimed_content_type").HasMaxLength(255);
            entity.Property(e => e.DetectedExtension).HasColumnName("detected_extension").HasMaxLength(20);
            entity.Property(e => e.OriginalFilename).HasColumnName("original_filename").HasMaxLength(500);

            entity.Property(e => e.StorageStrategy)
                .HasColumnName("storage_strategy")
                .HasMaxLength(20)
                .HasConversion(
                    v => v == StorageStrategy.Replicated ? "replicated" : "erasure_coded",
                    v => v == "replicated" ? StorageStrategy.Replicated : StorageStrategy.ErasureCoded);

            entity.Property(e => e.ChunkCount).HasColumnName("chunk_count").HasDefaultValue(1);

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .HasConversion(
                    v => v.ToString().ToLowerInvariant(),
                    v => Enum.Parse<ObjectStatus>(v, true));

            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Bucket)
                .WithMany(b => b.Objects)
                .HasForeignKey(e => e.BucketId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.BucketId, e.Key }).IsUnique();
            entity.HasIndex(e => e.Status);
        });

        // Chunk
        modelBuilder.Entity<Chunk>(entity =>
        {
            entity.ToTable("chunks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(26);
            entity.Property(e => e.ObjectId).HasColumnName("object_id").HasMaxLength(26).IsRequired();
            entity.Property(e => e.ChunkIndex).HasColumnName("chunk_index");
            entity.Property(e => e.SizeBytes).HasColumnName("size_bytes");
            entity.Property(e => e.Checksum).HasColumnName("checksum").HasMaxLength(64).IsRequired();

            entity.HasOne(e => e.Object)
                .WithMany(o => o.Chunks)
                .HasForeignKey(e => e.ObjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ObjectId, e.ChunkIndex }).IsUnique();
            entity.HasIndex(e => e.ObjectId);
        });

        // Shard
        modelBuilder.Entity<Shard>(entity =>
        {
            entity.ToTable("shards");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(26);
            entity.Property(e => e.ChunkId).HasColumnName("chunk_id").HasMaxLength(26).IsRequired();
            entity.Property(e => e.ShardIndex).HasColumnName("shard_index");
            entity.Property(e => e.NodeId).HasColumnName("node_id").HasMaxLength(26).IsRequired();
            entity.Property(e => e.SizeBytes).HasColumnName("size_bytes");
            entity.Property(e => e.Checksum).HasColumnName("checksum").HasMaxLength(64).IsRequired();

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .HasConversion(
                    v => v.ToString().ToLowerInvariant(),
                    v => Enum.Parse<ShardStatus>(v, true));

            entity.HasOne(e => e.Chunk)
                .WithMany(c => c.Shards)
                .HasForeignKey(e => e.ChunkId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Node)
                .WithMany(n => n.Shards)
                .HasForeignKey(e => e.NodeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.ChunkId, e.ShardIndex }).IsUnique();
            entity.HasIndex(e => e.ChunkId);
            entity.HasIndex(e => e.NodeId);
        });

        // Replica
        modelBuilder.Entity<Replica>(entity =>
        {
            entity.ToTable("replicas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(26);
            entity.Property(e => e.ObjectId).HasColumnName("object_id").HasMaxLength(26).IsRequired();
            entity.Property(e => e.NodeId).HasColumnName("node_id").HasMaxLength(26).IsRequired();
            entity.Property(e => e.Checksum).HasColumnName("checksum").HasMaxLength(64).IsRequired();

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .HasConversion(
                    v => v.ToString().ToLowerInvariant(),
                    v => Enum.Parse<ReplicaStatus>(v, true));

            entity.HasOne(e => e.Object)
                .WithMany(o => o.Replicas)
                .HasForeignKey(e => e.ObjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Node)
                .WithMany(n => n.Replicas)
                .HasForeignKey(e => e.NodeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.ObjectId, e.NodeId }).IsUnique();
            entity.HasIndex(e => e.ObjectId);
            entity.HasIndex(e => e.NodeId);
        });

        // Node
        modelBuilder.Entity<Node>(entity =>
        {
            entity.ToTable("nodes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(26);
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.GrpcAddress).HasColumnName("grpc_address").HasMaxLength(255).IsRequired();

            entity.Property(e => e.IsHealthy).HasColumnName("is_healthy").HasDefaultValue(true);
            entity.Property(e => e.TotalSpaceBytes).HasColumnName("total_space_bytes");
            entity.Property(e => e.FreeSpaceBytes).HasColumnName("free_space_bytes");
            entity.Property(e => e.UsedSpaceBytes).HasColumnName("used_space_bytes");
            entity.Property(e => e.ObjectCount).HasColumnName("object_count");
            entity.Property(e => e.ConsecutiveFailures).HasColumnName("consecutive_failures").HasDefaultValue(0);

            entity.Property(e => e.LastSeenAt).HasColumnName("last_seen_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.IsHealthy);
        });
    }
}
