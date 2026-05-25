using VideoLectureRagAssistant.Application.Abstractions;
using VideoLectureRagAssistant.Application.Contracts;

namespace VideoLectureRagAssistant.Application.Services;

public sealed class IngestFromUrlService : IIngestFromUrlService
{
    private readonly IAudioSourceUrlClassifier _sourceUrlClassifier;
    private readonly IAudioDownloader _audioDownloader;
    private readonly ILectureIngestService _lectureIngestService;

    public IngestFromUrlService(
        IAudioSourceUrlClassifier sourceUrlClassifier,
        IAudioDownloader audioDownloader,
        ILectureIngestService lectureIngestService)
    {
        _sourceUrlClassifier = sourceUrlClassifier ?? throw new ArgumentNullException(nameof(sourceUrlClassifier));
        _audioDownloader = audioDownloader ?? throw new ArgumentNullException(nameof(audioDownloader));
        _lectureIngestService = lectureIngestService ?? throw new ArgumentNullException(nameof(lectureIngestService));
    }

    public async Task<IngestFromUrlResult> IngestAsync(
        IngestFromUrlRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var sourceUrl = new Uri(request.Url, UriKind.Absolute);

            if (!_sourceUrlClassifier.IsSupported(sourceUrl))
            {
                return new IngestFromUrlResult(
                    success: false,
                    sourceUrl: request.Url,
                    error: new ErrorInfo(
                        code: "unsupported_audio_source_url",
                        message: "Unsupported audio source URL. Supported providers: Rutube and VK."));
            }

            var downloadResult = await _audioDownloader.DownloadAsync(sourceUrl, cancellationToken);

            var ingestRequest = new LectureIngestRequest(
                inputPath: downloadResult.LocalAudioPath,
                requestedTitle: request.RequestedTitle,
                languageHint: request.LanguageHint,
                transcriptionProvider: request.TranscriptionProvider,
                transcriptionModel: request.TranscriptionModel,
                overwrite: request.Overwrite);

            var ingestResult = await _lectureIngestService.IngestAsync(ingestRequest, cancellationToken);

            if (!ingestResult.Success)
            {
                return new IngestFromUrlResult(
                    success: false,
                    sourceUrl: downloadResult.SourceUrl,
                    localAudioPath: downloadResult.LocalAudioPath,
                    error: ingestResult.Error ?? new ErrorInfo(
                        code: "url_ingest_failed",
                        message: "Audio was downloaded, but lecture ingest failed."));
            }

            return new IngestFromUrlResult(
                success: true,
                sourceUrl: downloadResult.SourceUrl,
                localAudioPath: downloadResult.LocalAudioPath,
                lectureId: ingestResult.LectureId,
                lectureTitle: ingestResult.LectureTitle,
                transcriptPath: ingestResult.TranscriptPath,
                chunkCount: ingestResult.ChunkCount);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new IngestFromUrlResult(
                success: false,
                sourceUrl: request.Url,
                error: new ErrorInfo(
                    code: "url_ingest_failed",
                    message: "Failed to ingest lecture from URL.",
                    details: new Dictionary<string, string>
                    {
                        ["exceptionType"] = ex.GetType().Name,
                        ["exceptionMessage"] = ex.Message
                    }));
        }
    }
}
