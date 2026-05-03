# LLM Context

Канонический технический контекст для AI/LLM-агентов, которые генерируют, проверяют или рефакторят код проекта.

Если предложение или код противоречит этому документу, нужно считать это архитектурной ошибкой.

## 1. Назначение проекта

Video Lecture RAG Assistant — локальная MVP-система для вопросов по видеолекциям.

Основной сценарий:

```text
local video -> transcription -> transcript.json -> chunks -> embeddings -> Qdrant -> question -> retrieved context -> answer
```

Пользователь задаёт вопрос по загруженным лекциям. Система ищет релевантные фрагменты в транскриптах и формирует ответ только на основе найденного контекста.

## 2. Границы MVP

### Входит в MVP

- локальный запуск;
- ingest локального видеофайла;
- транскрибация видео;
- сохранение `transcript.json`;
- чтение transcript в C#;
- chunking в C#;
- построение embeddings;
- запись chunks в Qdrant;
- Minimal API `/ask`;
- retrieval релевантных chunks;
- генерация ответа на основе найденного контекста;
- sources в ответе;
- ручной rebuild индекса;
- локальный Razor UI как dev/demo-интерфейс.

### Не входит в MVP

- загрузка видео по ссылкам;
- downloader для внешних видеоплатформ;
- deduplication/versioning видео;
- автоматический reindex;
- микросервисы;
- очереди и брокеры сообщений;
- внутренний REST/gRPC между C# и Python;
- auth и роли пользователей;
- chat history;
- multi-tenant режим;
- word-level timestamps;
- OCR слайдов и доски;
- production deployment;
- отдельный frontend-проект;
- полноценный продуктовый web UI.

## 3. Обязательные архитектурные правила

1. C# / .NET — центр системы и единственная точка orchestration.
2. Python helper используется только как CLI-helper для транскрибации.
3. Python helper не выполняет chunking, embeddings, retrieval, answer generation и запись в Qdrant.
4. Связь C# ↔ Python — только через process start, JSON-файлы, exit code, stdout/stderr.
5. Внутренний REST/gRPC/queue между C# и Python запрещён.
6. Chunking выполняется в C#.
7. Qdrant используется только как vector store.
8. Ollama/OpenAI вызываются только через infrastructure providers.
9. Основной контракт MVP — CLI `ingest` и Minimal API `/ask`.
10. Razor UI допустим только как локальный dev/demo UI.
11. Razor PageModel может зависеть от `IAskService`, но не от Infrastructure.
12. Rebuild выполняется вручную.
13. При смене embedding model/provider/vector space нужен rebuild.
14. При смене vector size старая Qdrant collection несовместима.

## 4. Роли компонентов

### C# application

Отвечает за:

- CLI commands;
- Minimal API;
- Razor test UI;
- orchestration ingest / ask / rebuild;
- запуск Python helper;
- чтение `transcript.json`;
- chunking;
- embeddings;
- Qdrant write/search;
- answer generation;
- validation и error mapping.

### Python helper

Отвечает только за:

- чтение локального видеофайла;
- транскрибацию;
- запись `transcript.json`;
- запись helper output JSON;
- exit code.

Не должен содержать RAG-логику.

### Qdrant

Хранит retrieval-представление:

- vectors;
- chunk text;
- chunk metadata;
- lecture metadata.

Не является общей БД приложения.

### Ollama

Локальный provider для:

- embeddings;
- answer generation.

Вызывается из Infrastructure через provider implementations.

### Razor UI

Локальная dev/demo-панель для ручного тестирования ask-сценария.

Правила:

- не является основным MVP-контрактом;
- не заменяет `/ask`;
- вызывает application service;
- не вызывает Qdrant/Ollama/Python напрямую;
- не содержит retrieval или prompt logic.

## 5. Сценарии

### Ingest

```text
Admin
 -> CLI ingest
 -> ILectureIngestService
 -> IVideoSource.ResolveAsync
 -> ITranscriptionRunner.RunAsync
 -> Python helper
 -> transcript.json
 -> ITranscriptReader.ReadAsync
 -> IChunker.Chunk
 -> IEmbeddingProvider.EmbedBatchAsync
 -> IVectorStore.UpsertLectureChunksAsync
 -> Qdrant
```

Результат: лекция доступна для поиска.

### Ask

```text
User
 -> Minimal API /ask or Razor UI
 -> IAskService.AskAsync
 -> IContextRetriever.RetrieveAsync
 -> IEmbeddingProvider.EmbedAsync(question)
 -> IVectorStore.SearchAsync
 -> IAnswerGenerator.GenerateAsync
 -> AskResponse
```

