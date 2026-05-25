using VideoLectureRagAssistant.Application.Contracts;

namespace VideoLectureRagAssistant.Application.Abstractions;

public interface IIngestFromUrlService
{
    Task<IngestFromUrlResult> IngestAsync(
        IngestFromUrlRequest request,
        CancellationToken cancellationToken = default);
}
