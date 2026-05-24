using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using VideoRag.Contracts;

namespace VideoRag.TelegramBot;

public sealed class TelegramBotHostedService : BackgroundService
{
    private const string ButtonAddLecture = "➕ Добавить лекцию";
    private const string ButtonCheckStatus = "📊 Проверить статус";
    private const string ButtonHealth = "🟢 Проверить API";
    private const string ButtonHelp = "❓ Помощь";
    private const string ButtonCancel = "↩️ Отмена";

    private readonly TelegramBotClient _bot;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramBotHostedService> _logger;
    private readonly ConcurrentDictionary<string, byte> _watchedJobs = new();
    private readonly ConcurrentDictionary<long, PendingTelegramAction> _pendingActions = new();

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

    private async Task HandleUpdateAsync(
        ITelegramBotClient bot,
        Update update,
        CancellationToken cancellationToken)
    {
        var message = update.Message;

        if (message?.Text is null)
            return;

        var chatId = message.Chat.Id;
        var text = message.Text.Trim();

        if (string.IsNullOrWhiteSpace(text))
            return;

        if (text.Equals(ButtonCancel, StringComparison.OrdinalIgnoreCase) ||
            text.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
        {
            _pendingActions.TryRemove(chatId, out _);

            await SendMainMenuAsync(
                bot,
                chatId,
                "Действие отменено. Выбери нужную кнопку.",
                cancellationToken);

            return;
        }

        if (text.Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            _pendingActions.TryRemove(chatId, out _);

            await SendMainMenuAsync(
                bot,
                chatId,
                "Я отвечаю на вопросы по загруженным лекциям.\n\n" +
                "Можно работать кнопками ниже:\n" +
                $"{ButtonAddLecture} — добавить Rutube/VK лекцию\n" +
                $"{ButtonCheckStatus} — проверить обработку по UUID\n" +
                $"{ButtonHealth} — проверить API\n" +
                $"{ButtonHelp} — открыть справку\n\n" +
                "Обычное текстовое сообщение считается вопросом по лекциям.",
                cancellationToken);

            return;
        }

        if (text.Equals("/help", StringComparison.OrdinalIgnoreCase) ||
            text.Equals(ButtonHelp, StringComparison.OrdinalIgnoreCase))
        {
            await SendHelpAsync(bot, chatId, cancellationToken);
            return;
        }

        if (text.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
            text.Equals(ButtonHealth, StringComparison.OrdinalIgnoreCase))
        {
            await HandleHealthAsync(bot, chatId, cancellationToken);
            return;
        }

        if (text.Equals(ButtonAddLecture, StringComparison.OrdinalIgnoreCase) ||
            text.Equals("/add", StringComparison.OrdinalIgnoreCase))
        {
            _pendingActions[chatId] = PendingTelegramAction.WaitingForUrl;

            await bot.SendMessage(
                chatId,
                "Отправь ссылку на публичное Rutube/VK видео.\n\n" +
                "Пример:\n" +
                "https://rutube.ru/video/...\n\n" +
                $"Для отмены нажми {ButtonCancel}.",
                replyMarkup: BuildMainKeyboard(),
                cancellationToken: cancellationToken);

            return;
        }

        if (text.StartsWith("/add ", StringComparison.OrdinalIgnoreCase))
        {
            _pendingActions.TryRemove(chatId, out _);
            await HandleAddUrlAsync(bot, chatId, text["/add ".Length..].Trim(), cancellationToken);
            return;
        }

        if (text.Equals(ButtonCheckStatus, StringComparison.OrdinalIgnoreCase) ||
            text.Equals("/status", StringComparison.OrdinalIgnoreCase))
        {
            _pendingActions[chatId] = PendingTelegramAction.WaitingForJobId;

            await bot.SendMessage(
                chatId,
                "Отправь UUID задачи обработки.\n\n" +
                "Он появляется после добавления лекции.\n\n" +
                $"Для отмены нажми {ButtonCancel}.",
                replyMarkup: BuildMainKeyboard(),
                cancellationToken: cancellationToken);

            return;
        }

        if (text.StartsWith("/status ", StringComparison.OrdinalIgnoreCase))
        {
            _pendingActions.TryRemove(chatId, out _);
            await HandleStatusValueAsync(bot, chatId, text["/status ".Length..].Trim(), cancellationToken);
            return;
        }

        if (_pendingActions.TryGetValue(chatId, out var pendingAction))
        {
            switch (pendingAction)
            {
                case PendingTelegramAction.WaitingForUrl:
                    _pendingActions.TryRemove(chatId, out _);
                    await HandleAddUrlAsync(bot, chatId, text, cancellationToken);
                    return;

                case PendingTelegramAction.WaitingForJobId:
                    _pendingActions.TryRemove(chatId, out _);
                    await HandleStatusValueAsync(bot, chatId, text, cancellationToken);
                    return;

                default:
                    _pendingActions.TryRemove(chatId, out _);
                    break;
            }
        }

        if (text.StartsWith("/", StringComparison.OrdinalIgnoreCase))
        {
            await bot.SendMessage(
                chatId,
                "Неизвестная команда. Используй кнопки меню или /help.",
                replyMarkup: BuildMainKeyboard(),
                cancellationToken: cancellationToken);

            return;
        }

        await HandleAskAsync(bot, chatId, text, cancellationToken);
    }

    private async Task SendHelpAsync(
        ITelegramBotClient bot,
        long chatId,
        CancellationToken cancellationToken)
    {
        await bot.SendMessage(
            chatId,
            "Доступные действия:\n\n" +
            $"{ButtonAddLecture} — бот попросит ссылку и создаст задачу обработки.\n" +
            $"{ButtonCheckStatus} — бот попросит UUID и покажет статус.\n" +
            $"{ButtonHealth} — проверка доступности backend API.\n\n" +
            "После добавления лекции бот сам напишет, когда обработка завершится.\n" +
            "Обычное сообщение без команды считается вопросом по загруженным лекциям.",
            replyMarkup: BuildMainKeyboard(),
            cancellationToken: cancellationToken);
    }

    private async Task SendMainMenuAsync(
        ITelegramBotClient bot,
        long chatId,
        string text,
        CancellationToken cancellationToken)
    {
        await bot.SendMessage(
            chatId,
            text,
            replyMarkup: BuildMainKeyboard(),
            cancellationToken: cancellationToken);
    }

    private static ReplyKeyboardMarkup BuildMainKeyboard()
    {
        return new ReplyKeyboardMarkup(
            new[]
            {
                new[]
                {
                    new KeyboardButton(ButtonAddLecture),
                    new KeyboardButton(ButtonCheckStatus)
                },
                new[]
                {
                    new KeyboardButton(ButtonHealth),
                    new KeyboardButton(ButtonHelp)
                },
                new[]
                {
                    new KeyboardButton(ButtonCancel)
                }
            })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false
        };
    }

