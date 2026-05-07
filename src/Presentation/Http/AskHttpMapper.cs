using VideoLectureRagAssistant.Application.Contracts;
using VideoLectureRagAssistant.Domain.Entities;
using VideoRag.Contracts;

namespace VideoLectureRagAssistant.Presentation.Http;

public static class AskHttpMapper
{
    public static AskResponseDto ToDto(AskResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new AskResponseDto(
            Answer: response.Answer,
            UsedContext: response.UsedContext,
            Message: response.Message,
            Sources: response.Sources.Select(ToDto).ToArray());
    }

    private static SourceDto ToDto(SourceCitation source)
    {
        return new SourceDto(
            LectureTitle: source.LectureTitle,
            ChunkIndex: source.ChunkIndex,
            ApproxMinute: source.ApproxMinute,
            Position: FormatPosition(source),
            ApproxStartSec: source.ApproxStartSec,
            ApproxEndSec: source.ApproxEndSec);
    }

    private static string FormatPosition(SourceCitation source)
    {
        if (source.ApproxStartSec is not null || source.ApproxEndSec is not null)
            return $"{FormatTime(source.ApproxStartSec)}–{FormatTime(source.ApproxEndSec)}";

        return $"~{source.ApproxMinute} мин.";
    }

    private static string FormatTime(double? seconds)
    {
        if (seconds is null)
            return "?";

        var time = TimeSpan.FromSeconds(seconds.Value);
        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
    }
}