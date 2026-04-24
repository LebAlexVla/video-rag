using System.Net.Http.Json;
using System.Text.Json;
using VideoLectureRagAssistant.Application.Abstractions;
using VideoLectureRagAssistant.Application.Contracts;
using VideoLectureRagAssistant.Domain.Entities;

namespace VideoLectureRagAssistant.Infrastructure.VectorStore;

public sealed class QdrantVectorStore : IVectorStore
{
    private readonly HttpClient _httpClient;
    private readonly string _collectionName;

    public QdrantVectorStore(HttpClient httpClient, string collectionName)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        if (string.IsNullOrWhiteSpace(collectionName))
            throw new ArgumentException("Collection name is required.", nameof(collectionName));

        _collectionName = collectionName.Trim();
    }

    public async Task UpsertLectureChunksAsync(
        IReadOnlyList<EmbeddedLectureChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        if (chunks.Count == 0)
            return;

        var requestBody = new
        {
            points = chunks.Select(chunk => new
            {
                id = chunk.ChunkId,
                vector = chunk.Vector,
                payload = new Dictionary<string, object?>
                {
                    ["chunkId"] = chunk.ChunkId,
                    ["lectureId"] = chunk.LectureId,
                    ["lectureTitle"] = chunk.LectureTitle,
                    ["chunkIndex"] = chunk.ChunkIndex,
                    ["text"] = chunk.Text,
                    ["approxMinute"] = chunk.ApproxMinute,
                    ["approxStartSec"] = chunk.ApproxStartSec,
                    ["approxEndSec"] = chunk.ApproxEndSec
                }
            })
        };

        using var response = await _httpClient.PutAsJsonAsync(
            $"/collections/{_collectionName}/points?wait=true",
            requestBody,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<RetrievedContext>> SearchAsync(
        float[] queryVector,
        int topK,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryVector);

        if (queryVector.Length == 0)
            throw new ArgumentException("Query vector must not be empty.", nameof(queryVector));

        if (topK <= 0)
            throw new ArgumentOutOfRangeException(nameof(topK), "TopK must be greater than 0.");

        using var response = await _httpClient.PostAsJsonAsync(
            $"/collections/{_collectionName}/points/search",
            new
            {
                vector = queryVector,
                limit = topK,
                with_payload = true
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("result", out var resultElement) ||
            resultElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Qdrant search response does not contain result array.");
        }

        return resultElement
            .EnumerateArray()
            .Select(MapRetrievedContext)
            .ToArray();
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"/collections/{_collectionName}/points/delete?wait=true",
            new
            {
                filter = new
                {
                    must = Array.Empty<object>()
                }
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private static RetrievedContext MapRetrievedContext(JsonElement item)
    {
        var payload = item.GetProperty("payload");

        if (!item.TryGetProperty("score", out var scoreElement) || !scoreElement.TryGetDouble(out var score))
            throw new InvalidOperationException("Qdrant result item does not contain score.");

        return new RetrievedContext(
            chunkId: GetRequiredPayloadString(payload, "chunkId"),
            lectureId: GetRequiredPayloadString(payload, "lectureId"),
            lectureTitle: GetRequiredPayloadString(payload, "lectureTitle"),
            chunkIndex: GetRequiredPayloadInt(payload, "chunkIndex"),
            text: GetRequiredPayloadString(payload, "text"),
            approxMinute: GetRequiredPayloadInt(payload, "approxMinute"),
            score: score,
            approxStartSec: GetOptionalPayloadDouble(payload, "approxStartSec"),
            approxEndSec: GetOptionalPayloadDouble(payload, "approxEndSec"));
    }

    private static string GetRequiredPayloadString(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"Qdrant payload property '{propertyName}' is missing.");

        var value = property.GetString();

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Qdrant payload property '{propertyName}' is empty.");

        return value.Trim();
    }

    private static int GetRequiredPayloadInt(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var property) || !property.TryGetInt32(out var value))
            throw new InvalidOperationException($"Qdrant payload property '{propertyName}' is missing.");

        return value;
    }

    private static double? GetOptionalPayloadDouble(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Null)
            return null;

        if (!property.TryGetDouble(out var value))
            throw new InvalidOperationException($"Qdrant payload property '{propertyName}' has invalid value.");

        return value;
    }
}