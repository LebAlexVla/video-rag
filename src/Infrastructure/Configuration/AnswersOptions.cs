namespace VideoLectureRagAssistant.Infrastructure.Configuration;

public sealed class AnswersOptions
{
    public const string SectionName = "Answers";

    public string Provider { get; set; } = "ollama";

    public OllamaProviderOptions Ollama { get; set; } = new();

    public OpenAiProviderOptions OpenAi { get; set; } = new();

    public DeepSeekProviderOptions DeepSeek { get; set; } = new();
}

public sealed class DeepSeekProviderOptions
{
    public string BaseUrl { get; set; } = "https://api.deepseek.com";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "deepseek-v4-flash";
}