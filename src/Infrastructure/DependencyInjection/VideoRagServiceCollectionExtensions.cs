using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VideoLectureRagAssistant.Application.Abstractions;
using VideoLectureRagAssistant.Application.Services;
using VideoLectureRagAssistant.Infrastructure.Answers;
using VideoLectureRagAssistant.Infrastructure.Configuration;
using VideoLectureRagAssistant.Infrastructure.Downloads;
using VideoLectureRagAssistant.Infrastructure.Embeddings;
using VideoLectureRagAssistant.Infrastructure.Transcript;
using VideoLectureRagAssistant.Infrastructure.Transcription;
using VideoLectureRagAssistant.Infrastructure.VectorStore;
using VideoLectureRagAssistant.Infrastructure.VideoSources;

namespace VideoLectureRagAssistant.Infrastructure.DependencyInjection;

public static class VideoRagServiceCollectionExtensions
{
    public static IServiceCollection AddVideoRagApplicationServices(this IServiceCollection services)
    {
        services.AddHostedService<QdrantInitializationService>();

        services.AddSingleton<IVideoSource, LocalFileVideoSource>();
        services.AddSingleton<IAudioSourceUrlClassifier, AudioSourceUrlClassifier>();

        services.AddSingleton<IAudioDownloader>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<AudioDownloaderOptions>>().Value;

            return new YtDlpAudioDownloader(
                sourceUrlClassifier: serviceProvider.GetRequiredService<IAudioSourceUrlClassifier>(),
                executablePath: options.ExecutablePath,
                outputDirectory: options.OutputDirectory,
                format: options.Format,
                audioFormat: options.AudioFormat,
                audioQuality: options.AudioQuality,
                noPlaylist: options.NoPlaylist,
                ffmpegExecutablePath: options.FfmpegExecutablePath,
                useStreamingMode: options.UseStreamingFfmpegCopy,
                fallbackToYtDlpPostProcessing: options.FallbackToYtDlpPostProcessing);
        });

        services.AddSingleton<ITranscriptReader, JsonTranscriptReader>();

        services.AddSingleton<IChunker>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ChunkingOptions>>().Value;

            return new Chunker(
                maxChunkLength: options.MaxChunkLength,
                overlapLength: options.OverlapLength);
        });

        services.AddSingleton<ITranscriptionRunner>(serviceProvider =>
        {
            var helperOptions = serviceProvider.GetRequiredService<IOptions<PythonHelperOptions>>().Value;
            var pathsOptions = serviceProvider.GetRequiredService<IOptions<PathsOptions>>().Value;

            return new PythonTranscriptionRunner(
                pythonExecutable: helperOptions.PythonExecutable,
                helperScriptPath: helperOptions.ScriptPath,
                jobsDirectory: pathsOptions.Jobs);
        });

        services.AddSingleton<IEmbeddingProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<EmbeddingsOptions>>().Value;
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

            return options.Provider.Trim().ToLowerInvariant() switch
            {
                "ollama" => new OllamaEmbeddingProvider(
                    httpClient: httpClientFactory.CreateClient("ollama-embeddings"),
                    modelName: options.Ollama.Model),

                "openai" => new OpenAiEmbeddingProvider(
                    httpClient: httpClientFactory.CreateClient("openai-embeddings"),
                    apiKey: options.OpenAi.ApiKey,
                    modelName: options.OpenAi.Model),

                "gemini" => new GeminiEmbeddingProvider(
                    httpClient: httpClientFactory.CreateClient("gemini-embeddings"),
                    apiKey: options.Gemini.ApiKey,
                    modelName: options.Gemini.Model,
                    outputDimensionality: options.Gemini.OutputDimensionality),

                _ => throw new InvalidOperationException("Unsupported embeddings provider.")
            };
        });

        services.AddSingleton<IVectorStore>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<QdrantOptions>>().Value;
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

            return new QdrantVectorStore(
                httpClient: httpClientFactory.CreateClient("qdrant"),
                collectionName: options.CollectionName);
        });

        services.AddSingleton<IAnswerGenerator>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<AnswersOptions>>().Value;
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

            return options.Provider.Trim().ToLowerInvariant() switch
            {
                "ollama" => new OllamaAnswerGenerator(
                    httpClient: httpClientFactory.CreateClient("ollama-answers"),
                    modelName: options.Ollama.Model),

                "openai" => new OpenAiAnswerGenerator(
                    httpClient: httpClientFactory.CreateClient("openai-answers"),
                    apiKey: options.OpenAi.ApiKey,
                    modelName: options.OpenAi.Model),

                "deepseek" => new OpenAiAnswerGenerator(
                    httpClient: httpClientFactory.CreateClient("deepseek-answers"),
                    apiKey: options.DeepSeek.ApiKey,
                    modelName: options.DeepSeek.Model),

                "yandex" => new YandexAiStudioAnswerGenerator(
                    apiKey: options.Yandex.ApiKey,
                    baseUrl: options.Yandex.BaseUrl,
                    modelUri: options.Yandex.BuildModelUri()),

                _ => throw new InvalidOperationException("Unsupported answers provider.")
            };
        });

        services.AddSingleton<AnswerGenerator>();
        services.AddSingleton<IContextRetriever, ContextRetriever>();
        services.AddSingleton<IAskService, AskService>();
        services.AddSingleton<IIngestFromUrlService, IngestFromUrlService>();
        services.AddSingleton<IIngestJobService, InMemoryIngestJobService>();

        services.AddSingleton<ILectureIngestService>(serviceProvider =>
        {
            var pathsOptions = serviceProvider.GetRequiredService<IOptions<PathsOptions>>().Value;

            return new LectureIngestService(
                videoSource: serviceProvider.GetRequiredService<IVideoSource>(),
                transcriptionRunner: serviceProvider.GetRequiredService<ITranscriptionRunner>(),
                transcriptReader: serviceProvider.GetRequiredService<ITranscriptReader>(),
                chunker: serviceProvider.GetRequiredService<IChunker>(),
                embeddingProvider: serviceProvider.GetRequiredService<IEmbeddingProvider>(),
                vectorStore: serviceProvider.GetRequiredService<IVectorStore>(),
                transcriptsRootPath: pathsOptions.Transcripts,
                registryPath: pathsOptions.Registry);
        });

        return services;
    }
}