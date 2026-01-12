namespace DocMaster.Api.Models;

public record ErrorResponse(ErrorDetail Error);

public record ErrorDetail(string Code, string Message, string RequestId);

public static class ErrorCodes
{
    public const string BucketNotFound = "BucketNotFound";
    public const string BucketNotEmpty = "BucketNotEmpty";
    public const string BucketAlreadyExists = "BucketAlreadyExists";
    public const string ObjectNotFound = "ObjectNotFound";
    public const string ObjectTooLarge = "ObjectTooLarge";
    public const string InsufficientNodes = "InsufficientNodes";
    public const string DangerousContentType = "DangerousContentType";
    public const string InvalidKey = "InvalidKey";
    public const string InvalidBucketName = "InvalidBucketName";
    public const string UploadFailed = "UploadFailed";
    public const string DownloadFailed = "DownloadFailed";
    public const string NodeNotFound = "NodeNotFound";
    public const string InternalError = "InternalError";
}
