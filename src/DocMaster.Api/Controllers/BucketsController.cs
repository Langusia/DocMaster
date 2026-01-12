using DocMaster.Api.Models;
using DocMaster.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocMaster.Api.Controllers;

[ApiController]
[Route("api/buckets")]
public class BucketsController : ControllerBase
{
    private readonly IBucketService _bucketService;

    public BucketsController(IBucketService bucketService)
    {
        _bucketService = bucketService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBucketRequest request, CancellationToken ct)
    {
        var result = await _bucketService.CreateAsync(request.Name, ct);
        if (!result.Success)
        {
            return ToErrorResponse(result.ErrorCode!, result.ErrorMessage!);
        }

        return Created($"/api/buckets/{result.Value!.Name}", result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _bucketService.ListAsync(ct);
        if (!result.Success)
        {
            return ToErrorResponse(result.ErrorCode!, result.ErrorMessage!);
        }

        return Ok(new BucketListResponse(result.Value!));
    }

    [HttpGet("{name}")]
    public async Task<IActionResult> Get(string name, CancellationToken ct)
    {
        var result = await _bucketService.GetAsync(name, ct);
        if (!result.Success)
        {
            return ToErrorResponse(result.ErrorCode!, result.ErrorMessage!);
        }

        return Ok(result.Value);
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> Delete(string name, CancellationToken ct)
    {
        var result = await _bucketService.DeleteAsync(name, ct);
        if (!result.Success)
        {
            return ToErrorResponse(result.ErrorCode!, result.ErrorMessage!);
        }

        return NoContent();
    }

    private IActionResult ToErrorResponse(string code, string message)
    {
        var requestId = HttpContext.TraceIdentifier;
        var error = new ErrorResponse(new ErrorDetail(code, message, requestId));

        return code switch
        {
            ErrorCodes.BucketNotFound => NotFound(error),
            ErrorCodes.BucketAlreadyExists => Conflict(error),
            ErrorCodes.BucketNotEmpty => Conflict(error),
            ErrorCodes.InvalidBucketName => BadRequest(error),
            _ => StatusCode(500, error)
        };
    }
}
