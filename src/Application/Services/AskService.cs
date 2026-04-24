using VideoLectureRagAssistant.Application.Abstractions;
using VideoLectureRagAssistant.Application.Contracts;

namespace VideoLectureRagAssistant.Application.Services;

public sealed class AskService : IAskService
{
    private readonly IContextRetriever _contextRetriever;
    private readonly IAnswerGenerator _answerGenerator;
    private readonly AnswerGenerator _responseMapper;

    public AskService(
        IContextRetriever contextRetriever,
        IAnswerGenerator answerGenerator,
        AnswerGenerator responseMapper)
    {
        _contextRetriever = contextRetriever ?? throw new ArgumentNullException(nameof(contextRetriever));
        _answerGenerator = answerGenerator ?? throw new ArgumentNullException(nameof(answerGenerator));
        _responseMapper = responseMapper ?? throw new ArgumentNullException(nameof(responseMapper));
    }

    public async Task<AskResponse> AskAsync(
        AskRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var retrievalResult = await _contextRetriever.RetrieveAsync(request, cancellationToken);

        if (!retrievalResult.HasSufficientContext || retrievalResult.Context.Count == 0)
        {
            return _responseMapper.CreateFallback(
                retrievalResult.Message ?? "Недостаточно релевантного контекста для уверенного ответа по загруженным лекциям.");
        }

        var answerResult = await _answerGenerator.GenerateAsync(
            request,
            retrievalResult.Context,
            cancellationToken);

        return _responseMapper.ToAskResponse(answerResult);
    }
}