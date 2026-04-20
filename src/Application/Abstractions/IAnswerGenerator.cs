using VideoLectureRagAssistant.Application.Contracts;
using VideoLectureRagAssistant.Domain.Entities;

namespace VideoLectureRagAssistant.Application.Abstractions;

public interface IAnswerGenerator
{
    Task<AnswerResult> GenerateAsync(
        AskRequest request,
        IReadOnlyList<RetrievedContext> context,
        CancellationToken cancellationToken = default);
}