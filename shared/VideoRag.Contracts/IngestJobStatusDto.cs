namespace VideoRag.Contracts;

public sealed record IngestJobStatusDto(
    Guid JobId,
    string Status,
    string? Stage = null,
    string? Message = null,
    string? SourceUrl = null,
    string? LocalAudioPath = null,
    string? LectureId = null,
    string? LectureTitle = null,
    string? TranscriptPath = null,
    int ChunkCount = 0,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public string? Error => ErrorMessage;
}
