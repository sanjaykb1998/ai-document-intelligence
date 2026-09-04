using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
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
            ".docx" => await ExtractFromDocxAsync(filePath),
            ".txt" => await ExtractFromTxtAsync(filePath),
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

    private static Task<string> ExtractFromDocxAsync(string filePath)
    {
        return Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(filePath);
            var entry = archive.GetEntry("word/document.xml");
            if (entry == null)
            {
                return string.Empty;
            }

            using var stream = entry.Open();
            var xdoc = XDocument.Load(stream);

            var paragraphs = xdoc.Descendants().Where(e => e.Name.LocalName == "p");
            var builder = new StringBuilder();

            foreach (var p in paragraphs)
            {
                var pText = string.Concat(p.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value));
                if (!string.IsNullOrWhiteSpace(pText))
                {
                    builder.AppendLine(pText);
                }
            }

            return builder.ToString();
        });
    }

    private static async Task<string> ExtractFromTxtAsync(string filePath)
    {
        return await File.ReadAllTextAsync(filePath, Encoding.UTF8);
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
