namespace VideoRag.Contracts;

public sealed record IngestJobStartResponseDto(
    Guid JobId,
    string Status,
    string? StatusUrl = null
);
