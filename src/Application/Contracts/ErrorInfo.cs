namespace VideoLectureRagAssistant.Application.Contracts;

public sealed record class ErrorInfo
{
    public ErrorInfo(
        string code,
        string message,
        IReadOnlyDictionary<string, string>? details = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", nameof(code));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required.", nameof(message));

        Code = code.Trim();
        Message = message.Trim();
        Details = details;
    }

    public string Code { get; }

    public string Message { get; }

    public IReadOnlyDictionary<string, string>? Details { get; }
}