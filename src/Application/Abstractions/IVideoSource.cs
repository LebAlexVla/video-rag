using VideoLectureRagAssistant.Application.Contracts;

namespace VideoLectureRagAssistant.Application.Abstractions;

public interface IVideoSource
{
    string SourceType { get; }

    Task<VideoSourceDescriptor> ResolveAsync(
        string input,
        CancellationToken cancellationToken = default);
}