using DocAI.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DocAI.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RagController : ControllerBase
{
    private readonly RagService _ragService;

    public RagController(RagService ragService)
    {
        _ragService = ragService;
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] AskRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest("Query is required");
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim))
        {
            return Unauthorized("Invalid token");
        }

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized("Invalid token");
        }
        var response = await _ragService.AskAsync(userId, request.Query);
        return Ok(response);
    }
}
