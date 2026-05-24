namespace VideoLectureRagAssistant.Application.Contracts;

public sealed record class IngestFromUrlRequest
{
    public IngestFromUrlRequest(
        string url,
        string? requestedTitle = null,
        string? languageHint = null,
        string transcriptionProvider = "faster-whisper",
        string transcriptionModel = "small",
        bool overwrite = true)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Url is required.", nameof(url));

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var parsedUrl))
            throw new ArgumentException("Url must be an absolute URI.", nameof(url));

        if (parsedUrl.Scheme != Uri.UriSchemeHttp && parsedUrl.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Url must use http or https scheme.", nameof(url));

        if (!string.IsNullOrWhiteSpace(requestedTitle) && string.IsNullOrWhiteSpace(requestedTitle.Trim()))
            throw new ArgumentException("RequestedTitle must not be empty when provided.", nameof(requestedTitle));

        if (string.IsNullOrWhiteSpace(transcriptionProvider))
            throw new ArgumentException("TranscriptionProvider is required.", nameof(transcriptionProvider));

        if (string.IsNullOrWhiteSpace(transcriptionModel))
            throw new ArgumentException("TranscriptionModel is required.", nameof(transcriptionModel));

        Url = parsedUrl.ToString();
        RequestedTitle = string.IsNullOrWhiteSpace(requestedTitle) ? null : requestedTitle.Trim();
        LanguageHint = string.IsNullOrWhiteSpace(languageHint) ? null : languageHint.Trim();
        TranscriptionProvider = transcriptionProvider.Trim();
        TranscriptionModel = transcriptionModel.Trim();
        Overwrite = overwrite;
    }

    public string Url { get; }

    public string? RequestedTitle { get; }

    public string? LanguageHint { get; }

    public string TranscriptionProvider { get; }

    public string TranscriptionModel { get; }

    public bool Overwrite { get; }
}
