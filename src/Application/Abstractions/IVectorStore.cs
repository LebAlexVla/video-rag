using VideoLectureRagAssistant.Application.Contracts;
using VideoLectureRagAssistant.Domain.Entities;

namespace VideoLectureRagAssistant.Application.Abstractions;

public interface IVectorStore
{
    Task UpsertLectureChunksAsync(
        IReadOnlyList<EmbeddedLectureChunk> chunks,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RetrievedContext>> SearchAsync(
        float[] queryVector,
        int topK,
        CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}