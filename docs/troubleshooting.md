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