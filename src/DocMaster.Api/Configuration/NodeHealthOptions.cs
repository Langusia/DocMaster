namespace DocMaster.Api.Configuration;

public class NodeHealthOptions
{
    public const string SectionName = "NodeHealth";

    public int PollIntervalSeconds { get; set; } = 10;
    public int MaxConsecutiveFailures { get; set; } = 3;
}
