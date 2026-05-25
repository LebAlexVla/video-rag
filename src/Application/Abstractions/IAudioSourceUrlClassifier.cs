using VideoLectureRagAssistant.Application.Contracts;

namespace VideoLectureRagAssistant.Application.Abstractions;

public interface IAudioSourceUrlClassifier
{
    bool IsSupported(Uri uri);

    AudioSourceProvider? DetectProvider(Uri uri);
}
