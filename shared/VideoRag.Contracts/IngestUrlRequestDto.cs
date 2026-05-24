namespace VideoRag.Contracts;

public sealed record IngestUrlRequestDto(
    string Url,
    string? LectureTitle = null,
    string? LanguageHint = null,
    string TranscriptionProvider = "faster-whisper",
    string TranscriptionModel = "small",
    bool Overwrite = true
);
