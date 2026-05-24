# Troubleshooting

Типовые проблемы локального запуска и способы исправления.

## .NET 8 SDK/runtime не установлен

### Симптомы

```text
You must install or update .NET to run this application.
Framework: 'Microsoft.NETCore.App', version '8.0.0'
```

или проект не собирается через `dotnet run`.

### Причина

Установлен другой .NET SDK/runtime, но нет .NET 8.

### Исправление

Установить .NET 8 SDK и проверить:

```bash
dotnet --list-sdks
dotnet --list-runtimes
```

В списке должен быть SDK/runtime версии `8.0.x`.

---

## Qdrant не запущен

### Симптомы

`dotnet run`, `ingest` или `ask` падают с ошибкой подключения к `localhost:6333`.

### Причина

Контейнер Qdrant не запущен или порт `6333` недоступен.

### Исправление

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

### Симптомы

Приложение падает при старте или ingest с ошибкой о несовпадении vector size.

Также возможна ошибка Qdrant при upsert points.

### Причина

Qdrant collection была создана под другую embedding-модель или другую размерность вектора.

### Исправление

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

### Симптомы

Запросы к `localhost:11434` не проходят.

Пример:

```powershell
Invoke-WebRequest http://localhost:11434/api/version
```

возвращает ошибку подключения.

### Причина

Ollama не запущена или слушает другой адрес/порт.

### Исправление

Запустить Ollama и проверить:

```powershell
Invoke-WebRequest http://localhost:11434/api/version
```

Должен вернуться JSON с версией Ollama.

---

## Модель Ollama не найдена

### Симптомы

Запрос к Ollama возвращает ошибку, что модель не найдена.

### Причина

Модель, указанная в `appsettings.json`, не установлена локально.

### Исправление

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

### Симптомы

Во время ingest видна ошибка:

```text
POST http://localhost:11434/api/embed
404 Not Found
```

### Причина

Возможные причины:

- используется старая версия Ollama;
- Ollama API запущен некорректно;
- endpoint embeddings недоступен в текущей установке.

### Исправление

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

### Симптомы

Ingest падает с ошибкой `helper_not_found`, ошибкой запуска Python или сообщением, что helper script не найден.

### Причина

Неверно указан Python executable или путь к helper script.

### Исправление

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

### Симптомы

В stderr видно, что Python выполняет файл вида:

```text
data/jobs/...input.json
```

и падает, например, на `NameError: name 'null' is not defined`.

### Причина

C# runner запускает Python с `input.json` вместо `main.py`.

Неправильно:

```text
python input.json output.json
```

Правильно:

```text
python scripts/python-helper/main.py input.json output.json
```

### Исправление

Проверить `PythonTranscriptionRunner`: первым аргументом после `python` должен быть путь к `main.py`.

---

## `transcript_not_found`

### Симптомы

Ingest падает с ошибкой:

```text
transcript_not_found
Helper finished successfully but transcript JSON was not created.
```

### Причина

Python helper создал transcript не там, где его ждёт C#.

Обычно причина — относительный путь к `outputTranscriptPath` или неверная рабочая директория.

### Исправление

C# должен передавать helper’у абсолютный путь к transcript-файлу.

Проверить, что путь в ошибке совпадает с реальным местом создания файла.

---

## `faster-whisper` / `ffmpeg` проблемы

### Симптомы

Python helper падает во время чтения видео или аудио.

В ошибке могут встречаться слова:

```text
ffmpeg
audio
decode
No such file
Invalid data
```

### Причина

`faster-whisper` не может прочитать входной видео/аудиофайл.

Возможные причины:

- не установлены Python dependencies;
- отсутствует ffmpeg;
- файл повреждён;
- формат файла не поддерживается окружением.

### Исправление

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

### Симптомы

CLI/API job падает на этапе скачивания аудио.

### Причина

`yt-dlp` не установлен или не доступен из PATH.

### Исправление

```bash
python -m pip install -U yt-dlp
yt-dlp --version
```

---

## URL ingest: `ffmpeg` не найден

### Симптомы

`yt-dlp` скачивает источник, но не может извлечь/конвертировать аудио.

### Причина

Для `--extract-audio` и `--audio-format mp3` нужен FFmpeg.

### Исправление

Установить FFmpeg и проверить:

```bash
ffmpeg -version
```

---

## URL ingest: не удалось скачать Rutube/VK

### Симптомы

Job завершается со статусом `Failed` на этапе `DownloadingAudio`.

### Возможные причины

- ссылка не публичная;
- видео удалено;
- видео требует авторизацию;
- Rutube/VK изменили способ отдачи медиа;
- установлена старая версия `yt-dlp`.

### Исправление

Обновить `yt-dlp`:

```bash
python -m pip install -U yt-dlp
```

Проверить ссылку вручную:

```bash
yt-dlp -f "bestaudio/worst[acodec!=none]" --extract-audio --audio-format m4a "URL"
```

Для MVP приватные видео и видео с авторизацией не поддерживаются.

---

## URL ingest job зависла в `Running`

### Симптомы

`GET /ingest/jobs/{jobId}` долго возвращает `Running`.

### Возможные причины

- длинная лекция;
- медленная транскрибация;
- завис внешний provider embeddings;
- проблема с Qdrant;
- процесс скачивания или транскрибации ждёт системный ресурс.

### Исправление

Проверить логи backend API, Qdrant, доступность provider и свободное место на диске.

Для локального MVP job storage хранится в памяти. После перезапуска API старые статусы job теряются.

---

## Telegram `/add` не работает

### Симптомы

Бот отвечает, что API недоступен, или не может создать ingest job.

### Исправление

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

## Streaming-извлечение аудио не создаёт `.mp4.part`

### Симптомы

Во время Rutube/VK ingest в `data/downloads/audio` появляется большой временный файл `.mp4.part`.

### Причина

Downloader использует режим post-processing через `yt-dlp`. Если платформа не отдаёт отдельный audio-only поток, `yt-dlp` может сначала скачать muxed-поток `video+audio`, а уже потом извлечь аудио.

### Исправление

Использовать streaming-режим извлечения аудио:

```json
"AudioDownloader": {
  "Format": "bestaudio/worst[acodec!=none]",
  "AudioFormat": "m4a",
  "UseStreamingFfmpegCopy": true
}
```

В этом режиме приложение получает прямой media URL через `yt-dlp -g`, затем запускает `ffmpeg -vn -map 0:a:0 -c:a copy`. Итоговый артефакт — аудиофайл, а полноценный временный видеофайл не накапливается на диске.

Если платформа не предоставляет отдельный audio-only поток, сетевой трафик всё равно включает минимальный поток с аудио, но видео не сохраняется как итоговый артефакт.

---

## VK direct streaming URL возвращает 400 Bad Request

### Симптомы

URL ingest для VK падает с ошибкой вида:

```text
ffmpeg failed to extract audio from the streamed media URL.
Server returned 400 Bad Request
```

### Причина

Прямые media URL от VK/CDN, полученные через `yt-dlp -g`, могут требовать headers, cookies или обработку JS challenge. Обычный `ffmpeg` при открытии прямой ссылки эти детали не воспроизводит.

### Исправление

Оставить включённым автоматический fallback:

```json
"AudioDownloader": {
  "UseStreamingFfmpegCopy": true,
  "FallbackToYtDlpPostProcessing": true
}
```

Сначала приложение пробует быстрый streaming-путь. Если `ffmpeg` не может открыть прямой URL, приложение переходит на `yt-dlp --extract-audio`, который умеет обрабатывать VK-специфичные детали доступа.

