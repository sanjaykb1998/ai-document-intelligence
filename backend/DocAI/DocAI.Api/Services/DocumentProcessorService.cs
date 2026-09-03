using DocAI.Api.Data;
using Microsoft.Extensions.Logging;

public class DocumentProcessorService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentProcessorService> _logger;

    public DocumentProcessorService(
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentProcessorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ProcessDocumentAsync(Guid documentId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var blobService = scope.ServiceProvider.GetRequiredService<BlobService>();
        var textService = scope.ServiceProvider.GetRequiredService<DocumentTextService>();
        var chunkService = scope.ServiceProvider.GetRequiredService<DocumentChunkService>();

        try
        {
            var document = await context.Documents.FindAsync(documentId);
            if (document == null) return;

            document.Status = "Processing";
            await context.SaveChangesAsync();

            var filePath = blobService.GetFilePath(document.FilePath);
            var extractedText = await textService.ExtractTextAsync(filePath);

            document.ExtractedText = extractedText;
            document.Status = "Processed";

            await context.SaveChangesAsync();
            await chunkService.StoreDocumentChunksAsync(document, extractedText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document {DocumentId}", documentId);

            var document = await context.Documents.FindAsync(documentId);
            if (document != null)
            {
                document.Status = "Failed";
                await context.SaveChangesAsync();
            }
        }
    }
}