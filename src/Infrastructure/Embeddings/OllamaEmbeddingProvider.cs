using System.Net.Http.Json;
using System.Text.Json;
using VideoLectureRagAssistant.Application.Abstractions;

namespace VideoLectureRagAssistant.Infrastructure.Embeddings;

public sealed class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName;

    public OllamaEmbeddingProvider(HttpClient httpClient, string modelName)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        if (string.IsNullOrWhiteSpace(modelName))
            throw new ArgumentException("Model name is required.", nameof(modelName));

        _modelName = modelName.Trim();
    }

    public string ProviderName => "ollama";

    public string ModelName => _modelName;

    public async Task<float[]> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var embeddings = await EmbedInternalAsync(new[] { NormalizeText(text) }, cancellationToken);
        return embeddings[0];
    }

    public Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
            return Task.FromResult<IReadOnlyList<float[]>>(Array.Empty<float[]>());

        var normalized = texts.Select(NormalizeText).ToArray();
        return EmbedInternalAsync(normalized, cancellationToken);
    }

    private async Task<IReadOnlyList<float[]>> EmbedInternalAsync(
        IReadOnlyList<string> input,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/api/embed",
            new
            {
                model = _modelName,
                input
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("embeddings", out var embeddingsElement) ||
            embeddingsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Ollama response does not contain embeddings array.");
        }

        var result = embeddingsElement
            .EnumerateArray()
            .Select(ReadFloatArray)
            .ToArray();

        if (result.Length != input.Count)
            throw new InvalidOperationException("Ollama returned unexpected embeddings count.");

        return result;
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
            throw new InvalidOperationException("Embedding item must be an array.");

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
}