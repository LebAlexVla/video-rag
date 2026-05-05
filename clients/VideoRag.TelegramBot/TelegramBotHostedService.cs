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

        if (text == "/start")
        {
            await bot.SendMessage(
                chatId,
                """
                Я отвечаю на вопросы по уже загруженным лекциям.

                Перед использованием нужно загрузить видео в основном приложении:

                dotnet run --project .\VideoLectureRagAssistant.csproj -- ingest "data/videos/lecture_0.mp4"

                После загрузки просто отправь вопрос обычным сообщением.

                Команды:
                /health — проверить доступность API
                /help — справка
                """,
                cancellationToken: cancellationToken);

            return;
        }

        if (text == "/help")
        {
            await bot.SendMessage(
                chatId,
                """
                Команды:
                /start — инструкция
                /help — справка
                /health — проверить, доступен ли API

                Как пользоваться:
                1. Сначала загрузи лекцию через CLI в основном приложении.
                2. Убедись, что API запущен на http://localhost:5000.
                3. Отправь вопрос обычным сообщением.

                """,
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
                isHealthy
                    ? "API доступен."
                    : "API недоступен. Проверь, что приложение запущено на http://localhost:5000.",
                cancellationToken: cancellationToken);

            return;
        }

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
                    cancellationToken: cancellationToken);

                return;
            }

            await bot.SendMessage(
                chatId,
                FormatAnswer(response),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Telegram message.");

            await bot.SendMessage(
                chatId,
                "API недоступен. Проверь, что VideoLectureRagAssistant запущен на http://localhost:5000.",
                cancellationToken: cancellationToken);
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

    private static string FormatAnswer(AskResponseDto response)
    {
        var answer = string.IsNullOrWhiteSpace(response.Answer)
            ? "Не нашёл достаточно контекста для ответа."
            : response.Answer;

        if (response.Sources.Count == 0)
            return TrimForTelegram(answer);

        var sources = string.Join(
            Environment.NewLine,
            response.Sources.Take(3).Select(source =>
                $"- {source.LectureTitle}, чанк #{source.ChunkIndex}, {source.Position}"));

        return TrimForTelegram($"""
        {answer}

        Источники:
        {sources}
        """);
    }

    private static string TrimForTelegram(string text)
    {
        const int maxLength = 4000;

        if (text.Length <= maxLength)
            return text;

        return text[..maxLength] + "...";
    }
}