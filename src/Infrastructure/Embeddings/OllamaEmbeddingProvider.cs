using System.Net;
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
        var normalized = NormalizeText(text);

        var batch = await EmbedBatchAsync(new[] { normalized }, cancellationToken);
        return batch[0];
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
            return Array.Empty<float[]>();

        var normalized = texts.Select(NormalizeText).ToArray();

        var result = await TryEmbedWithNewApiAsync(normalized, cancellationToken);

        if (result is not null)
            return result;

        result = await TryEmbedWithLegacyApiAsync(normalized, cancellationToken);

        if (result is not null)
            return result;

        throw new InvalidOperationException(
            "Ollama embeddings request failed: neither /api/embed nor /api/embeddings is available.");
    }

    private async Task<IReadOnlyList<float[]>?> TryEmbedWithNewApiAsync(
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

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

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

    private async Task<IReadOnlyList<float[]>?> TryEmbedWithLegacyApiAsync(
        IReadOnlyList<string> input,
        CancellationToken cancellationToken)
    {
        var result = new List<float[]>(input.Count);

        foreach (var text in input)
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "/api/embeddings",
                new
                {
                    model = _modelName,
                    prompt = text
                },
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("embedding", out var embeddingElement) ||
                embeddingElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Ollama legacy response does not contain embedding array.");
            }

            result.Add(ReadFloatArray(embeddingElement));
        }

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