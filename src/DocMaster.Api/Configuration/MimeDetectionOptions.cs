namespace DocMaster.Api.Configuration;

public class MimeDetectionOptions
{
    public const string SectionName = "MimeDetection";

    public bool RejectDangerousMismatches { get; set; } = true;
}
