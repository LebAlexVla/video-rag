namespace VideoLectureRagAssistant.Application.Contracts;

public enum IngestJobStage
{
    Created,
    DownloadingAudioAndIngesting,
    Completed,
    Failed
}
