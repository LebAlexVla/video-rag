namespace VideoLectureRagAssistant.Domain.Entities;

public sealed record class SourceCitation
{
    public SourceCitation(
        string lectureTitle,
        int chunkIndex,
        int approxMinute,
        double? approxStartSec = null,
        double? approxEndSec = null)
    {
        if (string.IsNullOrWhiteSpace(lectureTitle))
            throw new ArgumentException("LectureTitle is required.", nameof(lectureTitle));

        if (chunkIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(chunkIndex), "ChunkIndex must be greater than or equal to 0.");

        if (approxMinute < 0)
            throw new ArgumentOutOfRangeException(nameof(approxMinute), "ApproxMinute must be greater than or equal to 0.");

        if (approxStartSec.HasValue && approxStartSec.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(approxStartSec), "ApproxStartSec must be greater than or equal to 0.");

        if (approxStartSec.HasValue && approxEndSec.HasValue && approxEndSec.Value <= approxStartSec.Value)
            throw new ArgumentOutOfRangeException(nameof(approxEndSec), "ApproxEndSec must be greater than ApproxStartSec.");

        LectureTitle = lectureTitle.Trim();
        ChunkIndex = chunkIndex;
        ApproxMinute = approxMinute;
        ApproxStartSec = approxStartSec;
        ApproxEndSec = approxEndSec;
    }

    public string LectureTitle { get; }

    public int ChunkIndex { get; }

    public int ApproxMinute { get; }

    public double? ApproxStartSec { get; }

    public double? ApproxEndSec { get; }
}