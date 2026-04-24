namespace VideoLectureRagAssistant.Infrastructure.Configuration;

public sealed class PathsOptions
{
    public const string SectionName = "Paths";

    public string Videos { get; set; } = string.Empty;

    public string Transcripts { get; set; } = string.Empty;

    public string Jobs { get; set; } = string.Empty;

    public string Registry { get; set; } = string.Empty;
}