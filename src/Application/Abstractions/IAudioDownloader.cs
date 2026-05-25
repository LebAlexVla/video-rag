using VideoLectureRagAssistant.Application.Contracts;

namespace VideoLectureRagAssistant.Application.Abstractions;

public interface IAudioDownloader
{
    Task<AudioDownloadResult> DownloadAsync(
        Uri sourceUrl,
        CancellationToken cancellationToken = default);
}
