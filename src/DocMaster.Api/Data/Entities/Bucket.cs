namespace DocMaster.Api.Data.Entities;

public class Bucket
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<StorageObject> Objects { get; set; } = new List<StorageObject>();
}
