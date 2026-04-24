using System.Text;
using VideoLectureRagAssistant.Application.Abstractions;
using VideoLectureRagAssistant.Domain.Entities;

namespace VideoLectureRagAssistant.Application.Services;

public sealed class Chunker : IChunker
{
    private readonly int _maxChunkLength;
    private readonly int _overlapLength;

    public Chunker(int maxChunkLength = 1200, int overlapLength = 200)
    {
        if (maxChunkLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxChunkLength), "MaxChunkLength must be greater than 0.");

        if (overlapLength < 0)
            throw new ArgumentOutOfRangeException(nameof(overlapLength), "OverlapLength must be greater than or equal to 0.");

        if (overlapLength >= maxChunkLength)
            throw new ArgumentOutOfRangeException(nameof(overlapLength), "OverlapLength must be smaller than MaxChunkLength.");

        _maxChunkLength = maxChunkLength;
        _overlapLength = overlapLength;
    }

    public IReadOnlyList<LectureChunk> Chunk(Transcript transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        var result = new List<LectureChunk>();
        var workingSegments = new List<TranscriptSegment>();
        var builder = new StringBuilder();
        var chunkIndex = 0;

        foreach (var segment in transcript.Segments)
        {
            if (builder.Length > 0 && builder.Length + 1 + segment.Text.Length > _maxChunkLength)
            {
                result.Add(BuildChunk(transcript.Lecture, workingSegments, chunkIndex));
                chunkIndex++;

                workingSegments = BuildOverlapSegments(workingSegments);
                builder.Clear();
                AppendSegments(builder, workingSegments);
            }

            workingSegments.Add(segment);

            if (builder.Length > 0)
                builder.Append(' ');

            builder.Append(segment.Text);
        }

        if (workingSegments.Count > 0)
        {
            result.Add(BuildChunk(transcript.Lecture, workingSegments, chunkIndex));
        }

        return result;
    }

    private LectureChunk BuildChunk(
        Lecture lecture,
        IReadOnlyList<TranscriptSegment> segments,
        int chunkIndex)
    {
        var text = string.Join(" ", segments.Select(x => x.Text)).Trim();
        var startSec = segments[0].StartSec;
        var endSec = segments[^1].EndSec;
        var approxMinute = (int)Math.Floor(startSec / 60d);

        return new LectureChunk(
            chunkId: $"{lecture.LectureId}-chunk-{chunkIndex:D4}",
            lectureId: lecture.LectureId,
            lectureTitle: lecture.Title,
            chunkIndex: chunkIndex,
            text: text,
            approxMinute: approxMinute,
            approxStartSec: startSec,
            approxEndSec: endSec);
    }

    private List<TranscriptSegment> BuildOverlapSegments(IReadOnlyList<TranscriptSegment> segments)
    {
        if (_overlapLength == 0 || segments.Count == 0)
            return new List<TranscriptSegment>();

        var overlap = new List<TranscriptSegment>();
        var totalLength = 0;

        for (var i = segments.Count - 1; i >= 0; i--)
        {
            var segment = segments[i];
            var lengthWithSpace = totalLength == 0 ? segment.Text.Length : segment.Text.Length + 1;

            if (totalLength > 0 && totalLength + lengthWithSpace > _overlapLength)
                break;

            overlap.Insert(0, segment);
            totalLength += lengthWithSpace;
        }

        return overlap;
    }

    private static void AppendSegments(StringBuilder builder, IReadOnlyList<TranscriptSegment> segments)
    {
        for (var i = 0; i < segments.Count; i++)
        {
            if (i > 0)
                builder.Append(' ');

            builder.Append(segments[i].Text);
        }
    }
}