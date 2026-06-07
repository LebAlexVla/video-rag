using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VideoLectureRagAssistant.Infrastructure.Answers;
using VideoLectureRagAssistant.Infrastructure.Embeddings;

namespace VideoLectureRagAssistant.Infrastructure.Configuration;

public static class OptionsRegistrationExtensions
{
    public static IServiceCollection ConfigureVideoRagOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<QdrantOptions>()
            .Bind(configuration.GetSection(QdrantOptions.SectionName))
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.BaseUrl) &&
                    Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _) &&
                    !string.IsNullOrWhiteSpace(options.CollectionName) &&
                    options.VectorSize > 0 &&
                    !string.IsNullOrWhiteSpace(options.Distance),
                "Qdrant configuration is invalid.")
            .ValidateOnStart();

        services
            .AddOptions<PathsOptions>()
            .Bind(configuration.GetSection(PathsOptions.SectionName))
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.Videos) &&
                    !string.IsNullOrWhiteSpace(options.Transcripts) &&
                    !string.IsNullOrWhiteSpace(options.Jobs) &&
                    !string.IsNullOrWhiteSpace(options.Registry),
                "Paths configuration is invalid.")
            .ValidateOnStart();

        services
            .AddOptions<ChunkingOptions>()
            .Bind(configuration.GetSection(ChunkingOptions.SectionName))
            .Validate(
                options =>
                    options.MaxChunkLength > 0 &&
                    options.OverlapLength >= 0 &&
                    options.OverlapLength < options.MaxChunkLength,
                "Chunking configuration is invalid.")
            .ValidateOnStart();

        services
            .AddOptions<PythonHelperOptions>()
            .Bind(configuration.GetSection(PythonHelperOptions.SectionName))
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.PythonExecutable) &&
                    !string.IsNullOrWhiteSpace(options.ScriptPath),
                "Python helper configuration is invalid.")
            .ValidateOnStart();

        services
            .AddOptions<AudioDownloaderOptions>()
            .Bind(configuration.GetSection(AudioDownloaderOptions.SectionName))
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.ExecutablePath) &&
                    !string.IsNullOrWhiteSpace(options.FfmpegExecutablePath) &&
                    !string.IsNullOrWhiteSpace(options.OutputDirectory) &&
                    !string.IsNullOrWhiteSpace(options.Format) &&
                    !string.IsNullOrWhiteSpace(options.AudioFormat) &&
                    !string.IsNullOrWhiteSpace(options.AudioQuality),
                "Audio downloader configuration is invalid.")
            .ValidateOnStart();

        services
            .AddOptions<EmbeddingsOptions>()
            .Bind(configuration.GetSection(EmbeddingsOptions.SectionName))
            .Validate(
                options => IsSupportedEmbeddingsProvider(options.Provider),
                "Embeddings provider must be 'ollama', 'openai', or 'gemini'.")
            .Validate(
                ValidateEmbeddingsProviderOptions,
                "Embeddings provider configuration is invalid.")
            .ValidateOnStart();

        services
            .AddOptions<AnswersOptions>()
            .Bind(configuration.GetSection(AnswersOptions.SectionName))
            .Validate(
                options => IsSupportedAnswersProvider(options.Provider),
                "Answers provider must be 'ollama', 'openai', 'deepseek', or 'yandex'.")
            .Validate(
                ValidateAnswersProviderOptions,
                "Answers provider configuration is invalid.")
            .ValidateOnStart();

        return services;
    }

    private static bool IsSupportedEmbeddingsProvider(string? provider)
    {
        return string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(provider, "openai", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(provider, "gemini", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedAnswersProvider(string? provider)
    {
        return string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(provider, "openai", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(provider, "deepseek", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(provider, "yandex", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ValidateEmbeddingsProviderOptions(EmbeddingsOptions options)
    {
        var provider = options.Provider.Trim().ToLowerInvariant();

        return provider switch
        {
            "ollama" =>
                !string.IsNullOrWhiteSpace(options.Ollama.BaseUrl) &&
                Uri.TryCreate(options.Ollama.BaseUrl, UriKind.Absolute, out _) &&
                !string.IsNullOrWhiteSpace(options.Ollama.Model),

            "openai" =>
                !string.IsNullOrWhiteSpace(options.OpenAi.BaseUrl) &&
                Uri.TryCreate(options.OpenAi.BaseUrl, UriKind.Absolute, out _) &&
                !string.IsNullOrWhiteSpace(options.OpenAi.ApiKey) &&
                !string.IsNullOrWhiteSpace(options.OpenAi.Model),

            "gemini" =>
                !string.IsNullOrWhiteSpace(options.Gemini.BaseUrl) &&
                Uri.TryCreate(options.Gemini.BaseUrl, UriKind.Absolute, out _) &&
                !string.IsNullOrWhiteSpace(options.Gemini.ApiKey) &&
                !string.IsNullOrWhiteSpace(options.Gemini.Model) &&
                options.Gemini.OutputDimensionality > 0,

            _ => false
        };
    }

    private static bool ValidateAnswersProviderOptions(AnswersOptions options)
    {
        var provider = options.Provider.Trim().ToLowerInvariant();

        return provider switch
        {
            "ollama" =>
                !string.IsNullOrWhiteSpace(options.Ollama.BaseUrl) &&
                Uri.TryCreate(options.Ollama.BaseUrl, UriKind.Absolute, out _) &&
                !string.IsNullOrWhiteSpace(options.Ollama.Model),

            "openai" =>
                !string.IsNullOrWhiteSpace(options.OpenAi.BaseUrl) &&
                Uri.TryCreate(options.OpenAi.BaseUrl, UriKind.Absolute, out _) &&
                !string.IsNullOrWhiteSpace(options.OpenAi.ApiKey) &&
                !string.IsNullOrWhiteSpace(options.OpenAi.Model),

            "deepseek" =>
                !string.IsNullOrWhiteSpace(options.DeepSeek.BaseUrl) &&
                Uri.TryCreate(options.DeepSeek.BaseUrl, UriKind.Absolute, out _) &&
                !string.IsNullOrWhiteSpace(options.DeepSeek.ApiKey) &&
                !string.IsNullOrWhiteSpace(options.DeepSeek.Model),

            "yandex" =>
                !string.IsNullOrWhiteSpace(options.Yandex.BaseUrl) &&
                Uri.TryCreate(options.Yandex.BaseUrl, UriKind.Absolute, out _) &&
                !string.IsNullOrWhiteSpace(options.Yandex.ApiKey) &&
                !string.IsNullOrWhiteSpace(options.Yandex.Model) &&
                (
                    options.Yandex.Model.Trim().StartsWith("gpt://", StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrWhiteSpace(options.Yandex.FolderId)
                ),

            _ => false
        };
    }
}