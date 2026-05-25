# Video Lecture RAG Assistant

Локальная система для вопросов по видеолекциям.

Система принимает локальный видеофайл, создаёт транскрипт, индексирует фрагменты лекции и позволяет задавать вопросы по загруженным материалам. Ответ формируется на основе найденного контекста и возвращается с источниками.

## Документация

Главная навигация:

- [Docs](docs/README.md)

Основные документы:

- [Quick start](docs/quick-start.md) — локальный запуск проекта.
- [Overview](docs/team/overview.md) — краткое описание системы.
- [Architecture](docs/team/architecture.md) — архитектура и границы компонентов.
- [Implementation guide](docs/team/implementation-guide.md) — правила разработки.
- [LLM context](docs/llm/llm-context.md) — контекст для AI/LLM-агентов.
- [ADR](docs/adr/README.md) — архитектурные решения.

## Возможности

- ingest локального видеофайла;
- транскрибация видео;
- индексация фрагментов лекции в Qdrant;
- вопросы по загруженным лекциям через HTTP API;
- ответ с источниками;
- ручной rebuild индекса;
- локальный Razor UI;
- внешние клиенты: Telegram bot и Web UI.

## Быстрый запуск

Подробно: [docs/quick-start.md](docs/quick-start.md).

Коротко:

```bash
docker compose up -d
pip install -r scripts/python-helper/requirements.txt
dotnet run -- ingest "data/videos/lecture_0.mp4"
dotnet run
```

После запуска:

```text
http://localhost:5000
```

Health check:

```text
http://localhost:5000/health
```

## Основные команды

```bash
dotnet run -- ingest "data/videos/lecture_0.mp4"
dotnet run
dotnet run -- rebuild
```

Клиенты:

```bash
dotnet run --project .\clients\VideoRag.TelegramBot\VideoRag.TelegramBot.csproj
dotnet run --project .\clients\VideoRag.WebUi\VideoRag.WebUi.csproj
```

## Структура проекта

```text
src/                    основной C# код
Pages/                  встроенный Razor UI
clients/                внешние клиенты
shared/                 общие DTO-контракты
scripts/python-helper/  Python helper для транскрибации
docs/                   документация
data/                   локальные данные
```

## Конфигурация

Основные настройки:

```text
appsettings.json
```

Локальные переопределения:

```text
appsettings.Local.json
```

API-ключи можно передать через `.env`:

```env
DEEPSEEK_API_KEY=your_deepseek_api_key_here
GEMINI_API_KEY=your_gemini_api_key_here
```

Для локального режима через Ollama:

```powershell
Copy-Item appsettings.Ollama.example.json appsettings.Local.json
```

## Локальные данные

```text
data/videos/       исходные видео
data/transcripts/  transcript JSON
data/jobs/         input/output JSON для Python helper
data/registry/     registry для rebuild
```

Локальные данные не предназначены для коммита в Git.

<!-- URL_AUDIO_INGEST_README_START -->
## Добавление лекции по Rutube / VK ссылке

Проект поддерживает добавление лекций по публичным ссылкам Rutube и VK. В этом сценарии скачивается только аудио, после чего запускается обычный ingest pipeline.

CLI:

```bash
dotnet run -- ingest-url "https://rutube.ru/video/..." --title "Lecture title"
dotnet run -- ingest-url "https://vk.com/video..." --title "Lecture title"
```

Backend API для UI и Telegram bot:

```text
POST /ingest/url
GET  /ingest/jobs/{jobId}
```

Проверочные материалы:

```text
docs/test-cases/url-ingest-final-checklist.md
scripts/smoke-test-url-ingest-cli.ps1
scripts/smoke-test-url-ingest-api.ps1
```

Ограничения MVP:

- поддерживаются публичные Rutube/VK ссылки;
- приватные видео и видео с авторизацией не поддерживаются;
- нужен `yt-dlp`;
- нужен `ffmpeg`;
- скачивается только аудио, картинка видео не сохраняется как целевой артефакт.
<!-- URL_AUDIO_INGEST_README_END -->
