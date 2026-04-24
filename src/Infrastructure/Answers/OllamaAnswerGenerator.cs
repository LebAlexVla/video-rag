using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using VideoLectureRagAssistant.Application.Abstractions;
using VideoLectureRagAssistant.Application.Contracts;
using VideoLectureRagAssistant.Domain.Entities;

namespace VideoLectureRagAssistant.Infrastructure.Answers;

public sealed class OllamaAnswerGenerator : IAnswerGenerator
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName;

    public OllamaAnswerGenerator(HttpClient httpClient, string modelName)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        if (string.IsNullOrWhiteSpace(modelName))
            throw new ArgumentException("Model name is required.", nameof(modelName));

        _modelName = modelName.Trim();
    }

    public async Task<AnswerResult> GenerateAsync(
        AskRequest request,
        IReadOnlyList<RetrievedContext> context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Count == 0)
            return CreateFallback();

        using var response = await _httpClient.PostAsJsonAsync(
            "/api/generate",
            new
            {
                model = _modelName,
                prompt = BuildPrompt(request.Question, context),
                stream = false
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("response", out var responseElement) ||
            responseElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Ollama response does not contain generated text.");
        }

        var answer = responseElement.GetString()?.Trim();

        return new AnswerResult(
            answer: answer,
            sources: BuildSources(context),
            usedContext: true);
    }

    private static AnswerResult CreateFallback()
    {
        return new AnswerResult(
            answer: null,
            sources: Array.Empty<SourceCitation>(),
            usedContext: false,
            message: "Недостаточно релевантного контекста для уверенного ответа по загруженным лекциям.");
    }

    private static string BuildPrompt(string question, IReadOnlyList<RetrievedContext> context)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Ты отвечаешь только на основе предоставленного контекста по лекциям.");
        builder.AppendLine("Не добавляй факты вне контекста.");
        builder.AppendLine();
        builder.AppendLine("Вопрос:");
        builder.AppendLine(question);
        builder.AppendLine();
        builder.AppendLine("Контекст:");

        for (var i = 0; i < context.Count; i++)
        {
            var item = context[i];
            builder.AppendLine($"[{i + 1}] Лекция: {item.LectureTitle}; Чанк: {item.ChunkIndex}; Минута: {item.ApproxMinute}");
            builder.AppendLine(item.Text);
            builder.AppendLine();
        }

        builder.AppendLine("Сформируй краткий точный ответ только по контексту.");

        return builder.ToString();
    }

    private static IReadOnlyList<SourceCitation> BuildSources(IReadOnlyList<RetrievedContext> context)
    {
        return context
            .Select(item => new SourceCitation(
                lectureTitle: item.LectureTitle,
                chunkIndex: item.ChunkIndex,
                approxMinute: item.ApproxMinute,
                approxStartSec: item.ApproxStartSec,
                approxEndSec: item.ApproxEndSec))
            .ToArray();
    }
}