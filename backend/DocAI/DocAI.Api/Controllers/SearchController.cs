using DocAI.Api.Data;
using DocAI.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocAI.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly DocumentChunkService _chunkService;

    public SearchController(DocumentChunkService chunkService)
    {
        _chunkService = chunkService;
    }

    [HttpPost("semantic-search")]
    public async Task<IActionResult> SemanticSearch([FromBody] SearchRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest("Query is required");
        }

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null)
        {
            return Unauthorized("Invalid token");
        }

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized("Invalid token");
        }
        var results = await _chunkService.SearchAsync(userId, request.Query);

        return Ok(results);
    }
}
