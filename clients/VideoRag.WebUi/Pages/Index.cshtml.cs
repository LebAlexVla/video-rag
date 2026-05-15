using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VideoRag.Contracts;

namespace VideoRag.WebUi.Pages;

public sealed class IndexModel : PageModel
{
    private readonly VideoRagApiClient _apiClient;

    public IndexModel(VideoRagApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty]
    public string Question { get; set; } = string.Empty;

    [BindProperty]
    public int TopK { get; set; } = 5;

    [BindProperty]
    public double MinScore { get; set; } = 0.5;
    
    public AskResponseDto? AskResponse { get; private set; }
    public string? ErrorMessage { get; private set; }

    public bool? IsApiHealthy { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        IsApiHealthy = await _apiClient.CheckHealthAsync(cancellationToken);
    }

    public async Task OnPostAsync(CancellationToken cancellationToken)
    {
        IsApiHealthy = await _apiClient.CheckHealthAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(Question))
        {
            ErrorMessage = "Введите вопрос.";
            return;
        }

        if (TopK < 1 || TopK > 10)
        {
            ErrorMessage = "TopK должен быть от 1 до 10.";
            return;
        }

        if (MinScore < 0 || MinScore > 1)
        {
            ErrorMessage = "MinScore должен быть от 0 до 1.";
            return;
        }

        try
        {
            var request = new AskRequestDto(
                Question: Question.Trim(),
                TopK: TopK,
                MinScore: MinScore
            );

            AskResponse = await _apiClient.AskAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}