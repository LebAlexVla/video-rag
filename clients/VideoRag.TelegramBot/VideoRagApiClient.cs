using System.Net.Http.Json;
using VideoRag.Contracts;

namespace VideoRag.TelegramBot;

public sealed class VideoRagApiClient
{
    private readonly HttpClient _httpClient;

    public VideoRagApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AskResponseDto?> AskAsync(
        string question,
        CancellationToken cancellationToken)
    {
        var request = new AskRequestDto(
            Question: question,
            TopK: 5,
            MinScore: 0.5
        );

        var response = await _httpClient.PostAsJsonAsync(
            "/ask",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<AskResponseDto>(
            cancellationToken);
    }
    
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}