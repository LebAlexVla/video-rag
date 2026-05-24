namespace VideoLectureRagAssistant.Infrastructure.Configuration;

public sealed class AudioDownloaderOptions
{
    public const string SectionName = "AudioDownloader";

    public string ExecutablePath { get; init; } = "yt-dlp";

    public string FfmpegExecutablePath { get; init; } = "ffmpeg";

    public string OutputDirectory { get; init; } = "data/downloads/audio";

    // Prefer audio-only. If the platform does not expose it, use the smallest stream containing audio.
    public string Format { get; init; } = "bestaudio/worst[acodec!=none]";

    // m4a is fast for VK/Rutube AAC/mp4a audio. It usually avoids expensive mp3 transcoding.
    public string AudioFormat { get; init; } = "m4a";

    public string AudioQuality { get; init; } = "0";

    public bool NoPlaylist { get; init; } = true;

    // First try: yt-dlp -g -> ffmpeg -c:a copy.
    // If ffmpeg cannot open the direct URL, fallback to yt-dlp --extract-audio.
    public bool UseStreamingFfmpegCopy { get; init; } = true;

    // Keeps VK reliable: VK direct CDN URLs can require yt-dlp-managed headers/challenges.
    public bool FallbackToYtDlpPostProcessing { get; init; } = true;
}
