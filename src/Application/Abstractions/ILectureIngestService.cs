using VideoLectureRagAssistant.Application.Contracts;

namespace VideoLectureRagAssistant.Application.Abstractions;

public interface ILectureIngestService
{
    Task<LectureIngestResult> IngestAsync(
        LectureIngestRequest request,
        CancellationToken cancellationToken = default);

    Task<LectureRebuildResult> RebuildAsync(
        LectureRebuildRequest request,
        CancellationToken cancellationToken = default);
}