using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using VideoRag.Contracts;

namespace VideoRag.WebUi;

public sealed class VideoRagApiClient
{
    private readonly HttpClient _httpClient;

    public VideoRagApiClient(HttpClient httpClient, IOptions<VideoRagApiOptions> options)
    {
        var baseUrl = options.Value.BaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("VideoRagApi:BaseUrl is required.");

        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(baseUrl);
    }

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<AskResponseDto> AskAsync(
        AskRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("/ask", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new InvalidOperationException(
                $"VideoRag API returned {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<AskResponseDto>(
            cancellationToken: cancellationToken);

        return result ?? throw new InvalidOperationException("VideoRag API returned empty response.");
    }
}