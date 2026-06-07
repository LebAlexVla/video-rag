public sealed class YandexAiStudioProviderOptions
{
    public string BaseUrl { get; set; } = "https://ai.api.cloud.yandex.net/v1";

    public string ApiKey { get; set; } = string.Empty;

    public string FolderId { get; set; } = string.Empty;

    public string Model { get; set; } = "deepseek-v32";

    public string BuildModelUri()
    {
        if (string.IsNullOrWhiteSpace(FolderId))
            throw new InvalidOperationException("Yandex folder ID is required.");

        if (string.IsNullOrWhiteSpace(Model))
            throw new InvalidOperationException("Yandex model is required.");

        if (Model.StartsWith("gpt://", StringComparison.OrdinalIgnoreCase))
            return Model.Trim();

        return $"gpt://{FolderId.Trim()}/{Model.Trim()}";
    }
}