using System.Text.Json;
using VideoLectureRagAssistant.Application.Abstractions;
using VideoLectureRagAssistant.Application.Contracts;
using VideoLectureRagAssistant.Domain.Entities;

namespace VideoLectureRagAssistant.Application.Services;

public sealed class LectureIngestService : ILectureIngestService
{
    private readonly IVideoSource _videoSource;
    private readonly ITranscriptionRunner _transcriptionRunner;
    private readonly ITranscriptReader _transcriptReader;
    private readonly IChunker _chunker;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly string _transcriptsRootPath;
    private readonly string _registryPath;

    public LectureIngestService(
        IVideoSource videoSource,
        ITranscriptionRunner transcriptionRunner,
        ITranscriptReader transcriptReader,
        IChunker chunker,
        IEmbeddingProvider embeddingProvider,
        IVectorStore vectorStore,
        string transcriptsRootPath = "data/transcripts",
        string registryPath = "data/registry/lectures.json")
    {
        _videoSource = videoSource ?? throw new ArgumentNullException(nameof(videoSource));
        _transcriptionRunner = transcriptionRunner ?? throw new ArgumentNullException(nameof(transcriptionRunner));
        _transcriptReader = transcriptReader ?? throw new ArgumentNullException(nameof(transcriptReader));
        _chunker = chunker ?? throw new ArgumentNullException(nameof(chunker));
        _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));

        if (string.IsNullOrWhiteSpace(transcriptsRootPath))
            throw new ArgumentException("Transcripts root path is required.", nameof(transcriptsRootPath));

        if (string.IsNullOrWhiteSpace(registryPath))
            throw new ArgumentException("Registry path is required.", nameof(registryPath));

        _transcriptsRootPath = Path.GetFullPath(transcriptsRootPath);
        _registryPath = Path.GetFullPath(registryPath);
    }

    public async Task<LectureIngestResult> IngestAsync(
        LectureIngestRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var source = await _videoSource.ResolveAsync(request.InputPath, cancellationToken);

            var jobId = BuildJobId();
            var transcriptPath = BuildTranscriptPath(source.FileName);

            var transcriptionRequest = new TranscriptionRunRequest(
                jobId: jobId,
                inputVideoPath: source.LocalPath,
                outputTranscriptPath: transcriptPath,
                requestedTitle: request.RequestedTitle ?? source.RequestedTitle,
                languageHint: request.LanguageHint,
                transcriptionProvider: request.TranscriptionProvider,
                transcriptionModel: request.TranscriptionModel,
                overwrite: request.Overwrite);

            var transcriptionResult = await _transcriptionRunner.RunAsync(transcriptionRequest, cancellationToken);

            if (!transcriptionResult.Success || string.IsNullOrWhiteSpace(transcriptionResult.TranscriptPath))
            {
                return new LectureIngestResult(
                    success: false,
                    error: transcriptionResult.Error ?? new ErrorInfo(
                        code: "transcription_failed",
                        message: "Transcription runner did not return a valid transcript path."));
            }

            var transcript = await _transcriptReader.ReadAsync(transcriptionResult.TranscriptPath, cancellationToken);

            var chunks = _chunker.Chunk(transcript);
            var embeddedChunks = await EmbedChunksAsync(chunks, cancellationToken);

            await _vectorStore.UpsertLectureChunksAsync(embeddedChunks, cancellationToken);

            await SaveRegistryEntryAsync(
                new RegistryLectureEntry(
                    transcript.Lecture.LectureId,
                    transcript.Lecture.Title,
                    transcriptionResult.TranscriptPath,
                    transcript.Lecture.SourcePath,
                    transcript.Lecture.SourceFileName),
                cancellationToken);

            return new LectureIngestResult(
                success: true,
                lectureId: transcript.Lecture.LectureId,
                lectureTitle: transcript.Lecture.Title,
                transcriptPath: transcriptionResult.TranscriptPath,
                chunkCount: embeddedChunks.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new LectureIngestResult(
                success: false,
                error: new ErrorInfo(
                    code: "ingest_failed",
                    message: "Failed to ingest lecture.",
                    details: new Dictionary<string, string>
                    {
                        ["exceptionType"] = ex.GetType().Name,
                        ["exceptionMessage"] = ex.Message
                    }));
        }
    }

    public async Task<LectureRebuildResult> RebuildAsync(
        LectureRebuildRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var entries = await ReadRegistryAsync(cancellationToken);

            if (request.ClearIndexFirst)
            {
                await _vectorStore.ClearAsync(cancellationToken);
            }

            var rebuiltLectureCount = 0;
            var rebuiltChunkCount = 0;

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(entry.TranscriptPath) || !File.Exists(entry.TranscriptPath))
                {
                    continue;
                }

                var transcript = await _transcriptReader.ReadAsync(entry.TranscriptPath, cancellationToken);
                var chunks = _chunker.Chunk(transcript);
                var embeddedChunks = await EmbedChunksAsync(chunks, cancellationToken);

                await _vectorStore.UpsertLectureChunksAsync(embeddedChunks, cancellationToken);

                rebuiltLectureCount++;
                rebuiltChunkCount += embeddedChunks.Count;
            }

            return new LectureRebuildResult(
                success: true,
                rebuiltLectureCount: rebuiltLectureCount,
                rebuiltChunkCount: rebuiltChunkCount);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new LectureRebuildResult(
                success: false,
                error: new ErrorInfo(
                    code: "rebuild_failed",
                    message: "Failed to rebuild index.",
                    details: new Dictionary<string, string>
                    {
                        ["exceptionType"] = ex.GetType().Name,
                        ["exceptionMessage"] = ex.Message
                    }));
        }
    }

    private async Task<IReadOnlyList<EmbeddedLectureChunk>> EmbedChunksAsync(
        IReadOnlyList<LectureChunk> chunks,
        CancellationToken cancellationToken)
    {
        if (chunks.Count == 0)
            return Array.Empty<EmbeddedLectureChunk>();

        var texts = chunks.Select(x => x.Text).ToArray();
        var vectors = await _embeddingProvider.EmbedBatchAsync(texts, cancellationToken);

        if (vectors.Count != chunks.Count)
        {
            throw new InvalidOperationException(
                $"Embedding provider returned {vectors.Count} vectors for {chunks.Count} chunks.");
        }

        var embeddedChunks = new List<EmbeddedLectureChunk>(chunks.Count);

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var vector = vectors[i];

            embeddedChunks.Add(new EmbeddedLectureChunk(
                chunkId: chunk.ChunkId,
                lectureId: chunk.LectureId,
                lectureTitle: chunk.LectureTitle,
                chunkIndex: chunk.ChunkIndex,
                text: chunk.Text,
                approxMinute: chunk.ApproxMinute,
                vector: vector,
                approxStartSec: chunk.ApproxStartSec,
                approxEndSec: chunk.ApproxEndSec));
        }

        return embeddedChunks;
    }

    private string BuildJobId()
    {
        return $"ingest-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
    }

    private string BuildTranscriptPath(string fileName)
    {
        var transcriptsRootFullPath = Path.GetFullPath(_transcriptsRootPath);
        Directory.CreateDirectory(transcriptsRootFullPath);

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var safeName = MakeSafeFileName(baseName);

        return Path.Combine(transcriptsRootFullPath, $"{safeName}.transcript.json");
    }

    private static string MakeSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value
            .Trim()
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray();

        var result = new string(chars);
        return string.IsNullOrWhiteSpace(result) ? "lecture" : result;
    }

    private async Task SaveRegistryEntryAsync(
        RegistryLectureEntry entry,
        CancellationToken cancellationToken)
    {
        var entries = await ReadRegistryAsync(cancellationToken);

        var existingIndex = entries.FindIndex(x => string.Equals(x.LectureId, entry.LectureId, StringComparison.Ordinal));

        if (existingIndex >= 0)
            entries[existingIndex] = entry;
        else
            entries.Add(entry);

        var directory = Path.GetDirectoryName(_registryPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_registryPath);
        await JsonSerializer.SerializeAsync(stream, entries, cancellationToken: cancellationToken);
    }

    private async Task<List<RegistryLectureEntry>> ReadRegistryAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_registryPath))
            return new List<RegistryLectureEntry>();

        await using var stream = File.OpenRead(_registryPath);

        var entries = await JsonSerializer.DeserializeAsync<List<RegistryLectureEntry>>(
            stream,
            cancellationToken: cancellationToken);

        return entries ?? new List<RegistryLectureEntry>();
    }

    private sealed record class RegistryLectureEntry(
        string LectureId,
        string LectureTitle,
        string TranscriptPath,
        string? SourcePath,
        string? SourceFileName);
}