namespace VideoLectureRagAssistant.Application.Contracts;

public sealed record class AskRequest
{
    public AskRequest(
        string question,
        int topK = 5,
        double minScore = 0.3)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Question is required.", nameof(question));

        var normalizedQuestion = question.Trim();

        if (normalizedQuestion.Length > 1000)
            throw new ArgumentOutOfRangeException(nameof(question), "Question length must be less than or equal to 1000 characters.");

        if (topK < 1 || topK > 10)
            throw new ArgumentOutOfRangeException(nameof(topK), "TopK must be in range 1..10.");

        if (minScore < 0 || minScore > 1)
            throw new ArgumentOutOfRangeException(nameof(minScore), "MinScore must be in range 0..1.");

        Question = normalizedQuestion;
        TopK = topK;
        MinScore = minScore;
    }

    public string Question { get; }

    public int TopK { get; }

    public double MinScore { get; }
}