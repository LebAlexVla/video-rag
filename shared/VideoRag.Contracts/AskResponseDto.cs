namespace VideoRag.Contracts;

public sealed record AskResponseDto(
    string Answer,
    IReadOnlyList<SourceDto> Sources,
    bool HasEnoughContext
);