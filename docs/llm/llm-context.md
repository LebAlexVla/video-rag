# LLM Context

Канонический технический контекст для AI/LLM-агентов, которые генерируют, проверяют или рефакторят код проекта.

Документ фиксирует устойчивые архитектурные правила проекта. Он не должен описывать временное состояние ветки, конкретный PR или разовые детали реализации.

## 1. Назначение проекта

Video Lecture RAG Assistant — локальная система для вопросов по видеолекциям.

Основной сценарий:

```text
local video -> transcription -> transcript.json -> chunks -> embeddings -> Qdrant -> question -> retrieved context -> answer
```

Пользователь задаёт вопрос по загруженным лекциям. Система ищет релевантные фрагменты в транскриптах и формирует ответ на основе найденного контекста.

## 2. Базовые архитектурные правила

1. C# / .NET — центр системы и основная точка orchestration.
2. Python используется только как CLI-helper для транскрибации.
3. Python helper не выполняет chunking, embeddings, retrieval, answer generation и запись в Qdrant.
4. Связь C# и Python строится через запуск процесса, JSON-файлы, exit code, stdout/stderr.
5. Внутренний REST, gRPC или очередь между C# и Python не используются.
6. Chunking выполняется в C#.
7. Qdrant используется только как vector store.
8. Провайдеры embeddings и answer generation вызываются только через infrastructure implementations.
9. Основной контракт для вопросов — HTTP API `POST /ask`.
10. CLI остаётся основным способом административных операций: ingest и rebuild.
11. Rebuild выполняется явно.
12. При смене embedding provider, embedding model, vector size или embedding space индекс нужно пересобрать.

## 3. Роли компонентов

### C# application

C# приложение управляет основными сценариями:

- CLI commands;
- HTTP API;
- локальный Razor UI, если он используется как test/demo-интерфейс;
- запуск Python helper;
- чтение transcript JSON;
- chunking;
- построение embeddings;
- запись и поиск в Qdrant;
- генерация ответа через provider abstraction;
- rebuild индекса;
- validation и error mapping.

C# не должен отдавать orchestration Python helper, клиентским приложениям или внешним сервисам.

### Python helper

Python helper отвечает только за транскрибацию.

Он может:

- читать локальный видеофайл;
- запускать transcription provider;
- писать `transcript.json`;
- писать output JSON;
- возвращать exit code;
- писать диагностические данные в stdout/stderr.

Он не должен содержать RAG-логику.

### Qdrant

Qdrant хранит retrieval-представление данных:

- vectors;
- chunk text;
- lecture metadata;
- chunk metadata;
- approximate source timing.

Qdrant не используется как общая база приложения для пользователей, истории чатов, задач, конфигурации или бизнес-состояния.

### Provider layer

Embeddings и answer generation подключаются через interfaces:

- `IEmbeddingProvider`;
- `IAnswerGenerator`.

Конкретные провайдеры являются infrastructure details. Их можно менять через конфигурацию и DI, не меняя application layer.

При смене embedding space старый индекс считается несовместимым, даже если размерность вектора совпадает.

### External clients

Клиентские приложения могут находиться в `clients/`.

Примеры допустимых клиентов:

- Telegram bot;
- отдельный Web UI;
- другие thin clients.

Клиенты обращаются к основному приложению через HTTP API и используют shared contracts, если они есть.

Клиенты не должны:

- выполнять retrieval;
- строить embeddings;
- вызывать answer provider;
- обращаться к Qdrant;
- запускать Python helper;
- читать transcript/registry файлы напрямую;
- дублировать application logic.

## 4. Основные сценарии

### Ingest

```text
Admin
 -> CLI ingest
 -> ILectureIngestService
 -> IVideoSource
 -> ITranscriptionRunner
 -> Python helper
 -> transcript.json
 -> ITranscriptReader
 -> IChunker
 -> IEmbeddingProvider
 -> IVectorStore
 -> Qdrant
```

Ключевые правила:

- входом является локальный видеофайл или источник, который сначала приводится к локальному файлу;
- Python создаёт transcript only;
- C# валидирует transcript;
- C# выполняет chunking, embeddings и запись в Qdrant.

### Ask

