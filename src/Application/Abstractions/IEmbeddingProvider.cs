namespace VideoLectureRagAssistant.Application.Abstractions;

public interface IEmbeddingProvider
{
    string ProviderName { get; }
    string ModelName { get; }

    Task<float[]> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);
}