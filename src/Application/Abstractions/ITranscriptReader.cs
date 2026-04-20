using VideoLectureRagAssistant.Domain.Entities;

namespace VideoLectureRagAssistant.Application.Abstractions;

public interface ITranscriptReader
{
    Task<Transcript> ReadAsync(
        string transcriptPath,
        CancellationToken cancellationToken = default);
}