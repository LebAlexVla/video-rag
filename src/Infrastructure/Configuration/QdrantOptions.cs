namespace VideoLectureRagAssistant.Infrastructure.Configuration;

public sealed class QdrantOptions
{
    public const string SectionName = "Qdrant";

    public string BaseUrl { get; set; } = string.Empty;

    public string CollectionName { get; set; } = string.Empty;

    public int VectorSize { get; set; }

    public string Distance { get; set; } = "Cosine";
}