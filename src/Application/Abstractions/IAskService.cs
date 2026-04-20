using VideoLectureRagAssistant.Application.Contracts;

namespace VideoLectureRagAssistant.Application.Abstractions;

public interface IAskService
{
    Task<AskResponse> AskAsync(
        AskRequest request,
        CancellationToken cancellationToken = default);
}