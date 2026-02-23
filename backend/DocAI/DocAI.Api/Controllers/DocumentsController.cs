using Microsoft.AspNetCore.Mvc;
using DocAI.Api.Data;
using DocAI.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DocAI.Api.Controllers;

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

        // Upload to Azure
        var fileUrl = await _blobService.UploadAsync(file);

        // Save to DB
        var document = new Document
        {
            FileName = file.FileName,
            FilePath = fileUrl,
            UploadedAt = DateTime.UtcNow,
            Status = "Uploaded"
        };

        try
        {
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();
            _ = Task.Run(() => _documentProcessorService.ProcessDocumentAsync(document.Id));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }

        return Ok(document);
    }

    [HttpGet]
    public async Task<IActionResult> GetDocuments()
    {
        var docs = await _context.Documents
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();

        return Ok(docs);
    }
}