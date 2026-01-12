using DocMaster.Api.Models;
using DocMaster.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocMaster.Api.Controllers;

[ApiController]
[Route("api/nodes")]
public class NodesController : ControllerBase
{
    private readonly INodeService _nodeService;

    public NodesController(INodeService nodeService)
    {
        _nodeService = nodeService;
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterNodeRequest request, CancellationToken ct)
    {
        var result = await _nodeService.RegisterAsync(request.Name, request.GrpcAddress, ct);
        if (!result.Success)
        {
            return ToErrorResponse(result.ErrorCode!, result.ErrorMessage!);
        }

        return Created($"/api/nodes/{result.Value!.Id}", result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _nodeService.ListAsync(ct);
        if (!result.Success)
        {
            return ToErrorResponse(result.ErrorCode!, result.ErrorMessage!);
        }

        return Ok(new NodeListResponse(result.Value!));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var result = await _nodeService.GetAsync(id, ct);
        if (!result.Success)
        {
            return ToErrorResponse(result.ErrorCode!, result.ErrorMessage!);
        }

        return Ok(result.Value);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Unregister(string id, CancellationToken ct)
    {
        var result = await _nodeService.UnregisterAsync(id, ct);
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
            ErrorCodes.NodeNotFound => NotFound(error),
            ErrorCodes.InvalidKey => BadRequest(error),
            _ => StatusCode(500, error)
        };
    }
}
