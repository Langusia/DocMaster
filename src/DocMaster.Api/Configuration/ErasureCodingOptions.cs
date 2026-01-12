namespace DocMaster.Api.Configuration;

public class ErasureCodingOptions
{
    public const string SectionName = "ErasureCoding";

    public int DataShards { get; set; } = 6;
    public int ParityShards { get; set; } = 3;
    public int ChunkSizeBytes { get; set; } = 10485760; // 10MB
    public int SmallObjectThreshold { get; set; } = 65536; // 64KB
    public long MaxFileSizeBytes { get; set; } = 1073741824; // 1GB

    public int TotalShards => DataShards + ParityShards;
}
