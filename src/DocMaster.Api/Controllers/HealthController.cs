using DocMaster.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocMaster.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly INodeCache _nodeCache;

    public HealthController(INodeCache nodeCache)
    {
        _nodeCache = nodeCache;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var allNodes = _nodeCache.GetAllNodes();
        var healthyNodes = _nodeCache.GetHealthyNodes();

        return Ok(new
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow,
            Nodes = new
            {
                Total = allNodes.Count,
                Healthy = healthyNodes.Count,
                Unhealthy = allNodes.Count - healthyNodes.Count
            }
        });
    }
}