Результат: ответ с sources или fallback.

### Rebuild

```text
Admin
 -> CLI rebuild
 -> ILectureIngestService.RebuildAsync
 -> read registry
 -> read saved transcript.json
 -> IChunker.Chunk
 -> IEmbeddingProvider.EmbedBatchAsync
 -> IVectorStore.UpsertLectureChunksAsync
 -> Qdrant
```

Rebuild использует сохранённые transcript-файлы и не обязан повторно запускать Python helper.

## 6. Слои и зависимости

### Domain

Содержит предметные сущности и их инварианты.

Примеры:

- `Lecture`
- `Transcript`
- `TranscriptSegment`
- `LectureChunk`
- `RetrievedContext`
- `SourceCitation`
- `AnswerResult`

Domain не зависит от Application и Infrastructure.

### Application

Содержит:

- `Application/Abstractions` — interfaces;
- `Application/Contracts` — DTO/contracts;
- `Application/Services` — orchestration/application services.

Application зависит от Domain и abstractions, но не от concrete Infrastructure.

### Infrastructure

Содержит concrete implementations:

- video sources;
- transcription runner;
- transcript reader;
- embedding providers;
- vector store;
- answer generators;
- configuration;
- technical initialization.

Infrastructure может зависеть от Application и Domain.

### Pages / API / CLI

Входной слой приложения.

Может вызывать Application services.

Не должен напрямую обращаться к Qdrant, Ollama, OpenAI, Python helper или provider SDK.

### Разрешённые зависимости

```text
Application -> Domain
Infrastructure -> Application
Infrastructure -> Domain
Pages/API/CLI -> Application
Composition root -> Infrastructure
```

### Запрещённые зависимости

```text
Domain -> Application
Domain -> Infrastructure
Application -> Infrastructure concrete classes
Pages/API/CLI -> Infrastructure concrete classes
Python helper -> Qdrant
Python helper -> retrieval/answer generation
```

## 7. Каноническая структура проекта

```text
src/
  Domain/
    Entities/

  Application/
    Abstractions/
    Contracts/
    Services/

  Infrastructure/
    VideoSources/
    Transcription/
    Transcript/
    Embeddings/
    VectorStore/
    Answers/
    Configuration/

Pages/
  Index.cshtml
  Index.cshtml.cs
  _ViewImports.cshtml

scripts/
  python-helper/
    main.py
    requirements.txt

docs/
  README.md
  quick-start.md
  troubleshooting.md
  team/
  llm/
  adr/

data/
  videos/
  transcripts/
  jobs/
  registry/
```

## 8. Официальные interfaces MVP

Interfaces лежат в `Application/Abstractions`.

- `IVideoSource` — нормализует и проверяет источник видео.
- `ITranscriptionRunner` — запускает Python helper и возвращает результат.
- `ITranscriptReader` — читает и валидирует `transcript.json`.
- `IChunker` — превращает transcript в chunks.
- `IEmbeddingProvider` — строит embeddings для текста.
- `IVectorStore` — пишет chunks и ищет context в vector store.
- `IContextRetriever` — строит embedding вопроса, ищет context, применяет `TopK` и `MinScore`.
- `IAnswerGenerator` — генерирует ответ по вопросу и найденному context.
- `ILectureIngestService` — orchestrates ingest и rebuild.
- `IAskService` — orchestrates ask-сценарий.

Не добавлять новые основные interfaces без явной необходимости.

## 9. Application contracts

Contracts лежат в `Application/Contracts`.

Основные contracts:

- `AskRequest`
- `AskResponse`
- `LectureIngestRequest`
- `LectureIngestResult`
- `LectureRebuildRequest`
- `LectureRebuildResult`
- `ContextRetrievalResult`
- `VideoSourceDescriptor`
- `TranscriptionRunRequest`
- `TranscriptionRunResult`
- `EmbeddedLectureChunk`
- `ErrorInfo`

DTO внешних SDK не должны попадать в Application contracts.

## 10. JSON-контракт C# ↔ Python

### Общие правила

- UTF-8 JSON;
- field naming: camelCase;
- Python получает input JSON;
- Python пишет output JSON;
- Python пишет transcript JSON;
- C# продолжает ingest только если:
  - process exit code успешный;
  - output JSON существует;
  - output JSON валиден;
  - `status == "success"`;
  - transcript JSON существует;
  - transcript JSON валиден.

