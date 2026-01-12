namespace DocMaster.Api.Data.Entities;

public class Chunk
{
    public string Id { get; set; } = null!;
    public string ObjectId { get; set; } = null!;
    public int ChunkIndex { get; set; }
    public long SizeBytes { get; set; }
    public string Checksum { get; set; } = null!;

    public StorageObject Object { get; set; } = null!;
    public ICollection<Shard> Shards { get; set; } = new List<Shard>();
}