```text
User / Client
 -> POST /ask
 -> IAskService
 -> IContextRetriever
 -> IEmbeddingProvider
 -> IVectorStore
 -> IAnswerGenerator
 -> AskResponse
```

Ключевые правила:

- вопрос проходит через application service;
- embedding вопроса строится через `IEmbeddingProvider`;
- retrieval выполняется через `IVectorStore`;
- answer generator получает только вопрос и найденный context;
- если контекста недостаточно, возвращается fallback;
- Python в ask flow не участвует.

### Rebuild

```text
Admin
 -> CLI rebuild
 -> ILectureIngestService
 -> read registry
 -> read saved transcript.json
 -> IChunker
 -> IEmbeddingProvider
 -> IVectorStore
 -> Qdrant
```

Ключевые правила:

- rebuild — явная административная операция;
- rebuild использует сохранённые transcript-файлы;
- rebuild не обязан повторно запускать transcription;
- при смене embedding space rebuild обязателен.

## 5. Слои и зависимости

### Domain

Содержит предметные сущности и инварианты.

Не зависит от Application, Infrastructure, UI, API, SDK, Qdrant, provider APIs или файловой системы.

### Application

Содержит:

- interfaces в `Application/Abstractions`;
- contracts в `Application/Contracts`;
- orchestration services в `Application/Services`.

Application зависит от Domain и abstractions, но не от concrete Infrastructure.

### Infrastructure

Содержит concrete implementations:

- video source adapters;
- transcription runner;
- transcript reader;
- embedding providers;
- vector store;
- answer generators;
- configuration;
- technical initialization.

Infrastructure может зависеть от Application и Domain.

### Entry points

Entry points:

- CLI;
- HTTP API;
- Razor Pages;
- external clients.

Entry points принимают ввод, вызывают application services или HTTP API и возвращают результат. Они не должны содержать RAG-логику.

### Разрешённые зависимости

```text
Application -> Domain
Infrastructure -> Application
Infrastructure -> Domain
API / CLI / Razor UI -> Application
External clients -> HTTP API / shared contracts
Composition root -> Infrastructure
```

### Запрещённые зависимости

```text
Domain -> Application
Domain -> Infrastructure
Application -> concrete Infrastructure classes
Entry points -> Qdrant directly
Entry points -> provider APIs directly
Entry points -> Python helper directly
Python helper -> Qdrant
Python helper -> retrieval / embeddings / answer generation
External clients -> RAG internals
```

## 6. Application interfaces

Основные interfaces:

- `IVideoSource` — нормализует и проверяет источник видео.
- `ITranscriptionRunner` — запускает Python helper.
- `ITranscriptReader` — читает и валидирует transcript JSON.
- `IChunker` — превращает transcript в chunks.
- `IEmbeddingProvider` — строит embeddings.
- `IVectorStore` — пишет chunks и ищет context.
- `IContextRetriever` — строит embedding вопроса, ищет context и применяет фильтрацию.
- `IAnswerGenerator` — генерирует ответ по вопросу и найденному context.
- `ILectureIngestService` — orchestrates ingest и rebuild.
- `IAskService` — orchestrates ask-сценарий.

Новые interfaces добавляются только при появлении реальной границы ответственности.

## 7. JSON-контракт C# ↔ Python

Общие правила:

- UTF-8 JSON;
- camelCase field naming;
- C# пишет input JSON;
- Python пишет output JSON;
- Python пишет transcript JSON;
- C# продолжает ingest только после успешного exit code, валидного output JSON и валидного transcript JSON.

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

Transcript invariants:

- `status == "success"`;
- `segments.length >= 1`;
- segment indexes are ordered without gaps;
- `startSec >= 0`;
- `endSec > startSec`;
- `text.Trim()` is not empty.

## 8. Qdrant payload

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

Required fields:

- `chunkId`;
- `lectureId`;
- `lectureTitle`;
- `chunkIndex`;
- `text`;
- `approxMinute`.

Optional fields:

- `approxStartSec`;
- `approxEndSec`.

Qdrant point id should be compatible with Qdrant requirements, for example UUID or integer. Human-readable `chunkId` should be stored in payload.

## 9. Ask API shape

### AskRequest

```json
{
  "question": "О чём эта лекция?",
  "topK": 5,
  "minScore": 0.3
}
```

