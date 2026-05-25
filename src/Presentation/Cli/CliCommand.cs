using VideoLectureRagAssistant.Application.Contracts;

namespace VideoLectureRagAssistant.Presentation.Cli;

public abstract record CliCommand;

public sealed record IngestCliCommand(LectureIngestRequest Request) : CliCommand;

public sealed record IngestUrlCliCommand(IngestFromUrlRequest Request) : CliCommand;

public sealed record RebuildCliCommand(LectureRebuildRequest Request) : CliCommand;
