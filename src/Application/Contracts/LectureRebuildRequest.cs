namespace VideoLectureRagAssistant.Application.Contracts;

public sealed record class LectureRebuildRequest
{
    public LectureRebuildRequest(bool clearIndexFirst = true)
    {
        ClearIndexFirst = clearIndexFirst;
    }

    public bool ClearIndexFirst { get; }
}