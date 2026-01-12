namespace DocMaster.Api.Configuration;

public class UploadOptions
{
    public const string SectionName = "Upload";

    public int MaxNodeAttempts { get; set; } = 3;
    public int GrpcTimeoutSeconds { get; set; } = 30;
}