    private async Task HandleHealthAsync(
        ITelegramBotClient bot,
        long chatId,
        CancellationToken cancellationToken)
    {
        using var healthScope = _scopeFactory.CreateScope();
        var apiClientHealth = healthScope.ServiceProvider.GetRequiredService<VideoRagApiClient>();

        var isHealthy = await apiClientHealth.IsHealthyAsync(cancellationToken);

        await bot.SendMessage(
            chatId,
            isHealthy
                ? "API доступен."
                : "API недоступен. Проверь, что backend запущен на http://localhost:5000.",
            replyMarkup: BuildMainKeyboard(),
            cancellationToken: cancellationToken);
    }

    private async Task HandleAddUrlAsync(
        ITelegramBotClient bot,
        long chatId,
        string url,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            await bot.SendMessage(
                chatId,
                "Это не похоже на корректную ссылку. Нажми «Добавить лекцию» и отправь Rutube/VK URL.",
                replyMarkup: BuildMainKeyboard(),
                cancellationToken: cancellationToken);

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
                await bot.SendMessage(
                    chatId,
                    "API не смог создать задачу обработки. Проверь ссылку и backend.",
                    replyMarkup: BuildMainKeyboard(),
                    cancellationToken: cancellationToken);

                return;
            }

            await bot.SendMessage(
                chatId,
                "Задача обработки создана.\n\n" +
                $"Job UUID: {response.JobId}\n\n" +
                "Я напишу сюда, когда лекция будет готова.\n" +
                "Чтобы проверить вручную, нажми «Проверить статус» и отправь этот UUID.",
                replyMarkup: BuildMainKeyboard(),
                cancellationToken: cancellationToken);

