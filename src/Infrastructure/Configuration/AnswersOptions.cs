namespace VideoLectureRagAssistant.Infrastructure.Configuration;

public sealed class AnswersOptions
{
    public const string SectionName = "Answers";

    public string Provider { get; set; } = "ollama";

    public OllamaProviderOptions Ollama { get; set; } = new();

    public OpenAiProviderOptions OpenAi { get; set; } = new();
}