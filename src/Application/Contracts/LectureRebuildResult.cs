namespace VideoLectureRagAssistant.Application.Contracts;

public sealed record class LectureRebuildResult
{
    public LectureRebuildResult(
        bool success,
        int rebuiltLectureCount = 0,
        int rebuiltChunkCount = 0,
        ErrorInfo? error = null)
    {
        if (rebuiltLectureCount < 0)
            throw new ArgumentOutOfRangeException(nameof(rebuiltLectureCount), "RebuiltLectureCount must be greater than or equal to 0.");

        if (rebuiltChunkCount < 0)
            throw new ArgumentOutOfRangeException(nameof(rebuiltChunkCount), "RebuiltChunkCount must be greater than or equal to 0.");

        Success = success;
        RebuiltLectureCount = rebuiltLectureCount;
        RebuiltChunkCount = rebuiltChunkCount;
        Error = error;
    }

    public bool Success { get; }

    public int RebuiltLectureCount { get; }

    public int RebuiltChunkCount { get; }

    public ErrorInfo? Error { get; }
}