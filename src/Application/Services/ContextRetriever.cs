using VideoLectureRagAssistant.Application.Abstractions;
using VideoLectureRagAssistant.Application.Contracts;
using VideoLectureRagAssistant.Domain.Entities;

namespace VideoLectureRagAssistant.Application.Services;

public sealed class ContextRetriever : IContextRetriever
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;

    public ContextRetriever(
        IEmbeddingProvider embeddingProvider,
        IVectorStore vectorStore)
    {
        _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
    }

    public async Task<ContextRetrievalResult> RetrieveAsync(
        AskRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var queryVector = await _embeddingProvider.EmbedAsync(request.Question, cancellationToken);
        var rawResults = await _vectorStore.SearchAsync(queryVector, request.TopK, cancellationToken);

        var filtered = rawResults
            .Where(x => x.Score >= request.MinScore)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.LectureTitle, StringComparer.Ordinal)
            .ThenBy(x => x.ChunkIndex)
            .ToArray();

        if (filtered.Length == 0)
        {
            return new ContextRetrievalResult(
                context: Array.Empty<RetrievedContext>(),
                hasSufficientContext: false,
                message: "Недостаточно релевантного контекста для уверенного ответа по загруженным лекциям.");
        }

        return new ContextRetrievalResult(
            context: filtered,
            hasSufficientContext: true);
    }
}