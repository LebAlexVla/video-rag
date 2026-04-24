# Architecture

## Архитектурная идея

Video Lecture RAG Assistant строится как локальное C# приложение, которое управляет всеми основными сценариями: ingest, ask и rebuild.

Главное правило архитектуры:

```text
C# — центр системы.
Python — только CLI-helper для транскрибации.
Qdrant — только vector store.
```

Система намеренно не делится на микросервисы. Для MVP важнее простой локальный запуск, понятные границы и минимальное число moving parts.

## Общая схема

```text
                     +----------------------+
                     |      User / Admin    |
                     +----------+-----------+
                                |
              +-----------------+-----------------+
              |                                   |
              v                                   v
        +-----------+                       +-------------+
        | Razor UI  |                       | CLI / API   |
        | test/demo |                       | ingest/ask  |
        +-----+-----+                       +------+------+
              |                                    |
              +-----------------+------------------+
                                |
                                v
                    +-----------------------+
                    |     C# application    |
                    |-----------------------|
                    | orchestration         |
                    | chunking              |
                    | embeddings            |
                    | retrieval             |
                    | answer generation     |
                    +----+--------------+---+
                         |              |
                         |              v
                         |       +-------------+
                         |       |   Qdrant    |
                         |       | vector store|
                         |       +-------------+
                         |
                         v
                +-------------------+
                | Python CLI helper |
                | transcription     |
                +---------+---------+
                          |
                          v
                  +---------------+
                  | local files   |
                  +---------------+

External local provider:
- Ollama for embeddings and answer generation
```

## Роли компонентов

### C# application

C# приложение отвечает за:

- CLI-команды;
- Minimal API;
- локальный Razor UI;
- orchestration сценариев;
- запуск Python helper;
- чтение `transcript.json`;
- chunking;
- построение embeddings;
- запись и поиск в Qdrant;
- вызов модели ответа;
- rebuild индекса;
- прикладную валидацию и обработку ошибок.

C# не должен отдавать orchestration другому процессу или языку.

### Python helper

Python helper — внешний CLI-скрипт, который запускается из C#.

Он отвечает только за:

- чтение локального видеофайла;
- запуск транскрибации;
- создание `transcript.json`;
- возврат exit code и диагностической информации.

Python helper не должен:

- выполнять chunking;
- строить embeddings;
- писать в Qdrant;
- искать контекст;
- генерировать ответ пользователю;
- поднимать HTTP API;
- быть вторым backend.

### Qdrant

Qdrant используется только как vector store.

Он хранит:

- vectors chunks;
- текст chunks;
- metadata для retrieval:
  - `chunkId`;
  - `lectureId`;
  - `lectureTitle`;
  - `chunkIndex`;
  - `approxMinute`;
  - примерные временные границы.

Qdrant не используется для:

- пользователей;
- истории чатов;
- статусов задач;
- конфигурации;
- общей операционной базы приложения.

### Ollama

Ollama используется как локальный provider для:

- embeddings;
- answer generation.

C# обращается к Ollama через infrastructure adapters, реализующие application interfaces.

Ollama не должен вызываться напрямую из UI, API endpoint или Application services в обход abstractions.

### Файловая система

Файловая система используется для:

- исходных видеофайлов;
- job input/output файлов для Python helper;
- сохранённых `transcript.json`;
- registry/manifest для rebuild;
- локальной конфигурации.

Rebuild должен опираться на сохранённые transcript-файлы, а не требовать повторной транскрибации.

### Razor UI

Razor UI — локальный test/demo-интерфейс.

Он нужен для удобной ручной проверки ask-сценария.

Правила:

- Razor UI не является основным MVP-контрактом.
- Основной контракт остаётся Minimal API `/ask`.
- Razor PageModel может зависеть от `IAskService`.
- Razor UI не должен напрямую работать с Qdrant, Ollama, OpenAI, Python helper или файловой системой.
- Razor UI не должен содержать retrieval logic или answer generation logic.

## Ingest flow

Назначение: подготовить локальную лекцию к поиску.

```text
Admin
 -> CLI ingest
 -> C# application
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

- входом является локальный видеофайл;
- Python только создаёт transcript;
- C# валидирует transcript;
- chunking выполняется в C#;
- embeddings строятся в C# через provider abstraction;
- запись в Qdrant выполняется только из C#.

## Ask flow

Назначение: ответить на вопрос по уже проиндексированным лекциям.

```text
User
 -> Minimal API /ask or Razor UI
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
- embedding вопроса строится в C#;
- retrieval выполняется через `IVectorStore`;
- фильтрация по релевантности выполняется в application layer;
- answer generator получает только найденный контекст;
- если контекста недостаточно, система возвращает fallback;
- Python в ask flow не участвует.

## Rebuild flow

Назначение: вручную пересобрать индекс.

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

- rebuild — явная ручная операция;
- rebuild не должен автоматически запускаться при изменении конфигурации;
- rebuild использует сохранённые transcript-файлы;
- rebuild не обязан повторно запускать Python helper;
- при смене embedding space старый индекс считается несовместимым.

Если изменилась размерность embeddings, может потребоваться удалить старую Qdrant collection или volume перед повторной индексацией.

## Граница C# ↔ Python

Связь между C# и Python строится через:

- запуск внешнего процесса;
- входной JSON;
- выходной JSON;
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
- очереди между C# и Python;
- Python HTTP service;
- запись в Qdrant из Python;
- chunking в Python;
- генерация ответа в Python.

## Слои и зависимости

В C# приложении используются следующие логические зоны:

```text
Domain
Application
Infrastructure
Pages / API / CLI composition
```

### Domain

Содержит предметные сущности и их инварианты.

Не зависит от Application и Infrastructure.

### Application

Содержит:

- interfaces в `Application/Abstractions`;
- сценарные contracts в `Application/Contracts`;
- orchestration services в `Application/Services`.

Application знает только о Domain и abstractions.

### Infrastructure

Содержит реализации interfaces:

- video source adapters;
- transcription runner;
- transcript reader;
- embedding providers;
- vector store;
- answer generators;
- technical initialization.

Infrastructure может зависеть от Application и Domain.

### Pages / API / CLI

Это входной слой приложения.

Он должен:

- принимать пользовательский ввод;
- вызывать application services;
- возвращать результат.

Он не должен напрямую работать с Qdrant, Ollama, Python helper или SDK моделей.

## Разрешённые зависимости

```text
Application -> Domain
Infrastructure -> Application
Infrastructure -> Domain
Pages/API/CLI -> Application
Composition root -> Infrastructure
```

## Запрещённые зависимости

```text
Domain -> Application
Domain -> Infrastructure
Application -> Infrastructure concrete classes
Pages/API/CLI -> Qdrant directly
Pages/API/CLI -> Ollama/OpenAI directly
Pages/API/CLI -> Python helper directly
Python helper -> Qdrant
Python helper -> retrieval/answer generation
```

## Архитектурные нарушения

Нарушением считается:

- перенос orchestration из C# в Python;
- превращение Python helper в HTTP-сервис;
- добавление внутреннего REST/gRPC между C# и Python;
- выполнение chunking в Python;
- запись embeddings в Qdrant из Python;
- прямой вызов Qdrant/Ollama/OpenAI из Razor UI или API endpoint;
- размещение concrete infrastructure implementations в Application;
- размещение application interfaces в Domain;
- использование Qdrant как общей базы приложения;
- автоматический rebuild без явного решения;
- смешивание embeddings разных моделей в одной collection;
- превращение Razor UI в основной продуктовый frontend вместо dev/demo-интерфейса.
