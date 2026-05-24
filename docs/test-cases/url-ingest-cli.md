# URL audio ingest — CLI test cases

Проверяет административный сценарий `ingest-url`.

## Предусловия

- `dotnet build` проходит.
- Qdrant запущен: `docker compose up -d`.
- Python dependencies установлены.
- `yt-dlp` доступен из PATH.
- `ffmpeg` доступен из PATH.
- Для embeddings/answers настроены API providers или Ollama.

## CLI-01 — старый ingest локального файла не сломан

Команда:

```powershell
dotnet run -- ingest "data/videos/lecture_0.mp4"
```

Ожидаемый результат:

```text
Ingest completed successfully.
LectureId: ...
LectureTitle: ...
TranscriptPath: ...
ChunkCount: ...
```

Проверить:

- `data/transcripts/*.transcript.json` создан;
- `/ask` продолжает отвечать по локальной лекции.

## CLI-02 — ingest Rutube URL

Команда:

```powershell
dotnet run -- ingest-url "https://rutube.ru/video/..." --title "Rutube smoke"
```

Ожидаемый результат:

- в `data/downloads/audio` появился аудиофайл;
- в `data/transcripts` появился transcript;
- chunks записаны в Qdrant;
- команда завершилась без stack trace.

## CLI-03 — ingest VK URL

Команда:

```powershell
dotnet run -- ingest-url "https://vk.com/video..." --title "VK smoke"
```

Ожидаемый результат аналогичен Rutube.

## CLI-04 — неподдерживаемая ссылка

Команда:

```powershell
dotnet run -- ingest-url "https://example.com/video"
```

Ожидаемый результат:

- команда возвращает ошибку в человекочитаемом виде;
- аудио не скачивается;
- транскрибация не запускается.

## CLI-05 — yt-dlp отсутствует

Временно убрать `yt-dlp` из PATH или проверить на машине без него.

Ожидаемый результат:

- ошибка объясняет, что `yt-dlp` не найден;
- есть подсказка `python -m pip install -U yt-dlp`;
- приложение не падает необработанным stack trace.

## Автоматизированный smoke-test

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-test-url-ingest-cli.ps1 -Url "https://rutube.ru/video/..."
```
