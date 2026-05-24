using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using VideoRag.Contracts;

namespace VideoRag.TelegramBot;

public sealed class TelegramBotHostedService : BackgroundService
{
    private readonly TelegramBotClient _bot;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramBotHostedService> _logger;
    private readonly ConcurrentDictionary<string, byte> _watchedJobs = new();

    public TelegramBotHostedService(
        IOptions<TelegramOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<TelegramBotHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var token = options.Value.BotToken;

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Telegram bot token is not configured.");

        _bot = new TelegramBotClient(token);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var me = await _bot.GetMe(stoppingToken);
        _logger.LogInformation("Telegram bot started: @{Username}", me.Username);

        _bot.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            receiverOptions: new ReceiverOptions
            {
                AllowedUpdates = [UpdateType.Message]
            },
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;

        if (message?.Text is null)
            return;

        var chatId = message.Chat.Id;
        var text = message.Text.Trim();

        if (text == "/start" || text == "/help")
        {
            await bot.SendMessage(
                chatId,
                "Команды:\n" +
                "/health — проверить доступность API\n" +
                "/add <Rutube/VK URL> — добавить лекцию по ссылке\n" +
                "/status <job UUID> — проверить обработку\n\n" +
                "После /add я сам напишу, когда лекция будет готова и можно задавать вопросы.",
                cancellationToken: cancellationToken);
            return;
        }

        if (text == "/health")
        {
            using var healthScope = _scopeFactory.CreateScope();
            var apiClientHealth = healthScope.ServiceProvider.GetRequiredService<VideoRagApiClient>();
            var isHealthy = await apiClientHealth.IsHealthyAsync(cancellationToken);

            await bot.SendMessage(
                chatId,
                isHealthy ? "API доступен." : "API недоступен. Проверь, что приложение запущено на http://localhost:5000.",
                cancellationToken: cancellationToken);
            return;
        }

        if (text.StartsWith("/add ", StringComparison.OrdinalIgnoreCase))
        {
            await HandleAddCommandAsync(bot, chatId, text, cancellationToken);
            return;
        }

        if (text.StartsWith("/status ", StringComparison.OrdinalIgnoreCase))
        {
            await HandleStatusCommandAsync(bot, chatId, text, cancellationToken);
            return;
        }

        await HandleAskAsync(bot, chatId, text, cancellationToken);
    }

