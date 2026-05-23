namespace VideoRag.Contracts;

public sealed record AskRequestDto(
    string Question,
    int TopK = 5,
    double MinScore = 0.3
);