namespace VideoLectureRagAssistant.Application.Contracts;

public sealed record AudioDownloadResult(
    string SourceUrl,
    string LocalAudioPath,
    string? Title,
    string? Provider);
