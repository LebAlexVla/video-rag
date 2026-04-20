namespace VideoLectureRagAssistant.Domain.Entities;

public sealed class AnswerResult
{
    public AnswerResult(
        string? answer,
        IReadOnlyList<SourceCitation> sources,
        bool usedContext,
        string? message = null)
    {
        if (usedContext && string.IsNullOrWhiteSpace(answer))
            throw new ArgumentException("Answer must be provided when UsedContext is true.", nameof(answer));

        Sources = sources ?? throw new ArgumentNullException(nameof(sources));
        Answer = string.IsNullOrWhiteSpace(answer) ? null : answer.Trim();
        UsedContext = usedContext;
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
    }

    public string? Answer { get; }

    public bool UsedContext { get; }

    public IReadOnlyList<SourceCitation> Sources { get; }

    public string? Message { get; }
}