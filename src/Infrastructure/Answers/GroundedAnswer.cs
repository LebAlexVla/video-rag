using System.Text;
using System.Text.Json;
using VideoLectureRagAssistant.Domain.Entities;

namespace VideoLectureRagAssistant.Infrastructure.Answers;

internal static class GroundedAnswer
{
    private const string FallbackMessage =
        "Недостаточно релевантного контекста для уверенного ответа по загруженным лекциям.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string BuildSystemMessage()
    {
        return
            "Ты RAG-модуль ответа по лекциям. " +
            "Отвечай только по предоставленному контексту. " +
            "Не используй внешние знания. " +
            "Если в контексте нет прямого ответа на вопрос, верни canAnswer=false. " +
            "Верни только валидный JSON без Markdown и без пояснений.";
    }

    public static string BuildPrompt(
        string question,
        IReadOnlyList<RetrievedContext> context)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Верни JSON строго одного из двух форматов.");
        builder.AppendLine();
        builder.AppendLine("Если в контексте есть прямой ответ на вопрос:");
        builder.AppendLine("""{"canAnswer":true,"answer":"краткий точный ответ","usedSourceIndexes":[1]}""");
        builder.AppendLine();
        builder.AppendLine("Если в контексте нет прямого ответа на вопрос:");
        builder.AppendLine("""{"canAnswer":false,"answer":null,"usedSourceIndexes":[]}""");
        builder.AppendLine();
        builder.AppendLine("Правила:");
        builder.AppendLine("- canAnswer=true только если ответ прямо следует из предоставленного контекста.");
        builder.AppendLine("- Если контекст похож по теме, но не содержит ответа, верни canAnswer=false.");
        builder.AppendLine("- Не используй внешние знания.");
        builder.AppendLine("- usedSourceIndexes — номера фрагментов, реально использованных для ответа.");
        builder.AppendLine("- При canAnswer=false поле usedSourceIndexes должно быть пустым.");
        builder.AppendLine("- Не добавляй Markdown, ```json или текст вокруг JSON.");
        builder.AppendLine();
        builder.AppendLine("Вопрос:");
        builder.AppendLine(question);
        builder.AppendLine();
        builder.AppendLine("Контекст:");

        for (var i = 0; i < context.Count; i++)
        {
            var item = context[i];

            builder.AppendLine(
                $"[{i + 1}] Лекция: {item.LectureTitle}; Чанк: {item.ChunkIndex}; Минута: {item.ApproxMinute}");

            if (item.ApproxStartSec.HasValue && item.ApproxEndSec.HasValue)
            {
                builder.AppendLine(
                    $"Время: {item.ApproxStartSec.Value:0.##}–{item.ApproxEndSec.Value:0.##} сек.");
            }

            builder.AppendLine(item.Text);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    public static AnswerResult FromModelResponse(
        string? rawResponse,
        IReadOnlyList<RetrievedContext> context)
    {
        var payload = TryParsePayload(rawResponse);

        if (payload is null)
            return CreateFallback();

        if (!payload.CanAnswer)
            return CreateFallback();

        if (string.IsNullOrWhiteSpace(payload.Answer))
            return CreateFallback();

        var sources = BuildSources(context, payload.UsedSourceIndexes);

        if (sources.Count == 0)
            return CreateFallback();

        return new AnswerResult(
            answer: payload.Answer.Trim(),
            sources: sources,
            usedContext: true);
    }

    public static AnswerResult CreateFallback()
    {
        return new AnswerResult(
            answer: null,
            sources: Array.Empty<SourceCitation>(),
            usedContext: false,
            message: FallbackMessage);
    }

    private static Payload? TryParsePayload(string? rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
            return null;

        var text = rawResponse.Trim();

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        if (start < 0 || end < start)
            return null;

        var json = text[start..(end + 1)];

        try
        {
            return JsonSerializer.Deserialize<Payload>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<SourceCitation> BuildSources(
        IReadOnlyList<RetrievedContext> context,
        IReadOnlyList<int>? usedSourceIndexes)
    {
        if (usedSourceIndexes is null || usedSourceIndexes.Count == 0)
            return Array.Empty<SourceCitation>();

        return usedSourceIndexes
            .Where(index => index >= 1 && index <= context.Count)
            .Distinct()
            .Select(index => context[index - 1])
            .Select(item => new SourceCitation(
                lectureTitle: item.LectureTitle,
                chunkIndex: item.ChunkIndex,
                approxMinute: item.ApproxMinute,
                approxStartSec: item.ApproxStartSec,
                approxEndSec: item.ApproxEndSec))
            .ToArray();
    }

    private sealed class Payload
    {
        public bool CanAnswer { get; init; }

        public string? Answer { get; init; }

        public int[] UsedSourceIndexes { get; init; } = [];
    }
}