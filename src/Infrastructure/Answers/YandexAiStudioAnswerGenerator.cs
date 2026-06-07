using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using VideoLectureRagAssistant.Application.Abstractions;
using VideoLectureRagAssistant.Application.Contracts;
using VideoLectureRagAssistant.Domain.Entities;

namespace VideoLectureRagAssistant.Infrastructure.Answers;

public sealed class YandexAiStudioAnswerGenerator : IAnswerGenerator
{
    private readonly ChatClient _chatClient;

    public YandexAiStudioAnswerGenerator(
        string apiKey,
        string baseUrl,
        string modelUri)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Yandex API key is required.", nameof(apiKey));

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Yandex base URL is required.", nameof(baseUrl));

        if (string.IsNullOrWhiteSpace(modelUri))
            throw new ArgumentException("Yandex model URI is required.", nameof(modelUri));

        _chatClient = new ChatClient(
            model: modelUri.Trim(),
            credential: new ApiKeyCredential(apiKey.Trim()),
            options: new OpenAIClientOptions
            {
                Endpoint = new Uri(baseUrl.Trim())
            });
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

        var messages = new ChatMessage[]
        {
            new SystemChatMessage(GroundedAnswer.BuildSystemMessage()),
            new UserChatMessage(GroundedAnswer.BuildPrompt(request.Question, context))
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0
        };

        ChatCompletion completion = await _chatClient.CompleteChatAsync(
            messages,
            options,
            cancellationToken);

        var rawAnswer = completion.Content.Count > 0
            ? completion.Content[0].Text
            : null;

        return GroundedAnswer.FromModelResponse(rawAnswer, context);
    }
}