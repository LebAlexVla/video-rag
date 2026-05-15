# Quick start

Практическая инструкция локального запуска проекта с нуля.

Подробные объяснения архитектуры находятся в [Architecture](./team/architecture.md). Типовые ошибки запуска собраны в [Troubleshooting](./troubleshooting.md).

## 1. Установить зависимости

Нужно установить:

- .NET 8 SDK
- Docker
- Python 3.10+
- Ollama — только если нет API-ключей (см. шаг 4)

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

На некоторых системах для транскрибации может потребоваться `ffmpeg`. Если helper падает на чтении видео или аудио, см. [Troubleshooting](./troubleshooting.md).

## 3. Поднять Qdrant

Из корня проекта:

```bash
docker compose up -d
```

Проверить контейнер:

```bash
docker ps
```

Qdrant должен быть доступен по адресу:

```text
http://localhost:6333
```

Qdrant dashboard:

```text
http://localhost:6333/dashboard
```

Коллекция Qdrant создаётся приложением автоматически при запуске, если её ещё нет.

## 4. Настроить провайдеры

По умолчанию проект использует cloud/API режим:

- DeepSeek для генерации ответов;
- Google Gemini для embeddings.

Для этого нужен файл `.env` в корне проекта:

```env
DEEPSEEK_API_KEY=your_deepseek_api_key_here
GEMINI_API_KEY=your_gemini_api_key_here
```

Файл `.env` не коммитится в Git.

### Локальный режим через Ollama

Если нет API-ключей или нужен полностью локальный запуск, можно переключиться на Ollama.

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

`appsettings.Local.json` не коммитится. Он автоматически подхватывается приложением и переопределяет настройки из `appsettings.json`.

## 5. Проверить конфигурацию

Основные настройки находятся в `appsettings.json`.

Проверь секции:

```text
Qdrant
Embeddings
Answers
PythonHelper
Paths
Chunking
```

Важно: `Qdrant.VectorSize` должен совпадать с размерностью выбранной embedding-модели.

По умолчанию `Answers.Provider = "deepseek"` и `Embeddings.Provider = "gemini"`. API-ключи передаются через `.env`, а не через `appsettings.json`.

Важно: `Qdrant.VectorSize` должен совпадать с размерностью выбранной embedding-модели.

Важно: при смене embedding provider нужно пересобрать индекс.

Например, если chunks были embedded через Gemini, а потом проект переключили на Ollama, старую Qdrant collection нужно пересоздать. Даже если `Qdrant.VectorSize` совпадает, embedding space у разных моделей разный.

## 6. Подготовить видео

Положить локальный видеофайл в папку:

```text
data/videos/
```

Пример:

```text
data/videos/lecture_0.mp4
```

## 7. Выполнить ingest

Минимальный запуск:

```bash
dotnet run -- ingest "data/videos/lecture_0.mp4"
```

Запуск с параметрами:

```bash
dotnet run -- ingest "data/videos/lecture_0.mp4" --title "Lecture 0" --language ru --transcription-provider faster-whisper --transcription-model small --overwrite true
```

После успешного ingest в консоли должен появиться результат с `LectureId`, `LectureTitle`, `TranscriptPath` и `ChunkCount`.

## 8. Запустить приложение

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

## 9. Открыть Razor UI

Открыть в браузере:

```text
http://localhost:5000
```

Razor UI нужен для локального тестирования вопросов. Основной API-контракт проекта остаётся `POST /ask`.

## 10. Проверить Minimal API /ask

PowerShell:

```powershell
Invoke-RestMethod `
  -Uri "http://localhost:5000/ask" `
  -Method Post `
  -ContentType "application/json" `
  -Body '{
    "question": "О чём эта лекция?",
    "topK": 5,
    "minScore": 0.1
  }'
```

Пример тела запроса:

```json
{
  "question": "О чём эта лекция?",
  "topK": 5,
  "minScore": 0.1
}
```

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

Если меняется embedding provider, embedding model или размерность вектора, старый индекс считается несовместимым.

Минимальный порядок действий:

1. Остановить приложение.
2. Обновить `appsettings.json`.
3. Удалить старую Qdrant collection или volume.
4. Выполнить `ingest` заново или запустить `rebuild`, если сохранённые `transcript.json` совместимы с новой конфигурацией.

Для удаления всех данных Qdrant volume:

```bash
docker compose down -v
docker compose up -d
```

После этого снова выполнить ingest:

```bash
dotnet run -- ingest "data/videos/lecture_0.mp4"
```

## 13. Если что-то не запускается

См. [Troubleshooting](./troubleshooting.md).
