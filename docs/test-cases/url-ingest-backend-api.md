# URL audio ingest: backend/core and API smoke tests

Проверки для сценария:

```text
Rutube / VK URL -> audio download -> old ingest pipeline -> transcript -> chunks -> embeddings -> Qdrant -> /ask
```

## 1. Подготовка

Установить зависимости:

```powershell
python -m pip install -U yt-dlp
ffmpeg -version
```

Поднять Qdrant:

```powershell
docker compose up -d
```

Запустить основное приложение:

```powershell
dotnet run
```

Проверить health:

```powershell
Invoke-RestMethod http://localhost:5000/health
```

## 2. Backend/core smoke test через CLI

Rutube:

```powershell
dotnet run -- ingest-url "https://rutube.ru/video/..." --title "Rutube test lecture"
```

VK:

```powershell
dotnet run -- ingest-url "https://vk.com/video..." --title "VK test lecture"
```

Ожидаемо:

```text
data/downloads/audio содержит скачанный аудиофайл
data/transcripts содержит transcript JSON
data/registry/lectures.json содержит новую лекцию
chunks записаны в Qdrant
команда завершается без stack trace
```

## 3. API: создать ingest job

```powershell
$body = @{
  url = "https://rutube.ru/video/..."
  lectureTitle = "Rutube API test"
} | ConvertTo-Json

$start = Invoke-RestMethod `
  -Uri "http://localhost:5000/ingest/url" `
  -Method Post `
  -ContentType "application/json" `
  -Body $body

$start
```

Ожидаемо:

```text
jobId заполнен
status = Queued или Running
statusUrl = /ingest/jobs/{jobId}
```

## 4. API: проверить статус job

```powershell
Invoke-RestMethod "http://localhost:5000/ingest/jobs/$($start.jobId)"
```

Повторять, пока статус не станет:

```text
Succeeded
```

или:

```text
Failed
```

При успехе ожидаемо:

```text
lectureTitle заполнен
localAudioPath заполнен
transcriptPath заполнен
error пустой
```

## 5. API: проверить /ask после ingest

```powershell
$askBody = @{
  question = "О чём была лекция?"
  topK = 5
  minScore = 0.5
} | ConvertTo-Json

Invoke-RestMethod `
  -Uri "http://localhost:5000/ask" `
  -Method Post `
  -ContentType "application/json" `
  -Body $askBody
```

Ожидаемо:

```text
usedContext = true
answer содержит ответ по лекции
sources содержит новую лекцию
```

## 6. Негативные сценарии

Неподдерживаемый URL:

```powershell
$body = @{
  url = "https://example.com/video"
  lectureTitle = "Unsupported"
} | ConvertTo-Json

Invoke-RestMethod `
  -Uri "http://localhost:5000/ingest/url" `
  -Method Post `
  -ContentType "application/json" `
  -Body $body
```

Ожидаемо:

```text
job создаётся и завершается Failed
или API сразу возвращает 400, если validation выполняется до постановки в очередь
ingest pipeline не запускается после ошибки скачивания
```

## 7. UI smoke test

1. Запустить основной API.
2. Запустить внешний Web UI client:

```powershell
dotnet run --project .\clients\VideoRag.WebUi\VideoRag.WebUi.csproj
```

3. Открыть Web UI.
4. Вставить Rutube / VK-ссылку.
5. Нажать `Добавить лекцию`.
6. Проверять статус по Job ID.
7. После успеха задать вопрос по лекции.

## 8. Telegram smoke test

1. Запустить основной API.
2. Запустить Telegram bot:

```powershell
dotnet run --project .\clients\VideoRag.TelegramBot\VideoRag.TelegramBot.csproj
```

3. В Telegram отправить:

```text
/add https://rutube.ru/video/...
```

4. Проверить статус:

```text
/status <jobId>
```

5. После успеха отправить обычный вопрос по лекции.
