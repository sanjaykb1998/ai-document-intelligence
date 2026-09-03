using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocAI.Api.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class RagService
{
    private readonly DocumentChunkService _chunkService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RagService> _logger;

    public RagService(
        DocumentChunkService chunkService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<RagService> logger)
    {
        _chunkService = chunkService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AskResponse> AskAsync(Guid userId, string query)
    {
        var askTask = AskCoreAsync(userId, query);
        var completedTask = await Task.WhenAny(askTask, Task.Delay(TimeSpan.FromSeconds(25)));
        if (completedTask != askTask)
        {
            return new AskResponse
            {
                Answer = "I could not finish generating the answer in time. Please try again.",
                Sources = Array.Empty<SearchResult>()
            };
        }

        return await askTask;
    }

    private async Task<AskResponse> AskCoreAsync(Guid userId, string query)
    {
        var sources = await _chunkService.SearchAsync(userId, query, top: 5);
        var answer = await GenerateAnswerAsync(query, sources);
        return new AskResponse
        {
            Answer = answer,
            Sources = sources
        };
    }

    private async Task<string> GenerateAnswerAsync(string query, IReadOnlyList<SearchResult> sources)
    {
        var context = string.Join("\n\n", sources.Select(source =>
            $"Source: {source.FileName} (chunk {source.ChunkIndex})\n{source.Text}"));

        if (string.IsNullOrWhiteSpace(context))
        {
            context = "No document context was found for this question.";
        }

        var prompt = $"""
You are a helpful and precise assistant for answering questions based on uploaded documents.
Guidelines:
1. Base your answer strictly on the provided document context. Do not guess or hallucinate details that are not present.
2. If the user asks for a person's full name, identity details, or specific names, extract the complete full name exactly as written in the documents (including surname/last name, first name, and middle names, e.g. "Bayyareddy, Sanjay Kumar" or "Sanjay Kumar Bayyareddy").
3. If the question asks for an amount, dates, or a duration, compute/return the exact value found in or derivable from the context, including currency/units.
4. If the answer cannot be found in the document context, say so clearly instead of guessing.
5. Keep the answer concise (1-3 clear sentences) and factual.

Question:
{query}

Document context:
{context}
""";

        var apiKey = _configuration["Llm:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            var llmAnswer = await GenerateAnswerWithChatCompletionsAsync(prompt, apiKey);
            if (!string.IsNullOrWhiteSpace(llmAnswer))
            {
                return llmAnswer;
            }
        }

        var ollamaAnswer = await GenerateAnswerWithOllamaAsync(prompt);
        if (!string.IsNullOrWhiteSpace(ollamaAnswer))
        {
            return ollamaAnswer;
        }

        // Heuristic-based extraction is only used as a last-resort fallback
        // when no LLM (Groq/OpenAI/Ollama) is reachable.
        var directAnswer = TryBuildDirectAnswer(query, sources);
        if (!string.IsNullOrWhiteSpace(directAnswer))
        {
            return directAnswer;
        }

        return BuildFallbackAnswer(query, sources);
    }

    private async Task<string?> GenerateAnswerWithChatCompletionsAsync(string prompt, string apiKey)
    {
        var model = _configuration["Llm:Model"] ?? "gpt-4o-mini";
        var baseUrl = (_configuration["Llm:BaseUrl"] ?? "https://api.openai.com/v1").TrimEnd('/');

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(25);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var response = await client.PostAsJsonAsync(
                $"{baseUrl}/chat/completions",
                new
                {
                    model,
                    temperature = 0.2,
                    max_tokens = 350,
                    messages = new[]
                    {
                        new { role = "system", content = "You are a concise document question answering assistant. Answer like ChatGPT in 1-3 short sentences." },
                        new { role = "user", content = prompt }
                    }
                });

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<ChatCompletionsResponse>();
            var content = payload?.Choices?.FirstOrDefault()?.Message?.Content;
            return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "LLM API request failed");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "LLM API request timed out");
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "LLM API response parsing failed");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "LLM API response was invalid JSON");
        }

        return null;
    }

    private async Task<string?> GenerateAnswerWithOllamaAsync(string prompt)
    {
        var model = _configuration["Ollama:Model"] ?? "llama3.2:3b";
        var baseUrl = _configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            var response = await client.PostAsJsonAsync(
                $"{baseUrl}/api/generate",
                new
                {
                    model,
                    prompt,
                    stream = false
                });

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>();
            var content = payload?.Response;
            return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Ollama request failed");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Ollama request timed out");
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Ollama response parsing failed");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Ollama response was invalid JSON");
        }

        return null;
    }

    private static string? TryBuildDirectAnswer(string query, IReadOnlyList<SearchResult> sources)
    {
        var queryTokens = Tokenize(query).ToList();
        var subject = queryTokens.FirstOrDefault(token =>
            token is not ("what" or "which" or "when" or "where" or "why" or "how" or "is" or "are" or "the" or "a" or "an" or "amount" or "charge" or "tax" or "bill" or "recharge"));

        var wantsStayLength = queryTokens.Any(token =>
            token is "day" or "days" or "night" or "nights" or "stay" or "stayed" or "duration" or "how" or "long");

        if (wantsStayLength)
        {
            foreach (var source in sources)
            {
                var stayLength = TryExtractStayLength(source.Text);
                if (stayLength.HasValue)
                {
                    return $"You stayed for {stayLength.Value} days.";
                }
            }
        }

        var amountPatterns = new[]
        {
            new Regex(@"(?:₹|rs\.?|inr|\$|usd)\s?\d[\d,]*(?:\.\d+)?", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b\d[\d,]*(?:\.\d+)?\b", RegexOptions.Compiled)
        };

        foreach (var source in sources)
        {
            var text = source.Text ?? string.Empty;
            var subjectMatch = string.IsNullOrWhiteSpace(subject) || text.Contains(subject, StringComparison.OrdinalIgnoreCase);
            if (!subjectMatch)
            {
                continue;
            }

            foreach (var pattern in amountPatterns)
            {
                var match = pattern.Matches(text).Cast<Match>()
                    .FirstOrDefault(m => HasNearbyKeyword(text, m.Index, new[] { "amount", "recharge", "tax", "total", "charge", "bill" }));

                if (match != null)
                {
                    var amount = NormalizeAmount(match.Value);
                    if (string.IsNullOrWhiteSpace(amount))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(subject))
                    {
                        return $"The {subject} amount is {amount}.";
                    }

                    return $"The amount found in the document is {amount}.";
                }
            }
        }

        return null;
    }

    private static int? TryExtractStayLength(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var dateRangeMatch = Regex.Match(
            text,
            @"Date Range:\s*(?<start>\d{4}-\d{2}-\d{2})\s*-\s*(?<end>\d{4}-\d{2}-\d{2})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        if (dateRangeMatch.Success &&
            DateTime.TryParse(dateRangeMatch.Groups["start"].Value, out var startDate) &&
            DateTime.TryParse(dateRangeMatch.Groups["end"].Value, out var endDate))
        {
            return (endDate.Date - startDate.Date).Days + 1;
        }

        var checkInOutMatch = Regex.Match(
            text,
            @"Check In Date(?<start>[A-Za-z]+\s+\d{1,2},\s*\d{4}).*?Check Out Date(?<end>[A-Za-z]+\s+\d{1,2},\s*\d{4})",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        if (checkInOutMatch.Success &&
            DateTime.TryParseExact(checkInOutMatch.Groups["start"].Value.Trim(), "MMMM d, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out startDate) &&
            DateTime.TryParseExact(checkInOutMatch.Groups["end"].Value.Trim(), "MMMM d, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out endDate))
        {
            return (endDate.Date - startDate.Date).Days + 1;
        }

        return null;
    }

    private static bool HasNearbyKeyword(string text, int index, IEnumerable<string> keywords, int window = 120)
    {
        var start = Math.Max(0, index - window);
        var length = Math.Min(text.Length - start, window * 2);
        if (length <= 0)
        {
            return false;
        }

        var segment = text.Substring(start, length);
        return keywords.Any(keyword => segment.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeAmount(string value)
    {
        return value.Replace("  ", " ").Trim();
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        return text
            .Split(new[] { ' ', '\t', '\r', '\n', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => new string(token.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant())
            .Where(token => token.Length >= 3);
    }

    private static string BuildFallbackAnswer(string query, IReadOnlyList<SearchResult> sources)
    {
        if (sources.Count == 0)
        {
            return $"I couldn't find this in your uploaded documents. Question: {query}";
        }

        var best = sources.First();
        var excerpt = BuildExcerpt(best.Text, query);
        return $"I found a relevant section in {best.FileName}. {excerpt}";
    }

    private static string BuildExcerpt(string? text, string query)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "The document section was empty.";
        }

        var keywords = Tokenize(query)
            .Concat(new[] { "amount", "tax", "fee", "charge", "total", "recharge", "invoice", "payment" })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(keyword => keyword.Length >= 3)
            .ToList();

        var matchIndex = keywords
            .Select(keyword => text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase))
            .Where(index => index >= 0)
            .DefaultIfEmpty(-1)
            .Min();

        var start = matchIndex >= 0 ? Math.Max(0, matchIndex - 80) : 0;
        var length = Math.Min(text.Length - start, 260);
        var excerpt = text.Substring(start, length).Trim();

        if (text.Length > start + length)
        {
            excerpt += "...";
        }

        return excerpt;
    }

    private sealed class OllamaGenerateResponse
    {
        public string Response { get; set; }
    }

    private sealed class ChatCompletionsResponse
    {
        public List<ChatChoice> Choices { get; set; }
    }

    private sealed class ChatChoice
    {
        public ChatMessage Message { get; set; }
    }

    private sealed class ChatMessage
    {
        public string Content { get; set; }
    }
}
