using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using VideoLectureRagAssistant.Application.Abstractions;

namespace VideoLectureRagAssistant.Infrastructure.Embeddings;

public sealed class OpenAiEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _modelName;

    public OpenAiEmbeddingProvider(
        HttpClient httpClient,
        string apiKey,
        string modelName)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key is required.", nameof(apiKey));

        if (string.IsNullOrWhiteSpace(modelName))
            throw new ArgumentException("Model name is required.", nameof(modelName));

        _apiKey = apiKey.Trim();
        _modelName = modelName.Trim();
    }

    public string ProviderName => "openai";

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
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/embeddings")
        {
            Content = JsonContent.Create(new
            {
                model = _modelName,
                input
            })
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("data", out var dataElement) ||
            dataElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("OpenAI response does not contain data array.");
        }

        var result = dataElement
            .EnumerateArray()
            .Select(item => new
            {
                Index = item.GetProperty("index").GetInt32(),
                Vector = ReadFloatArray(item.GetProperty("embedding"))
            })
            .OrderBy(x => x.Index)
            .Select(x => x.Vector)
            .ToArray();

        if (result.Length != input.Count)
            throw new InvalidOperationException("OpenAI returned unexpected embeddings count.");

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