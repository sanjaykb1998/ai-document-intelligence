using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;

/// <summary>
/// Service for extracting text from documents using Azure AI Document Intelligence.
/// </summary>
public class AzureDocumentService
{
    private readonly DocumentAnalysisClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureDocumentService"/> class.
    /// </summary>
    /// <param name="config">The configuration containing Azure AI endpoint and key.</param>
    /// <exception cref="ArgumentNullException">Thrown when endpoint or key is null or empty.</exception>
    /// <exception cref="UriFormatException">Thrown when endpoint is not a valid URI.</exception>
    public AzureDocumentService(IConfiguration config)
    {
        var endpoint = config["AzureAI:Endpoint"];
        var key = config["AzureAI:Key"];

        _client = new DocumentAnalysisClient(
            new Uri(endpoint),
            new AzureKeyCredential(key));
    }

    /// <summary>
    /// Extracts text content from a document accessible via URL using Azure AI Document Intelligence.
    /// </summary>
    /// <param name="fileUrl">The URL of the document to analyze.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the extracted text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fileUrl is null or empty.</exception>
    /// <exception cref="UriFormatException">Thrown when fileUrl is not a valid URI.</exception>
    /// <exception cref="RequestFailedException">Thrown when the Azure service request fails.</exception>
    public async Task<string> ExtractTextFromUrlAsync(string fileUrl)
    {
        var operation = await _client.AnalyzeDocumentFromUriAsync(
            WaitUntil.Completed,
            "prebuilt-read",
            new Uri(fileUrl));

        var result = operation.Value;

        var text = "";

        // Iterate through all pages in the document
        foreach (var page in result.Pages)
        {
            // Extract text from each line on the page
            foreach (var line in page.Lines)
            {
                text += line.Content + "\n";
            }
        }

        return text;
    }
}