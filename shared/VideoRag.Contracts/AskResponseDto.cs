namespace VideoRag.Contracts;

public sealed record AskResponseDto(
    string? Answer,
    bool UsedContext,
    string? Message,
    IReadOnlyList<SourceDto> Sources
);