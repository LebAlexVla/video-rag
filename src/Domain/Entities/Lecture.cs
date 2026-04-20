namespace VideoLectureRagAssistant.Domain.Entities;

public sealed class Lecture
{
    public Lecture(
        string lectureId,
        string title,
        string sourceFileName,
        string sourcePath,
        string? language = null,
        double? durationSec = null)
    {
        if (string.IsNullOrWhiteSpace(lectureId))
            throw new ArgumentException("LectureId is required.", nameof(lectureId));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        if (string.IsNullOrWhiteSpace(sourceFileName))
            throw new ArgumentException("SourceFileName is required.", nameof(sourceFileName));

        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("SourcePath is required.", nameof(sourcePath));

        if (durationSec.HasValue && durationSec.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationSec), "DurationSec must be greater than 0 when provided.");

        LectureId = lectureId.Trim();
        Title = title.Trim();
        SourceFileName = sourceFileName.Trim();
        SourcePath = sourcePath.Trim();
        Language = string.IsNullOrWhiteSpace(language) ? null : language.Trim();
        DurationSec = durationSec;
    }

    public string LectureId { get; }

    public string Title { get; }

    public string SourceFileName { get; }

    public string SourcePath { get; }

    public string? Language { get; }

    public double? DurationSec { get; }
}