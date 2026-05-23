# Architecture

Документ описывает устойчивую архитектуру проекта Video Lecture RAG Assistant: роли компонентов, основные сценарии, границы слоёв и правила расширения.

## 1. Архитектурная идея

Video Lecture RAG Assistant строится как локальная система для вопросов по видеолекциям.

Главный сценарий:

```text
local video -> transcription -> transcript.json -> chunks -> embeddings -> Qdrant -> question -> retrieved context -> answer
```

Основное приложение на C# / .NET управляет всеми ключевыми процессами:

- ingest лекции;
- чтение и обработка transcript;
- chunking;
- построение embeddings;
- запись и поиск в Qdrant;
- генерация ответа;
- rebuild индекса;
- HTTP API для вопросов.

Python используется как внешний CLI-helper только для транскрибации. Qdrant используется только как vector store. Клиентские приложения остаются тонкими клиентами и не содержат RAG-логику.

Главное правило:

```text
C# — orchestration center.
Python — transcription-only CLI helper.
Qdrant — vector store.
Clients — thin HTTP clients.
```

## 2. Общая схема

```text
                         +----------------------+
                         |      User / Admin    |
                         +----------+-----------+
                                    |
              +---------------------+---------------------+
              |                                           |
              v                                           v
        +-------------+                             +--------------+
        |   Clients   |                             | CLI commands |
        |-------------|                             |--------------|
        | Web UI      |                             | ingest       |
        | Telegram    |                             | rebuild      |
        | other UI    |                             +------+-------+
        +------+------+                                    |
               |                                           |
               | HTTP API                                  |
               v                                           v
        +----------------------------------------------------------+
        |                 Main C# application                      |
        |----------------------------------------------------------|
        | Entry points: HTTP API / Razor Pages / CLI               |
        | Application services: ingest / ask / rebuild             |
        | Domain model                                             |
        | Infrastructure adapters                                  |
        +---------------------+--------------------+---------------+
                              |                    |
                              |                    |
                              v                    v
                    +----------------+     +-------------------+
                    |     Qdrant     |     | Python CLI helper |
                    |  vector store  |     | transcription     |
                    +----------------+     +---------+---------+
                                                     |
                                                     v
                                             +---------------+
                                             | transcript    |
                                             | JSON files    |
                                             +---------------+

Provider layer:
- embedding providers behind IEmbeddingProvider;
- answer providers behind IAnswerGenerator.
```

## 3. Основные компоненты

### Main C# application

Основное C# приложение — центральная часть системы.

Оно отвечает за:

- CLI-команды;
- HTTP API;
- встроенный Razor UI, если он используется как локальная test/demo-панель;
- orchestration сценариев;
- запуск Python helper;
- чтение и валидацию `transcript.json`;
- chunking;
- построение embeddings;
- запись chunks в Qdrant;
- поиск релевантного context;
- вызов answer provider;
- формирование grounded answer или fallback;
- rebuild индекса;
- прикладную валидацию и error mapping.

C# приложение не должно отдавать управление RAG-сценариями Python helper, Qdrant, внешним клиентам или provider-specific SDK.

### Python helper

Python helper — внешний CLI-скрипт, запускаемый из C#.

Он отвечает только за транскрибацию:

- принимает input JSON;
- читает локальный видеофайл;
- запускает transcription provider;
- пишет `transcript.json`;
- пишет output JSON;
- возвращает exit code;
- пишет диагностическую информацию в stdout/stderr.

Python helper не должен:

- выполнять chunking;
- строить embeddings;
- писать в Qdrant;
- искать context;
- генерировать answer;
- поднимать HTTP API;
- становиться вторым backend.

### Qdrant

Qdrant используется только как vector store.

Он хранит retrieval-представление лекций:

- vectors;
- chunk text;
- lecture metadata;
- chunk metadata;
- approximate source timing.

Qdrant не используется как общая база приложения.

В Qdrant нельзя хранить:

- пользователей;
- историю чатов;
- статусы задач;
- конфигурацию;
- бизнес-состояние приложения;
- данные, не связанные с retrieval.

