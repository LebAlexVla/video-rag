# Troubleshooting

Типовые проблемы локального запуска и способы исправления.

## .NET 8 SDK/runtime не установлен

### Symptoms

```text
You must install or update .NET to run this application.
Framework: 'Microsoft.NETCore.App', version '8.0.0'
```

или проект не собирается через `dotnet run`.

### Cause

Установлен другой .NET SDK/runtime, но нет .NET 8.

### Fix

Установить .NET 8 SDK и проверить:

```bash
dotnet --list-sdks
dotnet --list-runtimes
```

В списке должен быть SDK/runtime версии `8.0.x`.

---

## Qdrant не запущен

### Symptoms

`dotnet run`, `ingest` или `ask` падают с ошибкой подключения к `localhost:6333`.

### Cause

Контейнер Qdrant не запущен или порт `6333` недоступен.

### Fix

Запустить инфраструктуру:

```bash
docker compose up -d
```

Проверить контейнер:

```bash
docker ps
```

Проверить Qdrant:

```powershell
Invoke-WebRequest http://localhost:6333
```

---

## Qdrant collection / vector size mismatch

### Symptoms

Приложение падает при старте или ingest с ошибкой о несовпадении vector size.

Также возможна ошибка Qdrant при upsert points.

### Cause

Qdrant collection была создана под другую embedding-модель или другую размерность вектора.

### Fix

Проверить `Qdrant.VectorSize` в `appsettings.json`.

Если изменилась embedding-модель или размерность, удалить старый volume/collection и пересоздать индекс:

```bash
docker compose down -v
docker compose up -d
```

Затем выполнить ingest или rebuild заново:

```bash
dotnet run -- ingest "data/videos/lecture_0.mp4"
```

---

## Ollama не отвечает

### Symptoms

Запросы к `localhost:11434` не проходят.

Пример:

```powershell
Invoke-WebRequest http://localhost:11434/api/version
```

возвращает ошибку подключения.

### Cause

Ollama не запущена или слушает другой адрес/порт.

### Fix

Запустить Ollama и проверить:

```powershell
Invoke-WebRequest http://localhost:11434/api/version
```

Должен вернуться JSON с версией Ollama.

---

## Ollama model not found

### Symptoms

Запрос к Ollama возвращает ошибку, что модель не найдена.

### Cause

Модель, указанная в `appsettings.json`, не установлена локально.

### Fix

Посмотреть установленные модели:

```bash
ollama list
```

Установить нужные модели:

```bash
ollama pull embeddinggemma
ollama pull llama3.1
```

Названия моделей должны совпадать с `Embeddings.Ollama.Model` и `Answers.Ollama.Model` в `appsettings.json`.

---

## `/api/embed` возвращает 404

### Symptoms

Во время ingest видна ошибка:

```text
POST http://localhost:11434/api/embed
404 Not Found
```

### Cause

Возможные причины:

- используется старая версия Ollama;
- Ollama API запущен некорректно;
- endpoint embeddings недоступен в текущей установке.

### Fix

Проверить API:

```powershell
Invoke-WebRequest http://localhost:11434/api/version
Invoke-WebRequest http://localhost:11434/api/tags
```

Обновить Ollama до актуальной версии.

Проверить embedding-запрос вручную:

```powershell
Invoke-WebRequest `
  -Uri "http://localhost:11434/api/embed" `
  -Method Post `
  -ContentType "application/json" `
  -Body '{
    "model": "embeddinggemma",
    "input": "test"
  }'
```

Если модель не установлена:

```bash
ollama pull embeddinggemma
```

---

## Python helper не запускается

### Symptoms

Ingest падает с ошибкой `helper_not_found`, ошибкой запуска Python или сообщением, что helper script не найден.

### Cause

Неверно указан Python executable или путь к helper script.

### Fix

Проверить `appsettings.json`:

```json
"PythonHelper": {
  "PythonExecutable": "python",
  "ScriptPath": "scripts/python-helper/main.py"
}
```

Проверить Python:

```bash
python --version
```

Проверить, что файл существует:

```text
scripts/python-helper/main.py
```

---

## Python пытается выполнить `input.json`

### Symptoms

В stderr видно, что Python выполняет файл вида:

```text
data/jobs/...input.json
```

и падает, например, на `NameError: name 'null' is not defined`.

### Cause

C# runner запускает Python с `input.json` вместо `main.py`.

Неправильно:

```text
python input.json output.json
```

Правильно:

```text
python scripts/python-helper/main.py input.json output.json
```

### Fix

Проверить `PythonTranscriptionRunner`: первым аргументом после `python` должен быть путь к `main.py`.

---

## `transcript_not_found`

### Symptoms

Ingest падает с ошибкой:

```text
transcript_not_found
Helper finished successfully but transcript JSON was not created.
```

### Cause

Python helper создал transcript не там, где его ждёт C#.

Обычно причина — относительный путь к `outputTranscriptPath` или неверная рабочая директория.

