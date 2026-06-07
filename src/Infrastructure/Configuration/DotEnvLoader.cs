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

        CopyEnvironmentVariable(
            sourceName: "DEEPSEEK_API_KEY",
            targetName: "Answers__DeepSeek__ApiKey");

        CopyEnvironmentVariable(
            sourceName: "GEMINI_API_KEY",
            targetName: "Embeddings__Gemini__ApiKey");

        CopyEnvironmentVariable(
            sourceName: "YANDEX_API_KEY",
            targetName: "Answers__Yandex__ApiKey");

        CopyEnvironmentVariable(
            sourceName: "YANDEX_FOLDER_ID",
            targetName: "Answers__Yandex__FolderId");

        CopyEnvironmentVariable(
            sourceName: "YANDEX_MODEL",
            targetName: "Answers__Yandex__Model");

        CopyEnvironmentVariable(
            sourceName: "YANDEX_BASE_URL",
            targetName: "Answers__Yandex__BaseUrl");
    }

    private static void CopyEnvironmentVariable(
        string sourceName,
        string targetName)
    {
        var value = Environment.GetEnvironmentVariable(sourceName);

        if (!string.IsNullOrWhiteSpace(value))
            Environment.SetEnvironmentVariable(targetName, value);
    }
}