### Provider layer

Провайдеры embeddings и генерации ответа скрыты за application interfaces:

```text
IEmbeddingProvider
IAnswerGenerator
```

Конкретные реализации находятся в Infrastructure.

Текущая конфигурация может использовать cloud/API providers или локальные providers. Архитектура не должна зависеть от конкретного provider как от неизменяемой части системы.

Правила:

- Application layer не знает provider-specific SDK;
- UI/API/CLI не вызывают providers напрямую;
- provider меняется через configuration и DI;
- при смене embedding provider, model, vector size или semantic embedding space нужен rebuild индекса;
- embeddings от разных моделей нельзя смешивать в одной Qdrant collection.

### File system

Файловая система используется для локальных артефактов:

- исходные видеофайлы;
- job input/output JSON для Python helper;
- сохранённые transcript-файлы;
- registry/manifest для rebuild;
- локальные override-конфиги.

Rebuild должен опираться на сохранённые transcript-файлы и не обязан повторно запускать транскрибацию.

### External clients

Внешние клиенты находятся отдельно от RAG-core.

Допустимые клиенты:

- Telegram bot;
- отдельный Web UI;
- другие thin clients.

Клиенты обращаются к основному приложению через HTTP API и могут использовать общий shared contracts проект.

Клиенты не должны:

- выполнять retrieval;
- строить embeddings;
- генерировать answer;
- обращаться напрямую к Qdrant;
- запускать Python helper;
- читать transcript/registry files;
- дублировать application services.

Клиентский flow:

```text
Client -> HTTP API -> IAskService -> RAG pipeline
```

## 4. Основные сценарии

### Ingest flow

Назначение: подготовить локальную лекцию к поиску.

```text
Admin
 -> CLI ingest
 -> ILectureIngestService
 -> IVideoSource
 -> ITranscriptionRunner
 -> Python CLI helper
 -> transcript.json
 -> ITranscriptReader
 -> IChunker
 -> IEmbeddingProvider
 -> IVectorStore
 -> Qdrant
```

Ключевые правила:

- входом является локальный видеофайл или источник, который сначала приводится к локальному файлу;
- Python helper создаёт только transcript;
- C# валидирует transcript;
- chunking выполняется в C#;
- embeddings строятся в C# через provider abstraction;
- запись в Qdrant выполняется только из C#;
- результат ingest должен позволять задавать вопросы по лекции через `/ask`.

### Ask flow

Назначение: ответить на вопрос по уже проиндексированным лекциям.

```text
User / Client
 -> POST /ask
 -> IAskService
 -> IContextRetriever
 -> IEmbeddingProvider
 -> IVectorStore
 -> Qdrant
 -> IAnswerGenerator
 -> AskResponse
```

Ключевые правила:

- вопрос проходит через `IAskService`;
- embedding вопроса строится через `IEmbeddingProvider`;
- retrieval выполняется через `IVectorStore`;
- фильтрация по `TopK` и `MinScore` выполняется в application layer;
- answer generator получает только вопрос и найденный context;
- если контекста недостаточно, возвращается fallback;
- Python helper в ask flow не участвует.

### Rebuild flow

Назначение: пересобрать индекс из сохранённых transcript-файлов.

```text
Admin
 -> CLI rebuild
 -> ILectureIngestService
 -> read registry
 -> read saved transcript.json
 -> ITranscriptReader
 -> IChunker
 -> IEmbeddingProvider
 -> IVectorStore
 -> Qdrant
```

Ключевые правила:

- rebuild — явная административная операция;
- rebuild использует сохранённые transcript-файлы;
- rebuild не обязан повторно запускать Python helper;
- rebuild может очищать индекс перед повторной записью;
- при смене embedding space rebuild обязателен;
- при смене vector size может потребоваться удалить Qdrant collection или volume.

## 5. Граница C# ↔ Python

Связь между C# и Python строится через process boundary.

Используются:

- запуск внешнего процесса;
- input JSON;
- output JSON;
- transcript JSON;
- exit code;
- stdout/stderr для диагностики.

