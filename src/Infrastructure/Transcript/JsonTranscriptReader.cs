using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VideoLectureRagAssistant.Application.Abstractions;
using VideoLectureRagAssistant.Domain.Entities;

namespace VideoLectureRagAssistant.Infrastructure.Transcript;

public sealed class JsonTranscriptReader : ITranscriptReader
{
    public async Task<VideoLectureRagAssistant.Domain.Entities.Transcript> ReadAsync(
        string transcriptPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transcriptPath))
            throw new ArgumentException("Transcript path is required.", nameof(transcriptPath));

        var fullPath = Path.GetFullPath(transcriptPath.Trim());

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Transcript JSON file was not found.", fullPath);

        await using var stream = File.OpenRead(fullPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var root = document.RootElement;

        var jobId = GetRequiredString(root, "jobId");
        var status = GetRequiredString(root, "status");

        if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Transcript JSON status must be 'success'.");

        var lectureElement = GetRequiredObject(root, "lecture");
        var transcriberElement = GetRequiredObject(root, "transcriber");
        var segmentsElement = GetRequiredArray(root, "segments");

        var sourcePath = GetRequiredString(lectureElement, "sourcePath");
        var sourceFileName = GetRequiredString(lectureElement, "sourceFileName");
        var title = GetRequiredString(lectureElement, "title");
        var language = GetOptionalString(lectureElement, "language");
        var durationSec = GetOptionalDouble(lectureElement, "durationSec");

        var lecture = new Lecture(
            lectureId: BuildLectureId(sourcePath, title),
            title: title,
            sourceFileName: sourceFileName,
            sourcePath: sourcePath,
            language: language,
            durationSec: durationSec);

        var transcript = new VideoLectureRagAssistant.Domain.Entities.Transcript(
            jobId: jobId,
            lecture: lecture,
            transcriberProvider: GetRequiredString(transcriberElement, "provider"),
            transcriberModel: GetRequiredString(transcriberElement, "model"),
            segments: ReadSegments(segmentsElement));

        return transcript;
    }

    private static IReadOnlyList<TranscriptSegment> ReadSegments(JsonElement segmentsElement)
    {
        var segments = new List<TranscriptSegment>();

        foreach (var segmentElement in segmentsElement.EnumerateArray())
        {
            segments.Add(new TranscriptSegment(
                index: GetRequiredInt(segmentElement, "index"),
                startSec: GetRequiredDouble(segmentElement, "startSec"),
                endSec: GetRequiredDouble(segmentElement, "endSec"),
                text: GetRequiredString(segmentElement, "text")));
        }

        return segments;
    }

    private static string BuildLectureId(string sourcePath, string title)
    {
        var raw = $"{sourcePath}|{title}".Trim().ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        var hash = Convert.ToHexString(bytes).ToLowerInvariant();

        return $"lecture-{hash[..16]}";
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"Required string property '{propertyName}' was not found.");

        var value = property.GetString();

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"Required string property '{propertyName}' is empty.");

        return value.Trim();
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Null)
            return null;

        if (property.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"Optional string property '{propertyName}' has invalid type.");

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int GetRequiredInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || !property.TryGetInt32(out var value))
            throw new InvalidDataException($"Required integer property '{propertyName}' was not found.");

        return value;
    }

    private static double GetRequiredDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || !property.TryGetDouble(out var value))
            throw new InvalidDataException($"Required numeric property '{propertyName}' was not found.");

        return value;
    }

    private static double? GetOptionalDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Null)
            return null;

        if (!property.TryGetDouble(out var value))
            throw new InvalidDataException($"Optional numeric property '{propertyName}' has invalid value.");

        return value;
    }

    private static JsonElement GetRequiredObject(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"Required object property '{propertyName}' was not found.");

        return property;
    }

    private static JsonElement GetRequiredArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Required array property '{propertyName}' was not found.");

        return property;
    }
}