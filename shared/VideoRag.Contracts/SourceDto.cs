namespace VideoRag.Contracts;

public sealed record SourceDto(
    string LectureTitle,
    int ChunkIndex,
    int ApproxMinute,
    string Position,
    double? ApproxStartSec,
    double? ApproxEndSec
);