            StartWatchingJob(chatId, response.JobId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start URL ingest from Telegram.");

            await bot.SendMessage(
                chatId,
                "Не удалось создать задачу обработки. Проверь, что API запущен.",
                replyMarkup: BuildMainKeyboard(),
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleStatusValueAsync(
        ITelegramBotClient bot,
        long chatId,
        string rawJobId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawJobId))
        {
            await bot.SendMessage(
                chatId,
                "Укажи UUID задачи. Нажми «Проверить статус» и отправь UUID.",
                replyMarkup: BuildMainKeyboard(),
                cancellationToken: cancellationToken);

            return;
        }

        if (!Guid.TryParse(rawJobId, out var jobId))
        {
            await bot.SendMessage(
                chatId,
                "Некорректный UUID. Нажми «Проверить статус» и отправь UUID задачи.",
                replyMarkup: BuildMainKeyboard(),
                cancellationToken: cancellationToken);

            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<VideoRagApiClient>();

        try
        {
            var status = await apiClient.GetIngestJobStatusAsync(jobId, cancellationToken);

            await bot.SendMessage(
                chatId,
                status is null ? "Задача не найдена." : FormatIngestStatus(status),
                replyMarkup: BuildMainKeyboard(),
                cancellationToken: cancellationToken);

            if (status is not null && !IsFinished(status))
                StartWatchingJob(chatId, status.JobId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get URL ingest status from Telegram.");

            await bot.SendMessage(
                chatId,
                "Не удалось получить статус задачи. Проверь, что API запущен.",
                replyMarkup: BuildMainKeyboard(),
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleAskAsync(
        ITelegramBotClient bot,
        long chatId,
        string text,
        CancellationToken cancellationToken)
    {
        await bot.SendChatAction(
            chatId,
            ChatAction.Typing,
            cancellationToken: cancellationToken);

        using var scope = _scopeFactory.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<VideoRagApiClient>();

        try
        {
            var response = await apiClient.AskAsync(text, cancellationToken);

            if (response is null)
            {
                await bot.SendMessage(
                    chatId,
                    "API вернул ошибку. Проверь, что /ask работает.",
                    replyMarkup: BuildMainKeyboard(),
                    cancellationToken: cancellationToken);

                return;
            }

            await bot.SendMessage(
                chatId,
                FormatAnswer(response),
                replyMarkup: BuildMainKeyboard(),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Telegram message.");

            await bot.SendMessage(
                chatId,
                "API недоступен. Проверь, что backend запущен на http://localhost:5000.",
                replyMarkup: BuildMainKeyboard(),
                cancellationToken: cancellationToken);
        }
    }

    private void StartWatchingJob(
        long chatId,
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var watchKey = $"{chatId}:{jobId}";

        if (!_watchedJobs.TryAdd(watchKey, 0))
            return;

        _ = Task.Run(
            async () =>
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
            },
            CancellationToken.None);
    }

    private async Task WatchJobAsync(
        long chatId,
        Guid jobId,
        CancellationToken cancellationToken)
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
                await _bot.SendMessage(
                    chatId,
                    "Лекция добавлена. Теперь можно задавать вопросы.\n\n" +
                    FormatIngestStatus(status),
                    replyMarkup: BuildMainKeyboard(),
                    cancellationToken: cancellationToken);

                return;
            }

            if (IsFailed(status))
            {
                await _bot.SendMessage(
                    chatId,
                    "Обработка лекции завершилась ошибкой.\n\n" +
                    FormatIngestStatus(status),
                    replyMarkup: BuildMainKeyboard(),
                    cancellationToken: cancellationToken);

                return;
            }
        }
    }

    private Task HandleErrorAsync(
        ITelegramBotClient bot,
        Exception exception,
        CancellationToken cancellationToken)
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

    private static bool IsSucceeded(IngestJobStatusDto status)
    {
        return string.Equals(status.Status, "Succeeded", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFailed(IngestJobStatusDto status)
    {
        return string.Equals(status.Status, "Failed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFinished(IngestJobStatusDto status)
    {
        return IsSucceeded(status) || IsFailed(status);
    }

    private static string FormatAnswer(AskResponseDto response)
    {
        var answer = response.UsedContext
            ? response.Answer
            : response.Message;

        if (string.IsNullOrWhiteSpace(answer))
            answer = "Не нашёл достаточно контекста для ответа.";

        if (response.Sources.Count == 0)
            return TrimForTelegram(answer);

        var sources = string.Join(
            Environment.NewLine,
            response.Sources.Take(3).Select(source =>
                $"- {source.LectureTitle}, чанк #{source.ChunkIndex}, {source.Position}"));

        return TrimForTelegram($"{answer}{Environment.NewLine}{Environment.NewLine}Источники:{Environment.NewLine}{sources}");
    }

    private static string TrimForTelegram(string text)
    {
        const int maxLength = 4000;

        if (text.Length <= maxLength)
            return text;

        return text[..maxLength] + "...";
    }

    private enum PendingTelegramAction
    {
        WaitingForUrl,
        WaitingForJobId
    }
}
