# Quick start

Практическая инструкция локального запуска проекта с нуля.

Подробные объяснения архитектуры находятся в [Architecture](./team/architecture.md). Типовые ошибки запуска собраны в [Troubleshooting](./troubleshooting.md).

## 1. Установить зависимости

Нужно установить:

- .NET 9 SDK
- Docker
- Python 3.10+
- Ollama (только для embeddings)

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

Проверить Ollama:

```bash
ollama --version
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

## 4. Подготовить Ollama models (только для embeddings)

Генерация ответа выполняется через DeepSeek API (см. шаг 4a). Ollama нужен только для построения embeddings.

> **Начиная с ADR-014 embeddings переведены на Google Gemini API. Ollama больше не требуется для работы системы.** Раздел оставлен для справки на случай возврата к локальному провайдеру.

Если нужен локальный fallback — установить Ollama и модель:

```bash
ollama pull embeddinggemma
```

Затем переключить в `appsettings.json`: `"Embeddings.Provider": "ollama"`.

## 4a. Настроить API keys

Генерация ответа выполняется через [DeepSeek API](https://platform.deepseek.com/).
Embeddings выполняются через [Google Gemini API](https://aistudio.google.com/apikey).

Создай файл `.env` в корне проекта:

```bash
DEEPSEEK_API_TOKEN=sk-xxxxxxxxxxxxxxxx
GOOGLE_API_TOKEN=AIzaSyxxxxxxxxxxxxxxxx
```

Файл `.env` уже добавлен в `.gitignore` и не попадёт в репозиторий.

При старте приложение автоматически читает `.env` и передаёт токены в конфигурацию. Дополнительных действий не требуется.

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

По умолчанию `Answers.Provider` установлен в `deepseek`. API-ключ передаётся через `.env`, а не через `appsettings.json`.
```

Важно: `Qdrant.VectorSize` должен совпадать с размерностью выбранной embedding-модели.

## 6. Подготовить видео

Положи локальный видеофайл в папку:

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

Открой в браузере:

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
