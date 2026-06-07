using System.Net.Http.Json;
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
            return GroundedAnswer.CreateFallback();

        var prompt =
            GroundedAnswer.BuildSystemMessage() +
            Environment.NewLine +
            Environment.NewLine +
            GroundedAnswer.BuildPrompt(request.Question, context);

        using var response = await _httpClient.PostAsJsonAsync(
            "/api/generate",
            new
            {
                model = _modelName,
                prompt,
                stream = false,
                format = "json"
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

        var rawAnswer = responseElement.GetString()?.Trim();

        return GroundedAnswer.FromModelResponse(rawAnswer, context);
    }
}