using System.Net.Http.Json;
using System.Text.Json;
using VideoLectureRagAssistant.Application.Abstractions;

namespace VideoLectureRagAssistant.Infrastructure.Embeddings;

public sealed class GeminiEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _modelName;
    private readonly int _outputDimensionality;

    public GeminiEmbeddingProvider(
        HttpClient httpClient,
        string apiKey,
        string modelName,
        int outputDimensionality)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key is required.", nameof(apiKey));

        if (string.IsNullOrWhiteSpace(modelName))
            throw new ArgumentException("Model name is required.", nameof(modelName));

        if (outputDimensionality <= 0)
            throw new ArgumentOutOfRangeException(nameof(outputDimensionality));

        _apiKey = apiKey.Trim();
        _modelName = modelName.Trim();
        _outputDimensionality = outputDimensionality;
    }

    public string ProviderName => "gemini";

    public string ModelName => _modelName;

    public Task<float[]> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        // Questions are embedded as retrieval queries for optimal RAG semantic search
        return EmbedSingleAsync(text, "RETRIEVAL_QUERY", cancellationToken);
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
            return Array.Empty<float[]>();

        // Documents are embedded as retrieval documents for optimal RAG semantic search
        var url = $"/v1beta/models/{_modelName}:batchEmbedContents";

        var requests = texts
            .Select(text => new
            {
                model = $"models/{_modelName}",
                content = new { parts = new[] { new { text = NormalizeText(text) } } },
                taskType = "RETRIEVAL_DOCUMENT",
                outputDimensionality = _outputDimensionality
            })
            .ToArray();

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new { requests })
        };

        httpRequest.Headers.Add("x-goog-api-key", _apiKey);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, "Gemini batch embeddings request", cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("embeddings", out var embeddingsElement) ||
            embeddingsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Gemini response does not contain embeddings array.");
        }

        var result = embeddingsElement
            .EnumerateArray()
            .Select(item => ReadFloatArray(item.GetProperty("values")))
            .ToArray();

        if (result.Length != texts.Count)
            throw new InvalidOperationException("Gemini returned unexpected embeddings count.");

        return result;
    }

    private async Task<float[]> EmbedSingleAsync(
        string text,
        string taskType,
        CancellationToken cancellationToken)
    {
        var url = $"/v1beta/models/{_modelName}:embedContent";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                model = $"models/{_modelName}",
                content = new { parts = new[] { new { text = NormalizeText(text) } } },
                taskType,
                outputDimensionality = _outputDimensionality
            })
        };

        httpRequest.Headers.Add("x-goog-api-key", _apiKey);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, "Gemini embedding request", cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("embedding", out var embeddingElement) ||
            !embeddingElement.TryGetProperty("values", out var valuesElement))
        {
            throw new InvalidOperationException("Gemini response does not contain embedding values.");
        }

        return ReadFloatArray(valuesElement);
    }

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text is required.", nameof(text));

        return text.Trim();
    }

    private static float[] ReadFloatArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Embedding values must be an array.");

        var vector = new float[element.GetArrayLength()];
        var index = 0;

        foreach (var value in element.EnumerateArray())
        {
            if (!value.TryGetSingle(out var number))
                throw new InvalidOperationException("Embedding value is not a valid float.");

            vector[index++] = number;
        }

        return vector;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string providerOperation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

        throw new InvalidOperationException(
            $"{providerOperation} failed: {(int)response.StatusCode} {response.ReasonPhrase}. {errorBody}");
    }
}
