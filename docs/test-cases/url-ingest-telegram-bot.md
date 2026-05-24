# URL audio ingest — Telegram bot test cases

Проверяет добавление лекции по Rutube/VK ссылке через Telegram bot.

## Предусловия

Backend API запущен:

```powershell
dotnet run
```

Telegram bot запущен:

```powershell
$env:Telegram__BotToken="your_bot_token"
dotnet run --project .\clients\VideoRag.TelegramBot\VideoRag.TelegramBot.csproj
```

## TG-01 — health check

В Telegram отправить:

```text
/health
```

Ожидаемый результат:

```text
API доступен.
```

## TG-02 — help

Отправить:

```text
/help
```

Ожидаемый результат:

- в справке есть `/add <url>`;
- в справке есть `/status <jobId>`;
- описано, что поддерживаются Rutube/VK.

## TG-03 — добавление Rutube URL

Отправить:

```text
/add https://rutube.ru/video/...
```

Ожидаемый результат:

- бот создаёт ingest job;
- возвращает Job ID;
- предлагает команду `/status <jobId>`.

## TG-04 — проверка статуса

Отправить:

```text
/status <jobId>
```

Ожидаемый результат:

- бот показывает статус;
- при обработке показывает этап;
- при успехе пишет, что лекция добавлена;
- при ошибке показывает причину без stack trace.

## TG-05 — добавление VK URL

Повторить TG-03/TG-04 для публичной VK-ссылки.

## TG-06 — вопрос после ingest

После успешного ingest отправить обычный вопрос сообщением.

Ожидаемый результат:

- бот возвращает ответ;
- источники указывают на добавленную лекцию.

## TG-07 — ошибки ввода

Проверить:

```text
/add
/status
/status not-a-guid
/add https://example.com/video
```

Ожидаемый результат:

- бот отвечает понятными сообщениями;
- не падает;
- обычный polling Telegram продолжается.

## Smoke pre-check

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-test-url-ingest-telegram.ps1
```
