using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;

public class AzureDocumentService
{
    private readonly DocumentAnalysisClient _client;

    public AzureDocumentService(IConfiguration config)
    {
        var endpoint = config["AzureAI:Endpoint"];
        var key = config["AzureAI:Key"];

        _client = new DocumentAnalysisClient(
            new Uri(endpoint),
            new AzureKeyCredential(key));
    }

    public async Task<string> ExtractTextFromUrlAsync(string fileUrl)
    {
        var operation = await _client.AnalyzeDocumentFromUriAsync(
            WaitUntil.Completed,
            "prebuilt-read",
            new Uri(fileUrl));

        var result = operation.Value;

        var text = "";

        foreach (var page in result.Pages)
        {
            foreach (var line in page.Lines)
            {
                text += line.Content + "\n";
            }
        }

        return text;
    }
}