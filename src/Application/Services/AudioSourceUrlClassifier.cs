using VideoLectureRagAssistant.Application.Abstractions;
using VideoLectureRagAssistant.Application.Contracts;

namespace VideoLectureRagAssistant.Application.Services;

// Kept for compatibility with earlier generated code.
// The active DI registration should use Infrastructure.Downloads.AudioSourceUrlClassifier.
public sealed class AudioSourceUrlClassifier : IAudioSourceUrlClassifier
{
    public bool IsSupported(Uri uri)
    {
        return DetectProvider(uri) is not null;
    }

    public AudioSourceProvider? DetectProvider(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        var host = uri.Host.Trim().ToLowerInvariant();

        if (host == "rutube.ru" || host.EndsWith(".rutube.ru", StringComparison.Ordinal))
            return AudioSourceProvider.Rutube;

        if (host == "vk.com" ||
            host.EndsWith(".vk.com", StringComparison.Ordinal) ||
            host == "vkvideo.ru" ||
            host.EndsWith(".vkvideo.ru", StringComparison.Ordinal))
            return AudioSourceProvider.Vk;

        return null;
    }
}
