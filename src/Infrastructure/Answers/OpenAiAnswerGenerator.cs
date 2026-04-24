using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using VideoLectureRagAssistant.Application.Abstractions;
using VideoLectureRagAssistant.Application.Contracts;
using VideoLectureRagAssistant.Domain.Entities;

namespace VideoLectureRagAssistant.Infrastructure.Answers;

public sealed class OpenAiAnswerGenerator : IAnswerGenerator
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _modelName;

    public OpenAiAnswerGenerator(
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

    public async Task<AnswerResult> GenerateAsync(
        AskRequest request,
        IReadOnlyList<RetrievedContext> context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Count == 0)
            return CreateFallback();

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(new
            {
                model = _modelName,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "Ты отвечаешь только на основе предоставленного контекста по лекциям. Не добавляй факты вне контекста."
                    },
                    new
                    {
                        role = "user",
                        content = BuildUserMessage(request.Question, context)
                    }
                },
                temperature = 0
            })
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("choices", out var choicesElement) ||
            choicesElement.ValueKind != JsonValueKind.Array ||
            choicesElement.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("OpenAI response does not contain choices.");
        }

        var messageElement = choicesElement[0].GetProperty("message");

        if (!messageElement.TryGetProperty("content", out var contentElement) ||
            contentElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("OpenAI response does not contain message content.");
        }

        var answer = contentElement.GetString()?.Trim();

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

    private static string BuildUserMessage(string question, IReadOnlyList<RetrievedContext> context)
    {
        var builder = new StringBuilder();

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

        builder.AppendLine("Сформируй точный ответ только по этому контексту.");

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