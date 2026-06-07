using System.Net.Http.Headers;
using System.Net.Http.Json;
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
            return GroundedAnswer.CreateFallback();

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
                        content = GroundedAnswer.BuildSystemMessage()
                    },
                    new
                    {
                        role = "user",
                        content = GroundedAnswer.BuildPrompt(request.Question, context)
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
            throw new InvalidOperationException("OpenAI-compatible response does not contain choices.");
        }

        var firstChoice = choicesElement[0];

        if (!firstChoice.TryGetProperty("message", out var messageElement) ||
            !messageElement.TryGetProperty("content", out var contentElement) ||
            contentElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("OpenAI-compatible response does not contain message content.");
        }

        var rawAnswer = contentElement.GetString()?.Trim();

        return GroundedAnswer.FromModelResponse(rawAnswer, context);
    }
}