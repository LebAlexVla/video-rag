namespace VideoLectureRagAssistant.Application.Contracts;

public sealed record class LectureIngestResult
{
    public LectureIngestResult(
        bool success,
        string? lectureId = null,
        string? lectureTitle = null,
        string? transcriptPath = null,
        int chunkCount = 0,
        ErrorInfo? error = null)
    {
        if (success)
        {
            if (string.IsNullOrWhiteSpace(lectureId))
                throw new ArgumentException("LectureId is required for successful ingest.", nameof(lectureId));

            if (string.IsNullOrWhiteSpace(lectureTitle))
                throw new ArgumentException("LectureTitle is required for successful ingest.", nameof(lectureTitle));

            if (string.IsNullOrWhiteSpace(transcriptPath))
                throw new ArgumentException("TranscriptPath is required for successful ingest.", nameof(transcriptPath));

            if (chunkCount < 0)
                throw new ArgumentOutOfRangeException(nameof(chunkCount), "ChunkCount must be greater than or equal to 0.");
        }

        Success = success;
        LectureId = string.IsNullOrWhiteSpace(lectureId) ? null : lectureId.Trim();
        LectureTitle = string.IsNullOrWhiteSpace(lectureTitle) ? null : lectureTitle.Trim();
        TranscriptPath = string.IsNullOrWhiteSpace(transcriptPath) ? null : transcriptPath.Trim();
        ChunkCount = chunkCount;
        Error = error;
    }

    public bool Success { get; }

    public string? LectureId { get; }

    public string? LectureTitle { get; }

    public string? TranscriptPath { get; }

    public int ChunkCount { get; }

    public ErrorInfo? Error { get; }
}