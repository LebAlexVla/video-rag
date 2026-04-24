using VideoLectureRagAssistant.Application.Abstractions;
using VideoLectureRagAssistant.Application.Contracts;

namespace VideoLectureRagAssistant.Infrastructure.VideoSources;

public sealed class LocalFileVideoSource : IVideoSource
{
    public string SourceType => "local-file";

    public Task<VideoSourceDescriptor> ResolveAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Input path is required.", nameof(input));

        var normalizedInput = input.Trim();
        var fullPath = Path.GetFullPath(normalizedInput);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Input video file was not found.", fullPath);

        var fileName = Path.GetFileName(fullPath);

        return Task.FromResult(new VideoSourceDescriptor(
            sourceType: SourceType,
            input: normalizedInput,
            localPath: fullPath,
            fileName: fileName));
    }
}