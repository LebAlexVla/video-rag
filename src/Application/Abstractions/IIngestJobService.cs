using VideoLectureRagAssistant.Application.Contracts;

namespace VideoLectureRagAssistant.Application.Abstractions;

public interface IIngestJobService
{
    Task<IngestJobStartResult> StartUrlIngestAsync(
        IngestFromUrlRequest request,
        CancellationToken cancellationToken = default);

    IngestJobStatusResult? GetStatus(Guid jobId);
}
