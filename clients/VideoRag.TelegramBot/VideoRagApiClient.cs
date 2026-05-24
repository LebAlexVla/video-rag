using System.Net;
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

    public async Task<AskResponseDto?> AskAsync(string question, CancellationToken cancellationToken)
    {
        var request = new AskRequestDto(Question: question, TopK: 5, MinScore: 0.3);
        var response = await _httpClient.PostAsJsonAsync("/ask", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<AskResponseDto>(cancellationToken);
    }

    public async Task<IngestJobStartResponseDto?> StartUrlIngestAsync(string url, CancellationToken cancellationToken)
    {
        var request = new IngestUrlRequestDto(
            Url: url,
            LectureTitle: null,
            LanguageHint: "ru",
            TranscriptionProvider: "faster-whisper",
            TranscriptionModel: "small",
            Overwrite: true);

        var response = await _httpClient.PostAsJsonAsync("/ingest/url", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<IngestJobStartResponseDto>(cancellationToken);
    }

    public async Task<IngestJobStatusDto?> GetIngestJobStatusAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync($"/ingest/jobs/{jobId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<IngestJobStatusDto>(cancellationToken);
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
