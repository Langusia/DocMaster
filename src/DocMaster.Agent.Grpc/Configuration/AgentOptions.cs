namespace DocMaster.Agent.Grpc.Configuration;

public class AgentOptions
{
    public const string SectionName = "Agent";

    public string BasePath { get; set; } = "/data";
    public int ShardSymbolCount { get; set; } = 2;
    public int ShardLevelCount { get; set; } = 2;
    public int GrpcPort { get; set; } = 5001;
}
