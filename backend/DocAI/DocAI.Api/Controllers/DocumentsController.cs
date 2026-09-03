using Microsoft.AspNetCore.Mvc;
using DocAI.Api.Data;
using DocAI.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace DocAI.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly BlobService _blobService;
    private readonly AppDbContext _context;
    private readonly DocumentProcessorService _documentProcessorService;

    public DocumentsController(BlobService blobService, AppDbContext context, DocumentProcessorService documentProcessorService)
    {
        _blobService = blobService;
        _context = context;
        _documentProcessorService = documentProcessorService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("File is empty");

        // 🔐 Get logged in user ID from token
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null)
            return Unauthorized("Invalid token");
        var userId = Guid.Parse(userIdClaim);

        var storedFileName = await _blobService.UploadAsync(file);

        // Save to DB with user details
        var document = new Document
        {
            FileName = file.FileName,
            FilePath = storedFileName,
            UploadedAt = DateTime.UtcNow,
            Status = "Uploaded",
            UserId = userId
        };

        try
        {
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();
            _ = Task.Run(() => _documentProcessorService.ProcessDocumentAsync(document.Id));
        }
        catch (Exception ex)
        {
            await _blobService.DeleteAsync(storedFileName);
            return StatusCode(500, ex.Message);
        }

        return Ok(document);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document == null)
        {
            return NotFound("Document not found");
        }

        try
        {
            var (stream, contentType, _) = await _blobService.DownloadAsync(document.FilePath);
            return File(stream, contentType, document.FileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound("File not found");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetDocuments()
    {
        // 🔐 Get logged in user ID from token
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userIdClaim == null)
            return Unauthorized("Invalid token");

        var userId = Guid.Parse(userIdClaim);

        var docs = await _context.Documents
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();

        return Ok(docs);
    }
}