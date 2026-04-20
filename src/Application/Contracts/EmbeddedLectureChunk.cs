namespace VideoLectureRagAssistant.Application.Contracts;

public sealed record class EmbeddedLectureChunk
{
    public EmbeddedLectureChunk(
        string chunkId,
        string lectureId,
        string lectureTitle,
        int chunkIndex,
        string text,
        int approxMinute,
        float[] vector,
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

        if (vector is null)
            throw new ArgumentNullException(nameof(vector));

        if (vector.Length == 0)
            throw new ArgumentException("Vector must not be empty.", nameof(vector));

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
        Vector = vector;
        ApproxStartSec = approxStartSec;
        ApproxEndSec = approxEndSec;
    }

    public string ChunkId { get; }

    public string LectureId { get; }

    public string LectureTitle { get; }

    public int ChunkIndex { get; }

    public string Text { get; }

    public int ApproxMinute { get; }

    public float[] Vector { get; }

    public double? ApproxStartSec { get; }

    public double? ApproxEndSec { get; }
}