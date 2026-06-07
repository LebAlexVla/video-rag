namespace VideoLectureRagAssistant.Infrastructure.Configuration;

public sealed class AnswersOptions
{
    public const string SectionName = "Answers";

    public string Provider { get; set; } = "ollama";

    public OllamaProviderOptions Ollama { get; set; } = new();

    public OpenAiProviderOptions OpenAi { get; set; } = new();

    public DeepSeekProviderOptions DeepSeek { get; set; } = new();

    public YandexAiStudioProviderOptions Yandex { get; set; } = new();
}

public sealed class DeepSeekProviderOptions
{
    public string BaseUrl { get; set; } = "https://api.deepseek.com";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "deepseek-v4-flash";
}

public sealed class YandexAiStudioProviderOptions
{
    public string BaseUrl { get; set; } = "https://ai.api.cloud.yandex.net/v1";

    public string ApiKey { get; set; } = string.Empty;

    public string FolderId { get; set; } = string.Empty;

    public string Model { get; set; } = "deepseek-v32";

    public string BuildModelUri()
    {
        if (string.IsNullOrWhiteSpace(Model))
            throw new InvalidOperationException("Yandex model is required.");

        var model = Model.Trim();

        if (model.StartsWith("gpt://", StringComparison.OrdinalIgnoreCase))
            return model;

        if (string.IsNullOrWhiteSpace(FolderId))
            throw new InvalidOperationException("Yandex folder ID is required when model is not a full gpt:// URI.");

        return $"gpt://{FolderId.Trim()}/{model}";
    }
}