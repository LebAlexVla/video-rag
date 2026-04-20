using VideoLectureRagAssistant.Application.Contracts;

namespace VideoLectureRagAssistant.Application.Abstractions;

public interface ITranscriptionRunner
{
    Task<TranscriptionRunResult> RunAsync(
        TranscriptionRunRequest request,
        CancellationToken cancellationToken = default);
}