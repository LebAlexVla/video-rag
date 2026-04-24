using VideoLectureRagAssistant.Application.Contracts;
using VideoLectureRagAssistant.Domain.Entities;

namespace VideoLectureRagAssistant.Application.Services;

public sealed class AnswerGenerator
{
    public AskResponse ToAskResponse(AnswerResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new AskResponse(
            answer: result.Answer,
            usedContext: result.UsedContext,
            sources: result.Sources,
            message: result.Message);
    }

    public AskResponse CreateFallback(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Fallback message is required.", nameof(message));

        return new AskResponse(
            answer: null,
            usedContext: false,
            sources: Array.Empty<SourceCitation>(),
            message: message.Trim());
    }
}