using VideoLectureRagAssistant.Application.Contracts;

namespace VideoLectureRagAssistant.Application.Abstractions;

public interface IContextRetriever
{
    Task<ContextRetrievalResult> RetrieveAsync(
        AskRequest request,
        CancellationToken cancellationToken = default);
}