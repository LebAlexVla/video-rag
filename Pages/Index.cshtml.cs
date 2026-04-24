using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VideoLectureRagAssistant.Application.Abstractions;
using VideoLectureRagAssistant.Application.Contracts;

namespace VideoLectureRagAssistant.Pages;

public sealed class IndexModel : PageModel
{
    private readonly IAskService _askService;

    public IndexModel(IAskService askService)
    {
        _askService = askService ?? throw new ArgumentNullException(nameof(askService));
    }

    [BindProperty]
    public AskInputModel Input { get; set; } = new();

    public AskResponse? Response { get; private set; }

    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
        Input = new AskInputModel
        {
            Question = "О чём эта лекция?",
            TopK = 5,
            MinScore = 0.1
        };
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return Page();

        try
        {
            var request = new AskRequest(
                question: Input.Question,
                topK: Input.TopK,
                minScore: Input.MinScore);

            Response = await _askService.AskAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException)
        {
            ErrorMessage = ex.Message;
        }

        return Page();
    }

    public sealed class AskInputModel
    {
        [Required(ErrorMessage = "Введите вопрос.")]
        [StringLength(1000, ErrorMessage = "Вопрос должен быть не длиннее 1000 символов.")]
        public string Question { get; set; } = "О чём эта лекция?";

        [Range(1, 10, ErrorMessage = "TopK должен быть в диапазоне 1..10.")]
        public int TopK { get; set; } = 5;

        [Range(0, 1, ErrorMessage = "MinScore должен быть в диапазоне 0..1.")]
        public double MinScore { get; set; } = 0.1;
    }
}