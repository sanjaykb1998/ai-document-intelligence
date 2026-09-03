using System.Net.Http.Json;
using System.Text.Json;
using DocAI.Api.Models;
using Microsoft.Extensions.Logging;

public class DocumentChunkService
{
    private static readonly SemaphoreSlim FileLock = new(1, 1);
    private readonly string _indexFilePath;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentChunkService> _logger;

    public DocumentChunkService(
        IWebHostEnvironment environment,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<DocumentChunkService> logger)
    {
        var appDataPath = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(appDataPath);
        _indexFilePath = Path.Combine(appDataPath, "document-chunks.json");
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StoreDocumentChunksAsync(Document document, string extractedText)
    {
        var chunksToStore = await CreateChunksAsync(document, extractedText);

        await FileLock.WaitAsync();
        try
        {
            var allChunks = await LoadChunksUnsafeAsync();
            allChunks.RemoveAll(chunk => chunk.DocumentId == document.Id);
            allChunks.AddRange(chunksToStore);
            await SaveChunksUnsafeAsync(allChunks);
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task RebuildIndexAsync(IEnumerable<Document> documents)
    {
        var allChunks = new List<DocumentChunk>();
        foreach (var document in documents)
        {
            if (string.IsNullOrWhiteSpace(document.ExtractedText))
            {
                continue;
            }

            var chunks = await CreateChunksAsync(document, document.ExtractedText);
            allChunks.AddRange(chunks);
        }

        await FileLock.WaitAsync();
        try
        {
            await SaveChunksUnsafeAsync(allChunks);
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(Guid userId, string query, int top = 5)
    {
        var queryEmbedding = await GetEmbeddingAsync(query);
        if (queryEmbedding.Length == 0 || queryEmbedding.All(value => value == 0))
        {
            return Array.Empty<SearchResult>();
        }

        await FileLock.WaitAsync();
        try
        {
            var chunks = await LoadChunksUnsafeAsync();
            return chunks
                .Where(chunk => chunk.UserId == userId)
                .Select(chunk => new SearchResult
                {
                    DocumentId = chunk.DocumentId,
                    FileName = chunk.FileName,
                    ChunkIndex = chunk.ChunkIndex,
                    Text = chunk.Text,
                    Score = ScoreChunk(queryEmbedding, chunk, query)
                })
                .Where(result => result.Score > 0.1)
                .OrderByDescending(result => result.Score)
                .ThenBy(result => result.FileName)
                .ThenBy(result => result.ChunkIndex)
                .Take(top)
                .ToList();
        }
        finally
        {
            FileLock.Release();
        }
    }

    private async Task<List<DocumentChunk>> CreateChunksAsync(Document document, string extractedText)
    {
        var chunkTexts = ChunkText(extractedText).ToList();
        var chunks = new List<DocumentChunk>(chunkTexts.Count);
        for (var index = 0; index < chunkTexts.Count; index++)
        {
            var text = chunkTexts[index];
            chunks.Add(new DocumentChunk
            {
                DocumentId = document.Id,
                UserId = document.UserId,
                FileName = document.FileName,
                ChunkIndex = index,
                Text = text,
                Embedding = await GetEmbeddingAsync(text),
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        return chunks;
    }

    private async Task<float[]> GetEmbeddingAsync(string text)
    {
        var model = _configuration["Ollama:EmbeddingModel"] ?? "nomic-embed-text";
        var baseUrl = _configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(12);

            var legacyResponse = await client.PostAsJsonAsync(
                $"{baseUrl}/api/embeddings",
                new { model, prompt = text });

            if (legacyResponse.IsSuccessStatusCode)
            {
                await using var stream = await legacyResponse.Content.ReadAsStreamAsync();
                using var json = await JsonDocument.ParseAsync(stream);
                if (TryReadEmbedding(json.RootElement, out var embedding))
                {
                    Normalize(embedding);
                    return embedding;
                }
            }

            var response = await client.PostAsJsonAsync(
                $"{baseUrl}/api/embed",
                new { model, input = text });

            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync();
                using var json = await JsonDocument.ParseAsync(stream);
                if (TryReadEmbedding(json.RootElement, out var embedding))
                {
                    Normalize(embedding);
                    return embedding;
                }
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Embedding request failed. Using hashed fallback embedding.");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Embedding request timed out. Using hashed fallback embedding.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Embedding response parsing failed. Using hashed fallback embedding.");
        }

        return BuildHashedEmbedding(text);
    }

    private static bool TryReadEmbedding(JsonElement root, out float[] embedding)
    {
        if (root.TryGetProperty("embedding", out var embeddingProperty) && embeddingProperty.ValueKind == JsonValueKind.Array)
        {
            embedding = embeddingProperty.EnumerateArray().Select(item => item.GetSingle()).ToArray();
            return embedding.Length > 0;
        }

        if (root.TryGetProperty("embeddings", out var embeddingsProperty) &&
            embeddingsProperty.ValueKind == JsonValueKind.Array &&
            embeddingsProperty.GetArrayLength() > 0)
        {
            var first = embeddingsProperty[0];
            if (first.ValueKind == JsonValueKind.Array)
            {
                embedding = first.EnumerateArray().Select(item => item.GetSingle()).ToArray();
                return embedding.Length > 0;
            }
        }

        embedding = Array.Empty<float>();
        return false;
    }

    private static IEnumerable<string> ChunkText(string text, int chunkSize = 1200, int overlap = 200)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var normalizedText = text.Replace("\r\n", "\n");
        var start = 0;

        while (start < normalizedText.Length)
        {
            var length = Math.Min(chunkSize, normalizedText.Length - start);
            yield return normalizedText.Substring(start, length).Trim();

            if (start + length >= normalizedText.Length)
            {
                yield break;
            }

            start += Math.Max(1, chunkSize - overlap);
        }
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var tokens = new List<string>();
        foreach (var token in text.Split(
                     new[] { ' ', '\t', '\r', '\n', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'' },
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = new string(token.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            if (normalized.Length >= 3)
            {
                tokens.Add(normalized);
            }
        }

        return tokens;
    }

    private static float[] BuildHashedEmbedding(string text, int dimensions = 256)
    {
        var vector = new float[dimensions];
        var tokens = Tokenize(text).ToList();

        for (var i = 0; i < tokens.Count; i++)
        {
            AddHashedToken(vector, tokens[i]);
            if (i + 1 < tokens.Count)
            {
                AddHashedToken(vector, $"{tokens[i]}_{tokens[i + 1]}");
            }
        }

        Normalize(vector);
        return vector;
    }

    private static void AddHashedToken(float[] vector, string token)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var ch in token)
            {
                hash ^= ch;
                hash *= 16777619;
            }

            var index = (int)(hash % (uint)vector.Length);
            vector[index] += 1f;
        }
    }

    private static void Normalize(float[] vector)
    {
        var magnitude = MathF.Sqrt(vector.Sum(value => value * value));
        if (magnitude <= 0)
        {
            return;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= magnitude;
        }
    }

    private static double ScoreChunk(float[] queryEmbedding, DocumentChunk chunk, string query)
    {
        if (chunk.Embedding == null || chunk.Embedding.Length == 0)
        {
            return 0;
        }

        var cosine = CosineSimilarity(queryEmbedding, chunk.Embedding);
        var chunkTokens = Tokenize(chunk.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var queryTokens = Tokenize(query).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var overlapBoost = queryTokens.Count(token => chunkTokens.Contains(token)) * 0.03;
        var phraseBoost = chunk.Text.Contains(query, StringComparison.OrdinalIgnoreCase) ? 0.2 : 0;
        return cosine + overlapBoost + phraseBoost;
    }

    private static double CosineSimilarity(float[] left, float[] right)
    {
        var length = Math.Min(left.Length, right.Length);
        double dot = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;

        for (var i = 0; i < length; i++)
        {
            dot += left[i] * right[i];
            leftMagnitude += left[i] * left[i];
            rightMagnitude += right[i] * right[i];
        }

        if (leftMagnitude <= 0 || rightMagnitude <= 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }

    private async Task<List<DocumentChunk>> LoadChunksUnsafeAsync()
    {
        if (!File.Exists(_indexFilePath))
        {
            return new List<DocumentChunk>();
        }

        await using var stream = File.Open(_indexFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var chunks = await JsonSerializer.DeserializeAsync<List<DocumentChunk>>(stream);
        return chunks ?? new List<DocumentChunk>();
    }

    private async Task SaveChunksUnsafeAsync(List<DocumentChunk> chunks)
    {
        await using var stream = File.Open(_indexFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, chunks, new JsonSerializerOptions { WriteIndented = true });
    }
}
