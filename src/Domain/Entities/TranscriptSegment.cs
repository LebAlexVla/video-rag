namespace VideoLectureRagAssistant.Domain.Entities;

public sealed record class TranscriptSegment
{
    public TranscriptSegment(
        int index,
        double startSec,
        double endSec,
        string text)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be greater than or equal to 0.");

        if (startSec < 0)
            throw new ArgumentOutOfRangeException(nameof(startSec), "StartSec must be greater than or equal to 0.");

        if (endSec <= startSec)
            throw new ArgumentOutOfRangeException(nameof(endSec), "EndSec must be greater than StartSec.");

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text is required.", nameof(text));

        Index = index;
        StartSec = startSec;
        EndSec = endSec;
        Text = text.Trim();
    }

    public int Index { get; }

    public double StartSec { get; }

    public double EndSec { get; }

    public string Text { get; }
}