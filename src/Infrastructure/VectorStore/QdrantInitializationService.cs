using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using VideoLectureRagAssistant.Infrastructure.Configuration;

namespace VideoLectureRagAssistant.Infrastructure.VectorStore;

public sealed class QdrantInitializationService : IHostedService
{
    private static readonly string[] SupportedDistances =
    [
        "Cosine",
        "Dot",
        "Euclid",
        "Manhattan"
    ];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly QdrantOptions _options;

    public QdrantInitializationService(
        IHttpClientFactory httpClientFactory,
        IOptions<QdrantOptions> options)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ValidateOptions(_options);

        var client = _httpClientFactory.CreateClient("qdrant");

        var response = await client.GetAsync(
            $"/collections/{_options.CollectionName}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            await CreateCollectionAsync(client, cancellationToken);
            return;
        }

        response.EnsureSuccessStatusCode();

        await ValidateExistingCollectionAsync(response, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task CreateCollectionAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.PutAsJsonAsync(
            $"/collections/{_options.CollectionName}",
            new
            {
                vectors = new
                {
                    size = _options.VectorSize,
                    distance = _options.Distance
                }
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private async Task ValidateExistingCollectionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("result", out var resultElement) ||
            resultElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Qdrant collection details response does not contain result.");
        }

        if (!resultElement.TryGetProperty("config", out var configElement) ||
            configElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Qdrant collection details response does not contain config.");
        }

        if (!configElement.TryGetProperty("params", out var paramsElement) ||
            paramsElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Qdrant collection details response does not contain params.");
        }

        if (!paramsElement.TryGetProperty("vectors", out var vectorsElement))
        {
            throw new InvalidOperationException("Qdrant collection details response does not contain vectors config.");
        }

        var (actualSize, actualDistance) = ReadVectorConfig(vectorsElement);

        if (actualSize != _options.VectorSize)
        {
            throw new InvalidOperationException(
                $"Qdrant collection '{_options.CollectionName}' exists, but vector size is {actualSize} instead of configured {_options.VectorSize}.");
        }

        if (!string.Equals(actualDistance, _options.Distance, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Qdrant collection '{_options.CollectionName}' exists, but distance is '{actualDistance}' instead of configured '{_options.Distance}'.");
        }
    }

    private static (int Size, string Distance) ReadVectorConfig(JsonElement vectorsElement)
    {
        if (vectorsElement.ValueKind == JsonValueKind.Object &&
            vectorsElement.TryGetProperty("size", out var sizeElement) &&
            vectorsElement.TryGetProperty("distance", out var distanceElement))
        {
            return (
                Size: sizeElement.GetInt32(),
                Distance: distanceElement.GetString() ?? throw new InvalidOperationException("Qdrant vector distance is missing.")
            );
        }

        if (vectorsElement.ValueKind == JsonValueKind.Object &&
            vectorsElement.TryGetProperty("default", out var defaultElement) &&
            defaultElement.ValueKind == JsonValueKind.Object &&
            defaultElement.TryGetProperty("size", out var namedSizeElement) &&
            defaultElement.TryGetProperty("distance", out var namedDistanceElement))
        {
            return (
                Size: namedSizeElement.GetInt32(),
                Distance: namedDistanceElement.GetString() ?? throw new InvalidOperationException("Qdrant named vector distance is missing.")
            );
        }

        throw new InvalidOperationException("Unsupported Qdrant vectors configuration format.");
    }

    private static void ValidateOptions(QdrantOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.CollectionName))
            throw new InvalidOperationException("Qdrant collection name is required.");

        if (options.VectorSize <= 0)
            throw new InvalidOperationException("Qdrant vector size must be greater than 0.");

        if (string.IsNullOrWhiteSpace(options.Distance))
            throw new InvalidOperationException("Qdrant distance is required.");

        if (!SupportedDistances.Contains(options.Distance, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Qdrant distance '{options.Distance}' is not supported. Use one of: {string.Join(", ", SupportedDistances)}.");
        }
    }
}