### Input JSON

```json
{
  "jobId": "ingest-20260419-001",
  "inputVideoPath": "/data/videos/lecture1.mp4",
  "outputTranscriptPath": "/data/transcripts/lecture1.transcript.json",
  "requestedTitle": "Lecture 1",
  "languageHint": "ru",
  "transcriptionProvider": "faster-whisper",
  "transcriptionModel": "small",
  "overwrite": true
}
```

Required:

- `jobId`
- `inputVideoPath`
- `outputTranscriptPath`
- `transcriptionProvider`
- `transcriptionModel`
- `overwrite`

Optional:

- `requestedTitle`
- `languageHint`

### Output JSON при успехе

```json
{
  "jobId": "ingest-20260419-001",
  "status": "success"
}
```

### Output JSON при ошибке

```json
{
  "jobId": "ingest-20260419-001",
  "status": "failed",
  "error": {
    "code": "transcription_failed",
    "message": "Failed to transcribe input video",
    "details": {
      "provider": "faster-whisper",
      "model": "small"
    }
  }
}
```

### Transcript JSON

```json
{
  "jobId": "ingest-20260419-001",
  "status": "success",
  "lecture": {
    "title": "Lecture 1",
    "sourceFileName": "lecture1.mp4",
    "sourcePath": "/data/videos/lecture1.mp4",
    "language": "ru",
    "durationSec": 3572.4
  },
  "transcriber": {
    "provider": "faster-whisper",
    "model": "small"
  },
  "segments": [
    {
      "index": 0,
      "startSec": 0.0,
      "endSec": 8.7,
      "text": "..."
    }
  ]
}
```

Required transcript fields:

- root: `jobId`, `status`, `lecture`, `transcriber`, `segments`;
- lecture: `title`, `sourceFileName`, `sourcePath`;
- transcriber: `provider`, `model`;
- segment: `index`, `startSec`, `endSec`, `text`.

Transcript invariants:

- `status == "success"`;
- `segments.length >= 1`;
- segment indexes are ordered without gaps;
- `startSec >= 0`;
- `endSec > startSec`;
- `text.Trim()` is not empty.

### Exit codes

```text
0  success
1  processing failed
2  input validation failed
3  source not found
10 internal helper error
```

## 11. Qdrant payload

Qdrant stores retrieval data only.

Canonical payload:

```json
{
  "chunkId": "lecture-001-chunk-0001",
  "lectureId": "lecture-001",
  "lectureTitle": "Physics Lecture 1",
  "chunkIndex": 1,
  "text": "...",
  "approxMinute": 24,
  "approxStartSec": 1410.0,
  "approxEndSec": 1470.0
}
```

Required:

- `chunkId`
- `lectureId`
- `lectureTitle`
- `chunkIndex`
- `text`
- `approxMinute`

Optional:

- `approxStartSec`
- `approxEndSec`

Qdrant point id must be compatible with Qdrant requirements, for example UUID or integer. Human-readable `chunkId` should be stored in payload.

## 12. AskRequest / AskResponse shape

### AskRequest

```json
{
  "question": "О чём эта лекция?",
  "topK": 5,
  "minScore": 0.1
}
```

Rules:

- `question` required;
- `question.Trim()` not empty;
- max question length: 1000 chars;
- `topK` in range `1..10`;
- `minScore` in range `0..1`.

### AskResponse при успехе

```json
{
  "answer": "Ответ по найденному контексту.",
  "usedContext": true,
  "sources": [
    {
      "lectureTitle": "Lecture 1",
      "chunkIndex": 4,
      "approxMinute": 12,
      "approxStartSec": 720.0,
      "approxEndSec": 780.0
    }
  ],
  "message": null
}
```

### AskResponse fallback

```json
{
  "answer": null,
  "usedContext": false,
  "sources": [],
  "message": "Недостаточно релевантного контекста для уверенного ответа по загруженным лекциям."
}
```

## 13. Fallback rules

Fallback должен возвращаться, если:

- context пустой;
- все найденные chunks ниже `MinScore`;
- найденный context не содержит ответа на вопрос;
- answer generator не может сформировать grounded answer.

Prompt answer generator должен явно требовать:

```text
Отвечай только на основе предоставленного контекста.
Не добавляй факты вне контекста.
Если в контексте нет ответа, верни fallback message.
```

LLM не должна генерировать уверенный ответ по общим знаниям, если в retrieved context нет ответа.