    private async Task HandleAddCommandAsync(ITelegramBotClient bot, long chatId, string text, CancellationToken cancellationToken)
    {
        var url = text["/add ".Length..].Trim();

        if (string.IsNullOrWhiteSpace(url))
        {
            await bot.SendMessage(chatId, "Укажи ссылку после команды: /add <Rutube/VK URL>", cancellationToken: cancellationToken);
            return;
        }

        await bot.SendChatAction(chatId, ChatAction.Typing, cancellationToken: cancellationToken);

        using var scope = _scopeFactory.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<VideoRagApiClient>();

        try
        {
            var response = await apiClient.StartUrlIngestAsync(url, cancellationToken);

            if (response is null)
            {
                await bot.SendMessage(chatId, "API не смог создать задачу обработки.", cancellationToken: cancellationToken);
                return;
            }

            await bot.SendMessage(
                chatId,
                "Задача обработки создана.\n\n" +
                $"Job UUID: {response.JobId}\n\n" +
                $"Проверить вручную: /status {response.JobId}\n\n" +
                "Я напишу сюда, когда лекция будет готова.",
                cancellationToken: cancellationToken);

            StartWatchingJob(chatId, response.JobId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start URL ingest from Telegram.");
            await bot.SendMessage(chatId, "Не удалось создать задачу обработки. Проверь, что API запущен.", cancellationToken: cancellationToken);
        }
    }

    private async Task HandleStatusCommandAsync(ITelegramBotClient bot, long chatId, string text, CancellationToken cancellationToken)
    {
        var rawJobId = text["/status ".Length..].Trim();

        if (!Guid.TryParse(rawJobId, out var jobId))
        {
            await bot.SendMessage(chatId, "Некорректный UUID. Формат: /status <job UUID>", cancellationToken: cancellationToken);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<VideoRagApiClient>();
        var status = await apiClient.GetIngestJobStatusAsync(jobId, cancellationToken);

        await bot.SendMessage(
            chatId,
            status is null ? "Задача не найдена." : FormatIngestStatus(status),
            cancellationToken: cancellationToken);

        if (status is not null && !IsFinished(status))
            StartWatchingJob(chatId, status.JobId, cancellationToken);
    }

    private async Task HandleAskAsync(ITelegramBotClient bot, long chatId, string text, CancellationToken cancellationToken)
    {
        await bot.SendChatAction(chatId, ChatAction.Typing, cancellationToken: cancellationToken);

        using var scope = _scopeFactory.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<VideoRagApiClient>();

        try
        {
            var response = await apiClient.AskAsync(text, cancellationToken);

            if (response is null)
            {
                await bot.SendMessage(chatId, "API вернул ошибку. Проверь, что /ask работает.", cancellationToken: cancellationToken);
                return;
            }

            await bot.SendMessage(chatId, FormatAnswer(response), cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Telegram message.");
            await bot.SendMessage(chatId, "API недоступен. Проверь, что backend запущен на http://localhost:5000.", cancellationToken: cancellationToken);
        }
    }

    private void StartWatchingJob(long chatId, Guid jobId, CancellationToken cancellationToken)
    {
        var watchKey = $"{chatId}:{jobId}";

        if (!_watchedJobs.TryAdd(watchKey, 0))
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await WatchJobAsync(chatId, jobId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram ingest job watcher failed.");
            }
            finally
            {
                _watchedJobs.TryRemove(watchKey, out _);
            }
        }, CancellationToken.None);
    }

    private async Task WatchJobAsync(long chatId, Guid jobId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 720 && !cancellationToken.IsCancellationRequested; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

            using var scope = _scopeFactory.CreateScope();
            var apiClient = scope.ServiceProvider.GetRequiredService<VideoRagApiClient>();
            var status = await apiClient.GetIngestJobStatusAsync(jobId, cancellationToken);

            if (status is null)
                continue;

            if (IsSucceeded(status))
            {
                await _bot.SendMessage(chatId, "Лекция добавлена. Теперь можно задавать вопросы.\n\n" + FormatIngestStatus(status), cancellationToken: cancellationToken);
                return;
            }

            if (IsFailed(status))
            {
                await _bot.SendMessage(chatId, "Обработка лекции завершилась ошибкой.\n\n" + FormatIngestStatus(status), cancellationToken: cancellationToken);
                return;
            }
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram polling error.");
        return Task.CompletedTask;
    }

    private static string FormatIngestStatus(IngestJobStatusDto status)
    {
        var lines = new List<string>
        {
            $"Job UUID: {status.JobId}",
            $"Статус: {status.Status}"
        };

        if (!string.IsNullOrWhiteSpace(status.Stage))
            lines.Add($"Этап: {status.Stage}");

        if (!string.IsNullOrWhiteSpace(status.LectureTitle))
            lines.Add($"Лекция: {status.LectureTitle}");

        if (status.ChunkCount > 0)
            lines.Add($"Чанков: {status.ChunkCount}");

        if (IsSucceeded(status))
            lines.Add("Готово: можно задавать вопросы.");

        if (IsFailed(status) && !string.IsNullOrWhiteSpace(status.ErrorMessage))
            lines.Add($"Ошибка: {status.ErrorMessage}");

        return TrimForTelegram(string.Join(Environment.NewLine, lines));
    }

    private static bool IsSucceeded(IngestJobStatusDto status) => string.Equals(status.Status, "Succeeded", StringComparison.OrdinalIgnoreCase);

    private static bool IsFailed(IngestJobStatusDto status) => string.Equals(status.Status, "Failed", StringComparison.OrdinalIgnoreCase);

    private static bool IsFinished(IngestJobStatusDto status) => IsSucceeded(status) || IsFailed(status);

    private static string FormatAnswer(AskResponseDto response)
    {
        var answer = response.UsedContext ? response.Answer : response.Message;

        if (string.IsNullOrWhiteSpace(answer))
            answer = "Не нашёл достаточно контекста для ответа.";

        if (response.Sources.Count == 0)
            return TrimForTelegram(answer);

        var sources = string.Join(Environment.NewLine, response.Sources.Take(3).Select(source => $"- {source.LectureTitle}, чанк #{source.ChunkIndex}, {source.Position}"));
        return TrimForTelegram($"{answer}{Environment.NewLine}{Environment.NewLine}Источники:{Environment.NewLine}{sources}");
    }

    private static string TrimForTelegram(string text)
    {
        const int maxLength = 4000;
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}