Разрешено:

- запускать Python через process runner;
- передавать helper’у входной JSON;
- читать созданный `transcript.json`;
- обрабатывать exit code;
- сохранять диагностические details.

Запрещено:

- внутренний REST между C# и Python;
- gRPC между C# и Python;
- очередь между C# и Python;
- Python HTTP service;
- запись в Qdrant из Python;
- chunking в Python;
- embeddings в Python;
- answer generation в Python.

## 6. Слои и зависимости

В основном C# приложении используются логические слои:

```text
Domain
Application
Infrastructure
Entry points
Composition root
```

### Domain

Содержит предметные сущности и инварианты.

Примеры:

- Lecture;
- Transcript;
- TranscriptSegment;
- LectureChunk;
- RetrievedContext;
- SourceCitation;
- AnswerResult.

Domain не зависит от Application, Infrastructure, UI, API, SDK, Qdrant, provider APIs или файловой системы.

### Application

Содержит сценарные абстракции, contracts и orchestration services.

Зоны:

```text
Application/Abstractions
Application/Contracts
Application/Services
```

Application отвечает за:

- последовательность use case;
- прикладную валидацию;
- работу через interfaces;
- contracts между слоями;
- fallback rules;
- фильтрацию retrieved context.

Application не должен зависеть от concrete infrastructure implementations.

### Infrastructure

Содержит concrete implementations interfaces из Application.

Примеры:

- video source adapters;
- transcription runner;
- transcript reader;
- embedding providers;
- vector store;
- answer generators;
- configuration options;
- technical initialization.

Infrastructure может зависеть от Application и Domain.

Infrastructure не должна содержать UI-логику и не должна перехватывать orchestration всего пользовательского сценария, если это задача Application.

### Entry points

Entry points:

- CLI;
- HTTP API;
- встроенный Razor UI;
- внешние клиенты.

Entry points принимают ввод, вызывают application services или HTTP API и возвращают результат.

Они не должны напрямую обращаться к:

- Qdrant;
- provider APIs;
- Python helper;
- transcript files;
- registry files;
- infrastructure implementations.

### Composition root

Composition root связывает interfaces и implementations через DI.

Это место, где допустимо знать concrete infrastructure classes.

## 7. Разрешённые и запрещённые зависимости

### Разрешённые зависимости

```text
Application -> Domain
Infrastructure -> Application
Infrastructure -> Domain
API / CLI / Razor UI -> Application
External clients -> HTTP API
External clients -> shared contracts
Composition root -> Infrastructure
```

### Запрещённые зависимости

```text
Domain -> Application
Domain -> Infrastructure
Application -> Infrastructure concrete classes
Entry points -> Qdrant directly
Entry points -> provider APIs directly
Entry points -> Python helper directly
Python helper -> Qdrant
Python helper -> retrieval / embeddings / answer generation
External clients -> RAG internals
```

## 8. Контракты

### AskRequest

Внешний HTTP API для вопросов принимает request следующего смысла:

```json
{
  "question": "О чём эта лекция?",
  "topK": 5,
  "minScore": 0.3
}
```

Правила:

- `question` обязателен;
- `question.Trim()` не должен быть пустым;
- максимальная длина вопроса — 1000 символов;
- `topK` в диапазоне `1..10`;
- `minScore` в диапазоне `0..1`.

### AskResponse

При успешном grounded answer:

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

При недостаточном контексте:

```json
{
  "answer": null,
  "usedContext": false,
  "sources": [],
  "message": "Недостаточно релевантного контекста для уверенного ответа по загруженным лекциям."
}
```

### Transcript JSON

Transcript создаётся Python helper и читается C# приложением.

Основная форма:

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
- segment indexes ordered without gaps;
- `startSec >= 0`;
- `endSec > startSec`;
- `text.Trim()` is not empty.

### Qdrant payload

Qdrant payload хранит retrieval data:

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

- `chunkId`;
- `lectureId`;
- `lectureTitle`;
- `chunkIndex`;
- `text`;
- `approxMinute`.