## 14. Embedding model / provider / vector size

Если меняется:

- embedding provider;
- embedding model;
- vector size;
- semantic embedding space;

то старый индекс считается несовместимым.

Правила:

- embeddings разных моделей нельзя смешивать в одной Qdrant collection;
- после смены embedding space нужен rebuild;
- после смены vector size может потребоваться удалить Qdrant collection или volume;
- `Qdrant.VectorSize` должен совпадать с размерностью provider output.

## 15. Правила расширения

### Новый transcription provider

Добавляется в Python helper.

Условия:

- тот же input JSON;
- тот же output JSON;
- тот же transcript JSON;
- те же exit codes;
- без chunking;
- без embeddings;
- без Qdrant.

### Новый embedding provider

Добавляется как Infrastructure implementation `IEmbeddingProvider`.

Условия:

- реализует `EmbedAsync`;
- реализует `EmbedBatchAsync`;
- документирует vector size;
- регистрируется через DI;
- требует rebuild при смене embedding space.

### Google Gemini (текущий embedding provider)

Google Gemini API используется как основной провайдер embeddings начиная с ADR-014.

Конфигурируется через `Embeddings.Provider = "gemini"`.

Модель: `gemini-embedding-001`. Векторный размер: 768 (совпадает с `Qdrant.VectorSize`).

Использует task types для оптимального RAG:
- `EmbedAsync` (вопрос пользователя) → `RETRIEVAL_QUERY`;
- `EmbedBatchAsync` (chunks при ingest) → `RETRIEVAL_DOCUMENT`.

API-ключ передаётся через переменную окружения `GOOGLE_API_TOKEN` в файле `.env`.

### Новый answer provider

Добавляется как Infrastructure implementation `IAnswerGenerator`.

Условия:

- получает question и context;
- не выполняет retrieval;
- не строит embeddings;
- не обращается к Qdrant;
- соблюдает fallback rules.

### DeepSeek (текущий answer provider)

DeepSeek API используется как основной провайдер генерации ответа начиная с ADR-013.

Конфигурируется через `Answers.Provider = "deepseek"`.

API совместим с форматом OpenAI. Переиспользует `OpenAiAnswerGenerator` с отдельным `HttpClient` (`deepseek-answers`) и `BaseAddress = https://api.deepseek.com`.

API-ключ передаётся через переменную окружения `DEEPSEEK_API_TOKEN` в файле `.env`.

### Downloader в будущем

Добавляется как Infrastructure implementation `IVideoSource`.

Условия:

- результат `ResolveAsync` — локальный файл;
- downstream ingest flow не меняется;
- downloader не смешивается с transcription;
- downloader не пишет в Qdrant.

### Razor UI changes

Допустимы, если:

- UI остаётся локальным dev/demo-интерфейсом;
- PageModel зависит от Application services;
- UI не вызывает Infrastructure directly;
- Minimal API `/ask` остаётся основным контрактом.

## 16. Запреты для LLM

LLM не должна:

- превращать Python helper в service/backend;
- добавлять REST/gRPC/queue между C# и Python;
- переносить chunking в Python;
- строить embeddings в Python;
- писать в Qdrant из Python;
- генерировать ответ в Python;
- делать Qdrant общей БД приложения;
- смешивать operational data и retrieval data;
- класть interfaces в Domain;
- класть concrete infrastructure implementations в Application;
- заставлять Application зависеть от Infrastructure concrete classes;
- вызывать Qdrant/Ollama/OpenAI напрямую из Razor PageModel или API endpoint;
- делать Razor UI основным продуктовым frontend;
- добавлять отдельный frontend-проект без отдельного архитектурного решения;
- добавлять downloader в основной flow без сохранения локального файла как результата `IVideoSource`;
- реализовывать deduplication/versioning без отдельного решения;
- добавлять auth, chat history, multi-tenant, production deployment внутрь MVP без отдельного решения;
- менять официальные interfaces и contracts без необходимости;
- нарушать JSON-контракт C# ↔ Python.

## 17. Критерий согласованности кода

Код согласован с архитектурой, если:

- C# остаётся orchestration center;
- Python остаётся transcription-only CLI helper;
- chunking выполняется в C#;
- Application зависит от abstractions;
- Infrastructure содержит integrations;
- Qdrant хранит только retrieval data;
- `/ask` возвращает grounded answer или fallback;
- rebuild остаётся ручной операцией;
- Razor UI остаётся dev/demo UI.
