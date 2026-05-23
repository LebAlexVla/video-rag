# Quick start

Практическая инструкция локального запуска проекта.

Подробная архитектура описана в [Architecture](./team/architecture.md). Типовые ошибки запуска собраны в [Troubleshooting](./troubleshooting.md).

## 1. Установить зависимости

Нужно установить:

- .NET 8 SDK;
- Docker;
- Python 3.10+;
- ffmpeg, если transcription provider требует его для чтения видео;
- Ollama, если используется локальный provider вместо API providers.

Проверить .NET:

```bash
dotnet --list-sdks
dotnet --list-runtimes
```

Проверить Docker:

```bash
docker --version
docker compose version
```

Проверить Python:

```bash
python --version
```

## 2. Установить Python dependencies

Из корня проекта:

```bash
pip install -r scripts/python-helper/requirements.txt
```

Если helper падает на чтении видео или аудио, проверь `ffmpeg`:

```bash
ffmpeg -version
```

## 3. Поднять Qdrant

Из корня проекта:

```bash
docker compose up -d
```

Проверить контейнер:

```bash
docker ps
```

Qdrant должен быть доступен:

```text
http://localhost:6333
```

Dashboard:

```text
http://localhost:6333/dashboard
```

Коллекция Qdrant создаётся приложением автоматически при запуске, если её ещё нет.

## 4. Настроить providers

По умолчанию проект может использовать API providers для embeddings и генерации ответа.

Создать файл `.env` в корне проекта:

```env
DEEPSEEK_API_KEY=your_deepseek_api_key_here
GEMINI_API_KEY=your_gemini_api_key_here
```

Файл `.env` не коммитится в Git.

Основные настройки находятся в:

```text
appsettings.json
```

Локальные переопределения можно хранить в:

```text
appsettings.Local.json
```

Этот файл не должен коммититься.

## 5. Локальный режим через Ollama

Если нужен локальный режим или нет API-ключей, можно переключиться на Ollama.

Проверить Ollama:

```bash
ollama --version
```

Скачать модели:

```bash
ollama pull embeddinggemma
ollama pull llama3.1
```

Создать локальный override-конфиг:

```powershell
Copy-Item appsettings.Ollama.example.json appsettings.Local.json
```

После смены embedding provider или model нужно пересобрать индекс.

## 6. Проверить ключевые настройки

Проверь секции:

```text
Qdrant
Embeddings
Answers
PythonHelper
Paths
Chunking
```

Важно:

- `Qdrant.VectorSize` должен совпадать с размерностью выбранной embedding-модели;
- embeddings разных моделей нельзя смешивать в одной collection;
- при смене embedding provider, model, vector size или embedding space нужен rebuild или повторный ingest.


## 7. Подготовить видео

Положить локальный видеофайл в папку:

```text
data/videos/
```

Пример:

```text
data/videos/lecture_0.mp4
```

## 8. Выполнить ingest

Минимальный запуск:

```bash
dotnet run -- ingest "data/videos/lecture_0.mp4"
```

Запуск с параметрами:

```bash
dotnet run -- ingest "data/videos/lecture_0.mp4" --title "Lecture 0" --language ru --transcription-provider faster-whisper --transcription-model small --overwrite true
```

После успешного ingest в консоли должны появиться:

```text
LectureId
LectureTitle
TranscriptPath
ChunkCount
```

## 9. Запустить основное приложение

```bash
dotnet run
```

По умолчанию приложение доступно локально:

```text
http://localhost:5000
```

Health check:

```text
http://localhost:5000/health
```


Встроенный Razor UI:

```text
http://localhost:5000
```

## 10. Проверить `/ask`

PowerShell:

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

Пример request body:

```json
{
  "question": "О чём эта лекция?",
  "topK": 5,
  "minScore": 0.3
}
```

Ожидаемый результат:

- `usedContext: true` и ответ с sources;
- либо `usedContext: false` и fallback-сообщение, если контекста недостаточно.

## 11. Выполнить rebuild

Rebuild перечитывает сохранённые `transcript.json`, заново выполняет chunking, embeddings и запись в Qdrant.

```bash
dotnet run -- rebuild
```

С явным параметром:

```bash
dotnet run -- rebuild --clear-index-first true
```

## 12. Смена embedding model или vector size

Если меняется embedding provider, embedding model или vector size, старый индекс считается несовместимым.

Минимальный порядок действий:

1. Остановить приложение.
2. Обновить конфигурацию.
3. Удалить старую Qdrant collection или volume, если изменилась размерность.
4. Запустить Qdrant.
5. Выполнить `ingest` заново или `rebuild`, если transcript-файлы сохранены.

Удалить все данные Qdrant volume:

```bash
docker compose down -v
docker compose up -d
```

Затем снова выполнить ingest или rebuild:

```bash
dotnet run -- ingest "data/videos/lecture_0.mp4"
```

или:

```bash
dotnet run -- rebuild
```

## 13. Запуск Telegram bot

Основное приложение должно быть запущено отдельно:

```bash
dotnet run
```

Указать Telegram token.

PowerShell, только для текущей сессии:

```powershell
$env:Telegram__BotToken="your_bot_token"
dotnet run --project .\clients\VideoRag.TelegramBot\VideoRag.TelegramBot.csproj
```

Бот по умолчанию обращается к API:

```text
http://localhost:5000
```

Адрес API настраивается в конфигурации клиента через `VideoRagApi:BaseUrl`.

## 14. Запуск отдельного Web UI client

Основное приложение должно быть запущено отдельно:

```bash
dotnet run
```

В другом терминале:

```bash
dotnet run --project .\clients\VideoRag.WebUi\VideoRag.WebUi.csproj
```

Web UI client обращается к основному API.

Адрес API настраивается в:

```text
clients/VideoRag.WebUi/appsettings.json
```

## 15. Если что-то не запускается

См. [Troubleshooting](./troubleshooting.md).