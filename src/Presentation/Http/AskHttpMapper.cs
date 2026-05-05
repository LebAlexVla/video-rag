using VideoLectureRagAssistant.Application.Contracts;
using VideoLectureRagAssistant.Domain.Entities;
using VideoRag.Contracts;

namespace VideoLectureRagAssistant.Presentation.Http;

internal static class AskHttpMapper
{
    public static AskResponseDto ToDto(AskResponse response)
    {
        var answer = response.Answer
                     ?? response.Message
                     ?? "Не нашёл достаточно контекста для ответа.";

        return new AskResponseDto(
            Answer: answer,
            Sources: response.Sources
                .Select(ToDto)
                .ToList(),
            HasEnoughContext: response.UsedContext
        );
    }

    private static SourceDto ToDto(SourceCitation source)
    {
        return new SourceDto(
            LectureTitle: source.LectureTitle,
            ChunkIndex: source.ChunkIndex,
            ApproxMinute: source.ApproxMinute,
            Position: FormatSourcePosition(source),
            ApproxStartSec: source.ApproxStartSec,
            ApproxEndSec: source.ApproxEndSec
        );
    }

    private static string FormatSourcePosition(SourceCitation source)
    {
        if (source.ApproxStartSec.HasValue && source.ApproxEndSec.HasValue)
        {
            return $"примерно {FormatSeconds(source.ApproxStartSec.Value)}–{FormatSeconds(source.ApproxEndSec.Value)}";
        }

        if (source.ApproxStartSec.HasValue)
        {
            return $"примерно {FormatSeconds(source.ApproxStartSec.Value)}";
        }

        return $"примерно {source.ApproxMinute} мин.";
    }

    private static string FormatSeconds(double seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);

        if (time.TotalHours >= 1)
        {
            return $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
        }

        return $"{time.Minutes:D2}:{time.Seconds:D2}";
    }
}