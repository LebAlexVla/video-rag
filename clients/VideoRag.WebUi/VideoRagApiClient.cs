using System.Net;
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
            throw new InvalidOperationException($"VideoRag API returned {(int)response.StatusCode}: {body}");
        }

        return await response.Content.ReadFromJsonAsync<AskResponseDto>(cancellationToken)
               ?? throw new InvalidOperationException("VideoRag API returned empty response.");
    }

    public async Task<IngestJobStartResponseDto> StartUrlIngestAsync(
        IngestUrlRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("/ingest/url", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"VideoRag API returned {(int)response.StatusCode}: {body}");
        }

        return await response.Content.ReadFromJsonAsync<IngestJobStartResponseDto>(cancellationToken)
               ?? throw new InvalidOperationException("VideoRag API returned empty ingest response.");
    }

    public async Task<IngestJobStatusDto?> GetIngestJobStatusAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"/ingest/jobs/{jobId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"VideoRag API returned {(int)response.StatusCode}: {body}");
        }

        return await response.Content.ReadFromJsonAsync<IngestJobStatusDto>(cancellationToken);
    }
}
