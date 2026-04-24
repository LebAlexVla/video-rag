namespace VideoLectureRagAssistant.Infrastructure.Configuration;

public sealed class ChunkingOptions
{
    public const string SectionName = "Chunking";

    public int MaxChunkLength { get; set; } = 1200;

    public int OverlapLength { get; set; } = 200;
}