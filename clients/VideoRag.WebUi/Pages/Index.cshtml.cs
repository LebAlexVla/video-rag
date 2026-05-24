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
    public double MinScore { get; set; } = 0.0;

    [BindProperty]
    public UrlIngestInputModel UrlInput { get; set; } = new();

    [BindProperty]
    public string? StatusJobId { get; set; }

    public AskResponseDto? AskResponse { get; private set; }
    public string? AskErrorMessage { get; private set; }

    public IngestJobStartResponseDto? IngestStartResponse { get; private set; }
    public IngestJobStatusDto? IngestJobStatus { get; private set; }
    public string? IngestErrorMessage { get; private set; }
    public string? IngestInfoMessage { get; private set; }

    public bool? IsApiHealthy { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        IsApiHealthy = await _apiClient.CheckHealthAsync(cancellationToken);
    }

    public async Task<IActionResult> OnGetIngestStatusAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var status = await _apiClient.GetIngestJobStatusAsync(jobId, cancellationToken);

        if (status is null)
        {
            return new JsonResult(new
            {
                found = false,
                message = "Задача не найдена."
            });
        }

        return new JsonResult(new
        {
            found = true,
            jobId = status.JobId,
            status = status.Status,
            stage = status.Stage,
            message = BuildIngestStatusMessage(status),
            lectureTitle = status.LectureTitle,
            chunkCount = status.ChunkCount,
            canAskQuestions = IsSucceeded(status),
            isFinished = IsFinished(status)
        });
    }

    public async Task OnPostAskAsync(CancellationToken cancellationToken)
    {
        IsApiHealthy = await _apiClient.CheckHealthAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(Question))
        {
            AskErrorMessage = "Введите вопрос.";
            return;
        }

        if (TopK < 1 || TopK > 20)
        {
            AskErrorMessage = "TopK должен быть от 1 до 20.";
            return;
        }

        if (MinScore < 0 || MinScore > 1)
        {
            AskErrorMessage = "MinScore должен быть от 0 до 1. Используйте точку: 0.05, а не 0,05.";
            return;
        }

        try
        {
            var request = new AskRequestDto(
                Question: Question.Trim(),
                TopK: TopK,
                MinScore: MinScore);

            AskResponse = await _apiClient.AskAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            AskErrorMessage = ex.Message;
        }
    }

    public async Task OnPostStartUrlIngestAsync(CancellationToken cancellationToken)
    {
        IsApiHealthy = await _apiClient.CheckHealthAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(UrlInput.Url))
        {
            IngestErrorMessage = "Введите ссылку Rutube или VK.";
            return;
        }

        try
        {
            var request = new IngestUrlRequestDto(
                Url: UrlInput.Url.Trim(),
                LectureTitle: string.IsNullOrWhiteSpace(UrlInput.LectureTitle) ? null : UrlInput.LectureTitle.Trim(),
                LanguageHint: string.IsNullOrWhiteSpace(UrlInput.LanguageHint) ? "ru" : UrlInput.LanguageHint.Trim(),
                TranscriptionProvider: string.IsNullOrWhiteSpace(UrlInput.TranscriptionProvider)
                    ? "faster-whisper"
                    : UrlInput.TranscriptionProvider.Trim(),
                TranscriptionModel: string.IsNullOrWhiteSpace(UrlInput.TranscriptionModel)
                    ? "small"
                    : UrlInput.TranscriptionModel.Trim(),
                Overwrite: true);

            IngestStartResponse = await _apiClient.StartUrlIngestAsync(request, cancellationToken);
            StatusJobId = IngestStartResponse.JobId.ToString();

            IngestJobStatus = await _apiClient.GetIngestJobStatusAsync(
                IngestStartResponse.JobId,
                cancellationToken);

            IngestInfoMessage =
                "Задача создана. UUID показан ниже. Статус будет обновляться автоматически.";
        }
        catch (Exception ex)
        {
            IngestErrorMessage = ex.Message;
        }
    }

    public async Task OnPostCheckUrlIngestStatusAsync(CancellationToken cancellationToken)
    {
        IsApiHealthy = await _apiClient.CheckHealthAsync(cancellationToken);

        if (!Guid.TryParse(StatusJobId, out var jobId))
        {
            IngestErrorMessage = "Введите корректный UUID задачи.";
            return;
        }

        try
        {
            IngestJobStatus = await _apiClient.GetIngestJobStatusAsync(jobId, cancellationToken);

            if (IngestJobStatus is null)
            {
                IngestErrorMessage = "Задача с таким UUID не найдена.";
                return;
            }

            IngestInfoMessage = BuildIngestStatusMessage(IngestJobStatus);
        }
        catch (Exception ex)
        {
            IngestErrorMessage = ex.Message;
        }
    }

    public static bool IsSucceeded(IngestJobStatusDto status)
    {
        return string.Equals(status.Status, "Succeeded", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFinished(IngestJobStatusDto status)
    {
        return string.Equals(status.Status, "Succeeded", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status.Status, "Failed", StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildIngestStatusMessage(IngestJobStatusDto status)
    {
        if (string.Equals(status.Status, "Succeeded", StringComparison.OrdinalIgnoreCase))
        {
            var title = string.IsNullOrWhiteSpace(status.LectureTitle)
                ? "лекция"
                : status.LectureTitle;

            return $"Готово: {title} добавлена. Теперь можно задавать вопросы.";
        }

        if (string.Equals(status.Status, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(status.ErrorMessage)
                ? "Обработка завершилась ошибкой."
                : $"Обработка завершилась ошибкой: {status.ErrorMessage}";
        }

        var stage = string.IsNullOrWhiteSpace(status.Stage)
            ? "ожидание"
            : status.Stage;

        return $"Обработка идёт. Этап: {stage}.";
    }

    public sealed class UrlIngestInputModel
    {
        public string Url { get; set; } = string.Empty;

        public string? LectureTitle { get; set; }

        public string? LanguageHint { get; set; } = "ru";

        public string TranscriptionProvider { get; set; } = "faster-whisper";

        public string TranscriptionModel { get; set; } = "small";
    }
}
