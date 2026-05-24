namespace VideoLectureRagAssistant.Infrastructure.Configuration;

public static class DotEnvLoader
{
    public static void LoadFromCurrentDirectory()
    {
        var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");

        if (!File.Exists(envPath))
            return;

        foreach (var line in File.ReadAllLines(envPath))
        {
            var trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            var separatorIndex = trimmed.IndexOf('=');

            if (separatorIndex <= 0)
                continue;

            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim();

            Environment.SetEnvironmentVariable(key, value);
        }

        var deepSeekApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");

        if (!string.IsNullOrWhiteSpace(deepSeekApiKey))
            Environment.SetEnvironmentVariable("Answers__DeepSeek__ApiKey", deepSeekApiKey);

        var geminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        if (!string.IsNullOrWhiteSpace(geminiApiKey))
            Environment.SetEnvironmentVariable("Embeddings__Gemini__ApiKey", geminiApiKey);
    }
}
