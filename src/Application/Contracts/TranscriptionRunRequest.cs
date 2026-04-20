namespace VideoLectureRagAssistant.Application.Contracts;

public sealed record class TranscriptionRunRequest
{
    public TranscriptionRunRequest(
        string jobId,
        string inputVideoPath,
        string outputTranscriptPath,
        string transcriptionProvider,
        string transcriptionModel,
        bool overwrite,
        string? requestedTitle = null,
        string? languageHint = null)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new ArgumentException("JobId is required.", nameof(jobId));

        if (string.IsNullOrWhiteSpace(inputVideoPath))
            throw new ArgumentException("InputVideoPath is required.", nameof(inputVideoPath));

        if (string.IsNullOrWhiteSpace(outputTranscriptPath))
            throw new ArgumentException("OutputTranscriptPath is required.", nameof(outputTranscriptPath));

        if (string.IsNullOrWhiteSpace(transcriptionProvider))
            throw new ArgumentException("TranscriptionProvider is required.", nameof(transcriptionProvider));

        if (string.IsNullOrWhiteSpace(transcriptionModel))
            throw new ArgumentException("TranscriptionModel is required.", nameof(transcriptionModel));

        JobId = jobId.Trim();
        InputVideoPath = inputVideoPath.Trim();
        OutputTranscriptPath = outputTranscriptPath.Trim();
        RequestedTitle = string.IsNullOrWhiteSpace(requestedTitle) ? null : requestedTitle.Trim();
        LanguageHint = string.IsNullOrWhiteSpace(languageHint) ? null : languageHint.Trim();
        TranscriptionProvider = transcriptionProvider.Trim();
        TranscriptionModel = transcriptionModel.Trim();
        Overwrite = overwrite;
    }

    public string JobId { get; }

    public string InputVideoPath { get; }

    public string OutputTranscriptPath { get; }

    public string? RequestedTitle { get; }

    public string? LanguageHint { get; }

    public string TranscriptionProvider { get; }

    public string TranscriptionModel { get; }

    public bool Overwrite { get; }
}