using System.Text;
using Tesseract;
using UglyToad.PdfPig;

public class DocumentTextService
{
    private readonly IWebHostEnvironment _environment;

    public DocumentTextService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> ExtractTextAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found.", filePath);
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => await ExtractFromPdfAsync(filePath),
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tif" or ".tiff" => await ExtractFromImageAsync(filePath),
            _ => throw new NotSupportedException($"Unsupported file type: {extension}")
        };
    }

    private static Task<string> ExtractFromPdfAsync(string filePath)
    {
        return Task.Run(() =>
        {
            var builder = new StringBuilder();
            using var document = PdfDocument.Open(filePath);

            foreach (var page in document.GetPages())
            {
                builder.AppendLine(page.Text);
            }

            return builder.ToString();
        });
    }

    private Task<string> ExtractFromImageAsync(string filePath)
    {
        return Task.Run(() =>
        {
            var tessDataPath = Path.Combine(_environment.ContentRootPath, "tessdata");
            if (!Directory.Exists(tessDataPath))
            {
                throw new DirectoryNotFoundException($"Tesseract tessdata folder not found: {tessDataPath}");
            }

            using var engine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
            using var image = Pix.LoadFromFile(filePath);
            using var page = engine.Process(image);
            return page.GetText();
        });
    }
}
