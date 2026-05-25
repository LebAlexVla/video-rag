namespace VideoLectureRagAssistant.Application.Contracts;

public sealed record class IngestJobStatusResult
{
    public IngestJobStatusResult(
        Guid jobId,
        IngestJobStatus status,
        IngestJobStage stage,
        string? message = null,
        string? sourceUrl = null,
        string? localAudioPath = null,
        string? lectureId = null,
        string? lectureTitle = null,
        string? transcriptPath = null,
        int chunkCount = 0,
        ErrorInfo? error = null)
    {
        if (jobId == Guid.Empty)
            throw new ArgumentException("JobId must not be empty.", nameof(jobId));

        if (chunkCount < 0)
            throw new ArgumentOutOfRangeException(nameof(chunkCount), "ChunkCount must be greater than or equal to 0.");

        JobId = jobId;
        Status = status;
        Stage = stage;
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        SourceUrl = string.IsNullOrWhiteSpace(sourceUrl) ? null : sourceUrl.Trim();
        LocalAudioPath = string.IsNullOrWhiteSpace(localAudioPath) ? null : localAudioPath.Trim();
        LectureId = string.IsNullOrWhiteSpace(lectureId) ? null : lectureId.Trim();
        LectureTitle = string.IsNullOrWhiteSpace(lectureTitle) ? null : lectureTitle.Trim();
        TranscriptPath = string.IsNullOrWhiteSpace(transcriptPath) ? null : transcriptPath.Trim();
        ChunkCount = chunkCount;
        Error = error;
    }

    public Guid JobId { get; }

    public IngestJobStatus Status { get; }

    public IngestJobStage Stage { get; }

    public string? Message { get; }

    public string? SourceUrl { get; }

    public string? LocalAudioPath { get; }

    public string? LectureId { get; }

    public string? LectureTitle { get; }

    public string? TranscriptPath { get; }

    public int ChunkCount { get; }

    public ErrorInfo? Error { get; }
}