Optional:

- `approxStartSec`;
- `approxEndSec`.

Qdrant point id должен быть совместим с требованиями Qdrant, например UUID или integer. Human-readable `chunkId` хранится в payload.

## 9. Grounded answer rules

Answer generation должна быть grounded in retrieved context.

Fallback возвращается, если:

- context пустой;
- все найденные chunks ниже `MinScore`;
- найденный context не содержит ответа;
- answer generator не может сформировать grounded answer.

Prompt answer generator должен явно требовать:

```text
Отвечай только на основе предоставленного контекста.
Не добавляй факты вне контекста.
Если в контексте нет ответа, верни fallback.
```

LLM не должна генерировать уверенный ответ по общим знаниям, если retrieved context не содержит ответа.

## 10. Embedding compatibility

Embedding index несовместим после изменения:

- embedding provider;
- embedding model;
- vector size;
- semantic embedding space.

Правила:

- embeddings разных моделей нельзя смешивать в одной Qdrant collection;
- совпадение vector size не означает совместимость embedding space;
- после смены embedding space нужен rebuild;
- после смены vector size может потребоваться удалить Qdrant collection или volume;
- `Qdrant.VectorSize` должен совпадать с размерностью provider output.

## 11. Правила расширения

### Новый transcription provider

Добавляется за границей Python helper.

Условия:

- сохраняется тот же input/output JSON contract;
- сохраняется transcript contract;
- сохраняются совместимые exit codes;
- Python не выполняет chunking;
- Python не строит embeddings;
- Python не пишет в Qdrant;
- Python не генерирует answer.

### Новый embedding provider

Добавляется как infrastructure implementation `IEmbeddingProvider`.

Что нужно сделать:

1. Реализовать `EmbedAsync`.
2. Реализовать `EmbedBatchAsync`.
3. Документировать vector size.
4. Добавить provider-specific configuration.
5. Зарегистрировать provider через DI.
6. Обновить документацию по rebuild при смене embedding space.

### Новый answer provider

Добавляется как infrastructure implementation `IAnswerGenerator`.

Условия:

- получает question и context;
- не выполняет retrieval;
- не строит embeddings;
- не обращается к Qdrant;
- соблюдает grounded answer rules;
- возвращает answer или fallback.

### Новый client

Добавляется как thin client.

Условия:

- использует HTTP API;
- использует shared contracts, если они есть;
- не содержит RAG internals;
- не дублирует application services;
- не обращается напрямую к Infrastructure.

### Downloader

Downloader может быть добавлен как implementation `IVideoSource`.

Условия:

- результатом `ResolveAsync` является локальный файл;
- downstream ingest flow не меняется;
- downloader не смешивается с transcription;
- downloader не пишет в Qdrant;
- downloader не меняет transcript contract.

## 12. Архитектурные нарушения

Нарушением считается:

- перенос orchestration из C# в Python;
- превращение Python helper в HTTP service;
- добавление internal REST/gRPC/queue между C# и Python;
- выполнение chunking в Python;
- построение embeddings в Python;
- запись в Qdrant из Python;
- answer generation в Python;
- прямой вызов Qdrant/provider APIs из UI/API/CLI;
- размещение concrete infrastructure implementations в Application;
- размещение application interfaces в Domain;
- использование Qdrant как общей базы приложения;
- автоматический rebuild без явного архитектурного решения;
- смешивание embeddings разных моделей в одной collection;
- дублирование RAG-логики во внешних клиентах;
- чтение transcript/registry files напрямую из клиентов;
- нарушение JSON-контракта C# ↔ Python.

## 13. Критерий согласованности

Код согласован с архитектурой, если:

- C# остаётся orchestration center;
- Python остаётся transcription-only CLI helper;
- chunking выполняется в C#;
- Application зависит от abstractions;
- Infrastructure содержит integrations;
- Qdrant хранит только retrieval data;
- clients остаются thin clients;
- `/ask` возвращает grounded answer или fallback;
- rebuild остаётся явной операцией;
- смена embedding space сопровождается пересборкой индекса.
