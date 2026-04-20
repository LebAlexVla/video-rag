namespace VideoLectureRagAssistant.Application.Contracts;

public sealed record class VideoSourceDescriptor
{
    public VideoSourceDescriptor(
        string sourceType,
        string input,
        string localPath,
        string fileName,
        string? requestedTitle = null)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
            throw new ArgumentException("SourceType is required.", nameof(sourceType));

        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Input is required.", nameof(input));

        if (string.IsNullOrWhiteSpace(localPath))
            throw new ArgumentException("LocalPath is required.", nameof(localPath));

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("FileName is required.", nameof(fileName));

        SourceType = sourceType.Trim();
        Input = input.Trim();
        LocalPath = localPath.Trim();
        FileName = fileName.Trim();
        RequestedTitle = string.IsNullOrWhiteSpace(requestedTitle) ? null : requestedTitle.Trim();
    }

    public string SourceType { get; }

    public string Input { get; }

    public string LocalPath { get; }

    public string FileName { get; }

    public string? RequestedTitle { get; }
}