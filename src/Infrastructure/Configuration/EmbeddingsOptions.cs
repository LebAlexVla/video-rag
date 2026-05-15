namespace VideoLectureRagAssistant.Infrastructure.Configuration;

public sealed class EmbeddingsOptions
{
    public const string SectionName = "Embeddings";

    public string Provider { get; set; } = "ollama";

    public OllamaProviderOptions Ollama { get; set; } = new();

    public OpenAiProviderOptions OpenAi { get; set; } = new();

    public GeminiProviderOptions Gemini { get; set; } = new();
}

public sealed class OllamaProviderOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";

    public string Model { get; set; } = string.Empty;
}

public sealed class OpenAiProviderOptions
{
    public string BaseUrl { get; set; } = "https://api.openai.com";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;
}

public sealed class GeminiProviderOptions
{
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-embedding-001";

    public int OutputDimensionality { get; set; } = 768;
}