### Fix

C# должен передавать helper’у абсолютный путь к transcript-файлу.

Проверить, что путь в ошибке совпадает с реальным местом создания файла.

---

## `faster-whisper` / `ffmpeg` проблемы

### Symptoms

Python helper падает во время чтения видео или аудио.

В ошибке могут встречаться слова:

```text
ffmpeg
audio
decode
No such file
Invalid data
```

### Cause

`faster-whisper` не может прочитать входной видео/аудиофайл.

Возможные причины:

- не установлены Python dependencies;
- отсутствует ffmpeg;
- файл повреждён;
- формат файла не поддерживается окружением.

### Fix

Установить зависимости:

```bash
pip install -r scripts/python-helper/requirements.txt
```

Проверить видеофайл другим плеером.

Установить ffmpeg и убедиться, что он доступен из PATH:

```bash
ffmpeg -version
```

<!-- URL_AUDIO_INGEST_TROUBLESHOOTING_START -->
## URL ingest: `yt-dlp` не найден

### Symptoms

CLI/API job падает на этапе скачивания аудио.

### Cause

`yt-dlp` не установлен или не доступен из PATH.

### Fix

```bash
python -m pip install -U yt-dlp
yt-dlp --version
```

---

## URL ingest: `ffmpeg` не найден

### Symptoms

`yt-dlp` скачивает источник, но не может извлечь/конвертировать аудио.

### Cause

Для `--extract-audio` и `--audio-format mp3` нужен FFmpeg.

### Fix

Установить FFmpeg и проверить:

```bash
ffmpeg -version
```

---

## URL ingest: Rutube/VK download failed

### Symptoms

Job завершается со статусом `Failed` на этапе `DownloadingAudio`.

### Possible causes

- ссылка не публичная;
- видео удалено;
- видео требует авторизацию;
- Rutube/VK изменили способ отдачи медиа;
- старая версия `yt-dlp`.

### Fix

Обновить `yt-dlp`:

```bash
python -m pip install -U yt-dlp
```

Проверить ссылку вручную:

```bash
yt-dlp -f bestaudio/best --extract-audio --audio-format mp3 "URL"
```

Для MVP приватные видео и видео с авторизацией не поддерживаются.

---

## URL ingest job stuck in `Running`

### Symptoms

`GET /ingest/jobs/{jobId}` долго возвращает `Running`.

### Possible causes

- длинная лекция;
- медленная транскрибация;
- завис внешний provider embeddings;
- проблема с Qdrant;
- процесс скачивания/транскрибации ждёт системный ресурс.

### Fix

Проверить логи backend API, Qdrant, доступность provider и наличие свободного места на диске.

Для локального MVP job storage in-memory. После перезапуска API старые job status теряются.

---

## Telegram `/add` не работает

### Symptoms

Бот отвечает, что API недоступен, или не может создать ingest job.

### Fix

Проверить:

```powershell
Invoke-RestMethod http://localhost:5000/health
```

Проверить конфигурацию клиента:

```text
clients/VideoRag.TelegramBot/appsettings.json
VideoRagApi:BaseUrl
```

Backend API должен быть запущен отдельно.
<!-- URL_AUDIO_INGEST_TROUBLESHOOTING_END -->

---

## Streaming audio extraction creates no `.mp4.part`

### Symptoms

During Rutube/VK ingest, a large temporary `.mp4.part` file appears in `data/downloads/audio`.

### Cause

The downloader is using yt-dlp post-processing mode. If the platform does not expose an audio-only stream, yt-dlp may first download a muxed video+audio stream and only then extract audio.

### Fix

Use streaming extraction mode:

```json
"AudioDownloader": {
  "Format": "bestaudio/worst[acodec!=none]",
  "AudioFormat": "m4a",
  "UseStreamingFfmpegCopy": true
}
```

In this mode the application resolves a direct media URL through `yt-dlp -g`, then runs `ffmpeg -vn -map 0:a:0 -c:a copy`. The final artifact is audio-only, and a full temporary video file is not accumulated on disk.

If the platform does not provide an audio-only stream, network traffic still includes the smallest available stream that contains audio, but the video stream is not saved as a final artifact.


---

## VK direct streaming URL returns 400 Bad Request

### Symptoms

URL ingest fails on VK with an error similar to:

```text
ffmpeg failed to extract audio from the streamed media URL.
Server returned 400 Bad Request
```

### Cause

VK/CDN direct media URLs returned by `yt-dlp -g` may require headers, cookies or challenge handling that plain ffmpeg does not reproduce.

### Fix

Keep automatic fallback enabled:

```json
"AudioDownloader": {
  "UseStreamingFfmpegCopy": true,
  "FallbackToYtDlpPostProcessing": true
}
```

The application first tries the fast streaming path. If ffmpeg cannot open the direct URL, it falls back to `yt-dlp --extract-audio`, which handles VK-specific access details.
