using VideoLectureRagAssistant.Domain.Entities;

namespace VideoLectureRagAssistant.Application.Contracts;

public sealed record class AskResponse
{
    public AskResponse(
        string? answer,
        bool usedContext,
        IReadOnlyList<SourceCitation> sources,
        string? message = null)
    {
        if (usedContext && string.IsNullOrWhiteSpace(answer))
            throw new ArgumentException("Answer must be provided when UsedContext is true.", nameof(answer));

        Answer = string.IsNullOrWhiteSpace(answer) ? null : answer.Trim();
        UsedContext = usedContext;
        Sources = sources ?? throw new ArgumentNullException(nameof(sources));
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
    }

    public string? Answer { get; }

    public bool UsedContext { get; }

    public IReadOnlyList<SourceCitation> Sources { get; }

    public string? Message { get; }
}