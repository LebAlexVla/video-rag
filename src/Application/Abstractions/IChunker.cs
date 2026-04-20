using VideoLectureRagAssistant.Domain.Entities;

namespace VideoLectureRagAssistant.Application.Abstractions;

public interface IChunker
{
    IReadOnlyList<LectureChunk> Chunk(Transcript transcript);
}