Rules:

- `question` is required;
- `question.Trim()` is not empty;
- max question length is 1000 chars;
- `topK` is in range `1..10`;
- `minScore` is in range `0..1`.

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

## 10. Grounded answer rules

Answer generation must be grounded in retrieved context.

Fallback должен возвращаться, если:

- context пустой;
- все chunks ниже `MinScore`;
- найденный context не содержит ответа;
- answer generator не может сформировать grounded answer.

Prompt должен требовать:

```text
Отвечай только на основе предоставленного контекста.
Не добавляй факты вне контекста.
Если в контексте нет ответа, верни fallback.
```

## 11. Embedding compatibility

Индекс несовместим после изменения:

- embedding provider;
- embedding model;
- vector size;
- semantic embedding space.

Rules:

- embeddings разных моделей нельзя смешивать в одной Qdrant collection;
- после смены embedding space нужен rebuild;
- после смены vector size может потребоваться удалить Qdrant collection или volume;
- `Qdrant.VectorSize` должен совпадать с размерностью provider output.

## 12. Правила расширения

### Новый transcription provider

Добавляется за границей Python helper и сохраняет тот же JSON-контракт.

Условия:

- без chunking;
- без embeddings;
- без Qdrant;
- без answer generation;
- тот же transcript contract;
- совместимые exit codes.

### Новый embedding provider

Добавляется как infrastructure implementation `IEmbeddingProvider`.

Условия:

- реализует `EmbedAsync`;
- реализует `EmbedBatchAsync`;
- документирует vector size;
- регистрируется через DI;
- требует rebuild при смене embedding space.

### Новый answer provider

Добавляется как infrastructure implementation `IAnswerGenerator`.

Условия:

- получает question и context;
- не выполняет retrieval;
- не строит embeddings;
- не обращается к Qdrant;
- соблюдает grounded answer rules.

### Новый client

Добавляется как thin client.

Условия:

- использует HTTP API;
- использует shared contracts, если они есть;
- не содержит RAG internals;
- не дублирует application services.

### Downloader

Downloader может быть добавлен как implementation `IVideoSource`.

Условия:

- результатом является локальный файл;
- downstream ingest flow не меняется;
- downloader не смешивается с transcription;
- downloader не пишет в Qdrant.

## 13. Запреты

Запрещено:

- превращать Python helper в service/backend;
- добавлять REST/gRPC/queue между C# и Python;
- переносить chunking в Python;
- строить embeddings в Python;
- писать в Qdrant из Python;
- генерировать ответ в Python;
- использовать Qdrant как общую БД приложения;
- класть interfaces в Domain;
- класть concrete infrastructure implementations в Application;
- заставлять Application зависеть от Infrastructure concrete classes;
- вызывать Qdrant или provider APIs напрямую из UI/API/CLI;
- смешивать embeddings от разных моделей в одной collection;
- менять embedding provider без rebuild;
- дублировать RAG-логику во внешних клиентах;
- нарушать JSON-контракт C# ↔ Python.

## 14. Критерий согласованности кода

Код согласован с архитектурой, если:

- C# остаётся orchestration center;
- Python остаётся transcription-only CLI helper;
- chunking выполняется в C#;
- Application зависит от abstractions;
- Infrastructure содержит integrations;
- Qdrant хранит только retrieval data;
- clients являются thin clients;
- `/ask` возвращает grounded answer или fallback;
- rebuild остаётся явной операцией.

<!-- URL_AUDIO_INGEST_LLM_CONTEXT_START -->
### URL audio ingest

Система может принимать публичную Rutube/VK ссылку как источник лекции.

Правильный flow:

```text
URL -> audio downloader -> local audio file -> existing ingest pipeline
```

Ограничения:

- скачивается только аудио;
- downloader не заменяет Python helper;
- Python helper по-прежнему только транскрибирует локальный файл;
- Web UI и Telegram bot не содержат downloader/RAG logic;
- Web UI и Telegram bot вызывают основной HTTP API;
- HTTP URL ingest должен быть job-based, потому что скачивание и транскрибация могут быть долгими;
- job storage в MVP может быть in-memory.
<!-- URL_AUDIO_INGEST_LLM_CONTEXT_END -->
