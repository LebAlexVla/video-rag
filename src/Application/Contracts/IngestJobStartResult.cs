namespace VideoLectureRagAssistant.Application.Contracts;

public sealed record class IngestJobStartResult
{
    public IngestJobStartResult(Guid jobId, IngestJobStatus status)
    {
        if (jobId == Guid.Empty)
            throw new ArgumentException("JobId must not be empty.", nameof(jobId));

        JobId = jobId;
        Status = status;
    }

    public Guid JobId { get; }

    public IngestJobStatus Status { get; }
}
