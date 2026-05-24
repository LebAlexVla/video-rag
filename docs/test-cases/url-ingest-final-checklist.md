# URL audio ingest — final manual checklist

Итоговый чеклист для проверки всей фичи.

## 1. Dependencies

```powershell
dotnet --list-sdks
python --version
yt-dlp --version
ffmpeg -version
docker compose version
```

Ожидаемо:

- .NET 8 SDK установлен;
- Python доступен;
- `yt-dlp` доступен;
- `ffmpeg` доступен;
- Docker Compose доступен.

## 2. Infrastructure

```powershell
docker compose up -d
```

Проверить Qdrant:

```powershell
Invoke-WebRequest http://localhost:6333
```

## 3. Build

```powershell
dotnet build
```

Ожидаемо: сборка проходит без ошибок.

## 4. CLI

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-test-url-ingest-cli.ps1 -Url "https://rutube.ru/video/..."
```

## 5. API

Запустить backend:

```powershell
dotnet run
```

Проверить API:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-test-url-ingest-api.ps1 -Url "https://rutube.ru/video/..."
```

## 6. Razor UI

```powershell
dotnet run --project .\clients\VideoRag.WebUi\VideoRag.WebUi.csproj
```

Проверить вручную:

- форма URL ingest;
- job status;
- вопрос после успешной загрузки.

## 7. Telegram bot

```powershell
$env:Telegram__BotToken="your_bot_token"
dotnet run --project .\clients\VideoRag.TelegramBot\VideoRag.TelegramBot.csproj
```

Проверить вручную:

```text
/health
/add <url>
/status <jobId>
```

## 8. Regression

Обязательно проверить старые сценарии:

```powershell
dotnet run -- ingest "data/videos/lecture_0.mp4"
dotnet run -- rebuild
```

И `/ask`:

```powershell
Invoke-RestMethod `
  -Uri "http://localhost:5000/ask" `
  -Method Post `
  -ContentType "application/json" `
  -Body '{
    "question": "О чём эта лекция?",
    "topK": 5,
    "minScore": 0.3
  }'
```

## 9. Ограничения, которые считаются нормальными для MVP

- поддерживаются публичные Rutube/VK ссылки;
- приватные видео и видео с авторизацией не поддерживаются;
- скачивается только аудио;
- job storage in-memory, после перезапуска API статусы старых jobs теряются;
- для смены embedding provider/model нужен rebuild.
