using VideoLectureRagAssistant.Application.Contracts;
using VideoRag.Contracts;

namespace VideoLectureRagAssistant.Presentation.Http;

public static class IngestJobHttpMapper
{
    public static IngestJobStartResponseDto ToStartDto(IngestJobStartResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new IngestJobStartResponseDto(
            JobId: result.JobId,
            Status: result.Status.ToString(),
            StatusUrl: $"/ingest/jobs/{result.JobId}");
    }

    public static IngestJobStatusDto ToStatusDto(IngestJobStatusResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new IngestJobStatusDto(
            JobId: result.JobId,
            Status: result.Status.ToString(),
            Stage: result.Stage.ToString(),
            Message: result.Message,
            SourceUrl: result.SourceUrl,
            LocalAudioPath: result.LocalAudioPath,
            LectureId: result.LectureId,
            LectureTitle: result.LectureTitle,
            TranscriptPath: result.TranscriptPath,
            ChunkCount: result.ChunkCount,
            ErrorCode: result.Error?.Code,
            ErrorMessage: result.Error?.Message);
    }
}
