using System.Net;
using DocMaster.Api.Models;
using DocMaster.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocMaster.Api.Controllers;

[ApiController]
[Route("api/buckets/{bucket}/objects")]
public class ObjectsController : ControllerBase
{
    private readonly IObjectService _objectService;

    public ObjectsController(IObjectService objectService)
    {
        _objectService = objectService;
    }

    [HttpPut("{*key}")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> Upload(
        string bucket,
        string key,
        [FromHeader(Name = "X-Original-Filename")] string? originalFilename,
        CancellationToken ct)
    {
        var contentType = Request.ContentType;
        var result = await _objectService.UploadAsync(bucket, key, Request.Body, contentType, originalFilename, ct);

        if (!result.Success)
        {
            return ToErrorResponse(result.ErrorCode!, result.ErrorMessage!);
        }

        return Ok(result.Value);
    }

    [HttpGet("{*key}")]
    public async Task<IActionResult> Download(string bucket, string key, CancellationToken ct)
    {
        // First get object info for headers
        var infoResult = await _objectService.GetInfoAsync(bucket, key, ct);
        if (!infoResult.Success)
        {
            return ToErrorResponse(infoResult.ErrorCode!, infoResult.ErrorMessage!);
        }

        var info = infoResult.Value!;
        var streamResult = await _objectService.DownloadAsync(bucket, key, ct);
        if (!streamResult.Success)
        {
            return ToErrorResponse(streamResult.ErrorCode!, streamResult.ErrorMessage!);
        }

        // Determine filename for Content-Disposition
        var filename = ResolveDownloadFilename(info);

        Response.Headers.ETag = $"\"{info.Checksum}\"";
        Response.Headers.ContentDisposition = $"attachment; filename*=UTF-8''{Uri.EscapeDataString(filename)}";

        return File(streamResult.Value!, info.ContentType, enableRangeProcessing: false);
    }

    [HttpHead("{*key}")]
    public async Task<IActionResult> Head(string bucket, string key, CancellationToken ct)
    {
        var result = await _objectService.GetInfoAsync(bucket, key, ct);
        if (!result.Success)
        {
            return ToErrorResponse(result.ErrorCode!, result.ErrorMessage!);
        }

        var info = result.Value!;
        var filename = ResolveDownloadFilename(info);

        Response.Headers.ContentType = info.ContentType;
        Response.Headers.ContentLength = info.Size;
        Response.Headers.ETag = $"\"{info.Checksum}\"";
        Response.Headers.ContentDisposition = $"attachment; filename*=UTF-8''{Uri.EscapeDataString(filename)}";
        Response.Headers["X-Object-Status"] = info.Status;
        Response.Headers["X-Storage-Strategy"] = info.StorageStrategy;

        return Ok();
    }

    [HttpDelete("{*key}")]
    public async Task<IActionResult> Delete(string bucket, string key, CancellationToken ct)
    {
        var result = await _objectService.DeleteAsync(bucket, key, ct);
        if (!result.Success)
        {
            return ToErrorResponse(result.ErrorCode!, result.ErrorMessage!);
        }

        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> List(string bucket, CancellationToken ct)
    {
        var result = await _objectService.ListAsync(bucket, ct);
        if (!result.Success)
        {
            return ToErrorResponse(result.ErrorCode!, result.ErrorMessage!);
        }

        return Ok(new ObjectListResponse(result.Value!));
    }

    private static string ResolveDownloadFilename(ObjectInfo info)
    {
        // Get base name from original filename or key
        var baseName = info.OriginalFilename ?? info.Key;
        baseName = Path.GetFileNameWithoutExtension(baseName);

        // Get extension - prefer detected extension
        var extension = info.DetectedExtension;
        if (string.IsNullOrEmpty(extension))
        {
            extension = Path.GetExtension(info.OriginalFilename ?? info.Key)?.TrimStart('.');
        }

        if (string.IsNullOrEmpty(extension))
        {
            return baseName;
        }

        return $"{baseName}.{extension}";
    }

    private IActionResult ToErrorResponse(string code, string message)
    {
        var requestId = HttpContext.TraceIdentifier;
        var error = new ErrorResponse(new ErrorDetail(code, message, requestId));

        return code switch
        {
            ErrorCodes.BucketNotFound => NotFound(error),
            ErrorCodes.ObjectNotFound => NotFound(error),
            ErrorCodes.ObjectTooLarge => StatusCode((int)HttpStatusCode.RequestEntityTooLarge, error),
            ErrorCodes.InsufficientNodes => StatusCode((int)HttpStatusCode.ServiceUnavailable, error),
            ErrorCodes.DangerousContentType => BadRequest(error),
            ErrorCodes.InvalidKey => BadRequest(error),
            ErrorCodes.UploadFailed => StatusCode((int)HttpStatusCode.BadGateway, error),
            ErrorCodes.DownloadFailed => StatusCode((int)HttpStatusCode.BadGateway, error),
            _ => StatusCode(500, error)
        };
    }
}
