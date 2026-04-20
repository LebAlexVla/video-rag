namespace VideoLectureRagAssistant.Application.Contracts;

public sealed record class TranscriptionRunResult
{
    public TranscriptionRunResult(
        bool success,
        string jobId,
        int exitCode,
        string? transcriptPath = null,
        ErrorInfo? error = null)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new ArgumentException("JobId is required.", nameof(jobId));

        if (success && string.IsNullOrWhiteSpace(transcriptPath))
            throw new ArgumentException("TranscriptPath is required when transcription succeeds.", nameof(transcriptPath));

        JobId = jobId.Trim();
        Success = success;
        ExitCode = exitCode;
        TranscriptPath = string.IsNullOrWhiteSpace(transcriptPath) ? null : transcriptPath.Trim();
        Error = error;
    }

    public bool Success { get; }

    public string JobId { get; }

    public int ExitCode { get; }

    public string? TranscriptPath { get; }

    public ErrorInfo? Error { get; }
}