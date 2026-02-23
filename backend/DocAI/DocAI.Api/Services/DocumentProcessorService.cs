using DocAI.Api.Data;

public class DocumentProcessorService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AzureDocumentService _azureService;

    public DocumentProcessorService(
        IServiceScopeFactory scopeFactory,
        AzureDocumentService azureDocumentService)
    {
        _scopeFactory = scopeFactory;
        _azureService = azureDocumentService;
    }

    public async Task ProcessDocumentAsync(Guid documentId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var blobService = scope.ServiceProvider.GetRequiredService<BlobService>();

        try
        {
            var document = await context.Documents.FindAsync(documentId);
            if (document == null) return;

            document.Status = "Processing";
            await context.SaveChangesAsync();

            // Call Azure AI
            var extractedText =
                await _azureService.ExtractTextFromUrlAsync(document.FilePath);

            document.ExtractedText = extractedText;
            document.Status = "Processed";

            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            var document = await context.Documents.FindAsync(documentId);
            if (document != null)
            {
                document.Status = "Failed";
                await context.SaveChangesAsync();
            }
        }
    }
}