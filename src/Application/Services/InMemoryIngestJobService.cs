using System.Collections.Concurrent;
using VideoLectureRagAssistant.Application.Abstractions;
using VideoLectureRagAssistant.Application.Contracts;

namespace VideoLectureRagAssistant.Application.Services;

public sealed class InMemoryIngestJobService : IIngestJobService
{
    private readonly IIngestFromUrlService _ingestFromUrlService;
    private readonly ConcurrentDictionary<Guid, IngestJobStatusResult> _jobs = new();

    public InMemoryIngestJobService(IIngestFromUrlService ingestFromUrlService)
    {
        _ingestFromUrlService = ingestFromUrlService ?? throw new ArgumentNullException(nameof(ingestFromUrlService));
    }

    public Task<IngestJobStartResult> StartUrlIngestAsync(
        IngestFromUrlRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var jobId = Guid.NewGuid();

        SetState(new IngestJobStatusResult(
            jobId: jobId,
            status: IngestJobStatus.Queued,
            stage: IngestJobStage.Created,
            message: "Ingest job was queued.",
            sourceUrl: request.Url));

        _ = Task.Run(() => RunJobAsync(jobId, request), CancellationToken.None);

        return Task.FromResult(new IngestJobStartResult(jobId, IngestJobStatus.Queued));
    }

    public IngestJobStatusResult? GetStatus(Guid jobId)
    {
        return _jobs.TryGetValue(jobId, out var state) ? state : null;
    }

    private async Task RunJobAsync(Guid jobId, IngestFromUrlRequest request)
    {
        SetState(new IngestJobStatusResult(
            jobId: jobId,
            status: IngestJobStatus.Running,
            stage: IngestJobStage.DownloadingAudioAndIngesting,
            message: "Downloading audio and running ingest pipeline.",
            sourceUrl: request.Url));

        try
        {
            var result = await _ingestFromUrlService.IngestAsync(request, CancellationToken.None);

            if (result.Success)
            {
                SetState(new IngestJobStatusResult(
                    jobId: jobId,
                    status: IngestJobStatus.Succeeded,
                    stage: IngestJobStage.Completed,
                    message: "Lecture was ingested successfully.",
                    sourceUrl: result.SourceUrl,
                    localAudioPath: result.LocalAudioPath,
                    lectureId: result.LectureId,
                    lectureTitle: result.LectureTitle,
                    transcriptPath: result.TranscriptPath,
                    chunkCount: result.ChunkCount));

                return;
            }

            SetState(new IngestJobStatusResult(
                jobId: jobId,
                status: IngestJobStatus.Failed,
                stage: IngestJobStage.Failed,
                message: "URL ingest failed.",
                sourceUrl: result.SourceUrl ?? request.Url,
                localAudioPath: result.LocalAudioPath,
                error: result.Error ?? new ErrorInfo(
                    code: "url_ingest_failed",
                    message: "URL ingest failed without error details.")));
        }
        catch (Exception ex)
        {
            SetState(new IngestJobStatusResult(
                jobId: jobId,
                status: IngestJobStatus.Failed,
                stage: IngestJobStage.Failed,
                message: "URL ingest failed with an unexpected error.",
                sourceUrl: request.Url,
                error: new ErrorInfo(
                    code: "url_ingest_unhandled_exception",
                    message: "URL ingest failed with an unexpected error.",
                    details: new Dictionary<string, string>
                    {
                        ["exceptionType"] = ex.GetType().Name,
                        ["exceptionMessage"] = ex.Message
                    })));
        }
    }

    private void SetState(IngestJobStatusResult state)
    {
        _jobs[state.JobId] = state;
    }
}
