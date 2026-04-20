namespace VideoLectureRagAssistant.Application.Contracts;

public sealed record class LectureIngestRequest
{
    public LectureIngestRequest(
        string inputPath,
        string? requestedTitle = null,
        string? languageHint = null,
        string transcriptionProvider = "",
        string transcriptionModel = "",
        bool overwrite = true)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
            throw new ArgumentException("InputPath is required.", nameof(inputPath));

        if (!string.IsNullOrWhiteSpace(requestedTitle) && string.IsNullOrWhiteSpace(requestedTitle.Trim()))
            throw new ArgumentException("RequestedTitle must not be empty when provided.", nameof(requestedTitle));

        if (string.IsNullOrWhiteSpace(transcriptionProvider))
            throw new ArgumentException("TranscriptionProvider is required.", nameof(transcriptionProvider));

        if (string.IsNullOrWhiteSpace(transcriptionModel))
            throw new ArgumentException("TranscriptionModel is required.", nameof(transcriptionModel));

        InputPath = inputPath.Trim();
        RequestedTitle = string.IsNullOrWhiteSpace(requestedTitle) ? null : requestedTitle.Trim();
        LanguageHint = string.IsNullOrWhiteSpace(languageHint) ? null : languageHint.Trim();
        TranscriptionProvider = transcriptionProvider.Trim();
        TranscriptionModel = transcriptionModel.Trim();
        Overwrite = overwrite;
    }

    public string InputPath { get; }

    public string? RequestedTitle { get; }

    public string? LanguageHint { get; }

    public string TranscriptionProvider { get; }

    public string TranscriptionModel { get; }

    public bool Overwrite { get; }
}