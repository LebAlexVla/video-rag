namespace VideoLectureRagAssistant.Domain.Entities;

public sealed class Transcript
{
    public Transcript(
        string jobId,
        Lecture lecture,
        string transcriberProvider,
        string transcriberModel,
        IReadOnlyList<TranscriptSegment> segments)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new ArgumentException("JobId is required.", nameof(jobId));

        Lecture = lecture ?? throw new ArgumentNullException(nameof(lecture));

        if (string.IsNullOrWhiteSpace(transcriberProvider))
            throw new ArgumentException("TranscriberProvider is required.", nameof(transcriberProvider));

        if (string.IsNullOrWhiteSpace(transcriberModel))
            throw new ArgumentException("TranscriberModel is required.", nameof(transcriberModel));

        if (segments is null)
            throw new ArgumentNullException(nameof(segments));

        if (segments.Count == 0)
            throw new ArgumentException("Transcript must contain at least one segment.", nameof(segments));

        ValidateSegments(segments);

        JobId = jobId.Trim();
        TranscriberProvider = transcriberProvider.Trim();
        TranscriberModel = transcriberModel.Trim();
        Segments = segments;
    }

    public string JobId { get; }

    public Lecture Lecture { get; }

    public string TranscriberProvider { get; }

    public string TranscriberModel { get; }

    public IReadOnlyList<TranscriptSegment> Segments { get; }

    private static void ValidateSegments(IReadOnlyList<TranscriptSegment> segments)
    {
        for (var i = 0; i < segments.Count; i++)
        {
            var current = segments[i];

            if (current.Index != i)
                throw new ArgumentException("Transcript segments must be ordered by Index without gaps.", nameof(segments));

            if (i > 0)
            {
                var previous = segments[i - 1];
                if (current.StartSec < previous.StartSec)
                    throw new ArgumentException("Transcript segment time boundaries must not go backwards.", nameof(segments));
            }
        }
    }
}