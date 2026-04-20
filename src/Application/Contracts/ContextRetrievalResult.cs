using VideoLectureRagAssistant.Domain.Entities;

namespace VideoLectureRagAssistant.Application.Contracts;

public sealed record class ContextRetrievalResult
{
    public ContextRetrievalResult(
        IReadOnlyList<RetrievedContext> context,
        bool hasSufficientContext,
        string? message = null)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        HasSufficientContext = hasSufficientContext;
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
    }

    public IReadOnlyList<RetrievedContext> Context { get; }

    public bool HasSufficientContext { get; }

    public string? Message { get; }
}