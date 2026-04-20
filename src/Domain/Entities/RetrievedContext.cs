namespace VideoLectureRagAssistant.Domain.Entities;

public sealed class RetrievedContext
{
    public RetrievedContext(
        string chunkId,
        string lectureId,
        string lectureTitle,
        int chunkIndex,
        string text,
        int approxMinute,
        double score,
        double? approxStartSec = null,
        double? approxEndSec = null)
    {
        if (string.IsNullOrWhiteSpace(chunkId))
            throw new ArgumentException("ChunkId is required.", nameof(chunkId));

        if (string.IsNullOrWhiteSpace(lectureId))
            throw new ArgumentException("LectureId is required.", nameof(lectureId));

        if (string.IsNullOrWhiteSpace(lectureTitle))
            throw new ArgumentException("LectureTitle is required.", nameof(lectureTitle));

        if (chunkIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(chunkIndex), "ChunkIndex must be greater than or equal to 0.");

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text is required.", nameof(text));

        if (approxMinute < 0)
            throw new ArgumentOutOfRangeException(nameof(approxMinute), "ApproxMinute must be greater than or equal to 0.");

        if (double.IsNaN(score) || double.IsInfinity(score))
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be a finite number.");

        if (approxStartSec.HasValue && approxStartSec.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(approxStartSec), "ApproxStartSec must be greater than or equal to 0.");

        if (approxStartSec.HasValue && approxEndSec.HasValue && approxEndSec.Value <= approxStartSec.Value)
            throw new ArgumentOutOfRangeException(nameof(approxEndSec), "ApproxEndSec must be greater than ApproxStartSec.");

        ChunkId = chunkId.Trim();
        LectureId = lectureId.Trim();
        LectureTitle = lectureTitle.Trim();
        ChunkIndex = chunkIndex;
        Text = text.Trim();
        ApproxMinute = approxMinute;
        Score = score;
        ApproxStartSec = approxStartSec;
        ApproxEndSec = approxEndSec;
    }

    public string ChunkId { get; }

    public string LectureId { get; }

    public string LectureTitle { get; }

    public int ChunkIndex { get; }

    public string Text { get; }

    public int ApproxMinute { get; }

    public double Score { get; }

    public double? ApproxStartSec { get; }

    public double? ApproxEndSec { get; }
}