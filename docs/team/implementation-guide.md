# Implementation Guide

## 1. Назначение документа

Этот документ нужен для старта разработки. Он фиксирует:
- структуру проекта;
- размещение интерфейсов и реализаций;
- состав Domain и Application;
- рекомендуемый порядок реализации;
- правила расширения;
- признаки архитектурных нарушений.

Документ не пересказывает обзор проекта и не дублирует архитектурный документ. Его задача — помочь команде быстро начать писать код, не споря о слоях, контрактах и папках.

---

## 2. Структура проекта

```text
/src
  /Domain
    /Entities
      Lecture.cs
      Transcript.cs
      TranscriptSegment.cs
      LectureChunk.cs
      RetrievedContext.cs
      SourceCitation.cs
      AnswerResult.cs

  /Application
    /Abstractions
      IVideoSource.cs
      ITranscriptionRunner.cs
      ITranscriptReader.cs
      IChunker.cs
      IEmbeddingProvider.cs
      IVectorStore.cs
      IContextRetriever.cs
      IAnswerGenerator.cs
      ILectureIngestService.cs
      IAskService.cs

    /Contracts
      AskRequest.cs
      AskResponse.cs
      LectureIngestRequest.cs
      LectureIngestResult.cs
      LectureRebuildRequest.cs
      LectureRebuildResult.cs
      ContextRetrievalResult.cs
      VideoSourceDescriptor.cs
      TranscriptionRunRequest.cs
      TranscriptionRunResult.cs
      EmbeddedLectureChunk.cs
      ErrorInfo.cs

    /Services
      AskService.cs
      LectureIngestService.cs
      ContextRetriever.cs
      AnswerGenerator.cs
      Chunker.cs

  /Infrastructure
    /VideoSources
      LocalFileVideoSource.cs

    /Transcription
      PythonTranscriptionRunner.cs

    /Transcript
      JsonTranscriptReader.cs

    /Embeddings
      OllamaEmbeddingProvider.cs
      OpenAiEmbeddingProvider.cs

    /VectorStore
      QdrantVectorStore.cs

    /Answers
      OllamaAnswerGenerator.cs
      OpenAiAnswerGenerator.cs
```

---

## 3. Как распределять код по слоям

### Domain

В `Domain` лежат только предметные сущности и их инварианты:
- `Lecture`
- `Transcript`
- `TranscriptSegment`
- `LectureChunk`
- `RetrievedContext`
- `SourceCitation`
- `AnswerResult`

Сюда не кладутся:
- DTO сценариев;
- JSON-модели helper;
- модели payload для Qdrant;
- интерфейсы внешних зависимостей.

Практический критерий простой: если модель описывает предметную сущность и не зависит от транспорта, SDK и конкретного сценария, это `Domain`.

### Application

`Application` содержит прикладные контракты, интерфейсы и orchestration-сервисы.

#### `Application/Abstractions`
Здесь лежат все интерфейсы:
- `IVideoSource`
- `ITranscriptionRunner`
- `ITranscriptReader`
- `IChunker`
- `IEmbeddingProvider`
- `IVectorStore`
- `IContextRetriever`
- `IAnswerGenerator`
- `ILectureIngestService`
- `IAskService`

Если появляется новый интерфейс для интеграции или для прикладного сценария, он почти всегда должен лежать именно здесь.

#### `Application/Contracts`
Здесь лежат DTO и прикладные контракты:
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

Это не Domain, потому что эти модели описывают входы и выходы сценариев, промежуточные данные и результаты интеграций.

#### `Application/Services`
Здесь лежат orchestration-сервисы:
- `AskService`
- `LectureIngestService`
- `ContextRetriever`
- `AnswerGenerator`
- `Chunker`

Даже если конкретная реализация простая, это всё равно `Application`, потому что здесь живёт прикладная логика, а не техническая интеграция.

### Infrastructure

В `Infrastructure` лежат concrete-реализации и технические адаптеры:
- `LocalFileVideoSource`
- `PythonTranscriptionRunner`
- `JsonTranscriptReader`
- `OllamaEmbeddingProvider`
- `OpenAiEmbeddingProvider`
- `QdrantVectorStore`
- `OllamaAnswerGenerator`
- `OpenAiAnswerGenerator`

`Infrastructure` не должна становиться местом, где живёт orchestration use case или правила прикладных сценариев.

---

## 4. Базовые правила реализации

- C# — центр системы.
- Python helper только транскрибирует и пишет `transcript.json`.
- Chunking выполняется в C#.
- Интерфейсы лежат в `Application/Abstractions`.
- `Infrastructure` содержит только реализации.
- Основной интерфейс MVP: `CLI ingest + Minimal API ask`.
- Telegram bot — optional.
- Web UI — вне MVP.
- `ILLMProvider` не используется как основной контракт MVP.

Если новая реализация не укладывается в эти правила, это уже архитектурное изменение, а не обычная деталь реализации.

---

## 5. Что писать первым

Рекомендуемый порядок такой:

### Шаг 1. Каркас проекта
Сначала определить:
- Domain entities;
- Application contracts;
- Application abstractions.

Это даёт стабильную основу и позволяет нескольким людям работать параллельно без расхождения по контрактам.

### Шаг 2. Вертикаль ingest
Реализовать:
- `LocalFileVideoSource`
- `PythonTranscriptionRunner`
- `JsonTranscriptReader`
- `Chunker`
- основной `IEmbeddingProvider`
- `QdrantVectorStore`
- `LectureIngestService`

Цель этого этапа — довести до конца ingest одной лекции и убедиться, что данные реально попадают в Qdrant.

### Шаг 3. Вертикаль ask
Реализовать:
- `ContextRetriever`
- `AnswerGenerator`
- `AskService`
- Minimal API endpoint

Цель — получить рабочий ответ по уже проиндексированной лекции.

### Шаг 4. Rebuild
Добавить:
- `ClearAsync` в vector store;
- `RebuildAsync` в ingest service.

### Шаг 5. Optional
Только после этого:
- Telegram bot;
- резервный provider ответа;
- резервный provider embeddings.

Практическое правило: пока не работает полный путь `ingest -> Qdrant -> ask`, не стоит тратить время на optional-функции.

Отдельное правило MVP:
- сначала команда выбирает один основной рабочий стек провайдеров и доводит до конца именно его;
- не нужно одновременно реализовывать несколько равноправных комбинаций embeddings/answer providers;
- запасные провайдеры допустимы только после того, как стабильно работает основной путь `ingest -> transcript -> chunking -> embeddings -> Qdrant -> ask -> answer`.

Для первой реализации приоритет должен быть у локального рабочего стека.

---

## 6. Правила расширения

### Новый provider транскрибации

Добавляется в Python helper.

Обязательные условия:
- тот же входной JSON;
- тот же выходной JSON;
- те же инварианты;
- отсутствие влияния на orchestration C#.

C# не должен знать детали конкретного провайдера транскрибации.

### Новый provider векторов

Добавляется как новая реализация `IEmbeddingProvider`.

Обязательные условия:
- реализует `EmbedAsync` и `EmbedBatchAsync`;
- фиксирует имя провайдера и модели;
- документирует размерность embeddings.

Если меняется embedding space, требуется rebuild.

### Новый provider ответа

Добавляется как новая реализация `IAnswerGenerator`.

Обязательные условия:
- принимает вопрос и найденный контекст;
- возвращает `AnswerResult`;
- не меняет `IAskService`.

Генератор ответа не должен выполнять retrieval и не должен работать с Qdrant напрямую.

### Downloader в будущем

Добавляется как новая реализация `IVideoSource`.

После `ResolveAsync` система по-прежнему должна получить локальный путь к файлу. Весь pipeline после этого момента должен остаться прежним.

---

## 7. Что считается нарушением архитектуры

### Нарушения границы C# ↔ Python
- внутренний REST между C# и Python;
- Python как сервис;
- запись в Qdrant из Python;
- chunking в Python;
- ответ пользователю из Python.

### Нарушения слоёв
- интерфейсы в `Domain`;
- concrete-реализации в `Application`;
- прямой вызов Qdrant SDK из API-контроллеров;
- прямой вызов OpenAI/Ollama из API-контроллеров;
- orchestration use case в `Infrastructure`.

### Нарушения MVP
- использовать `ILLMProvider` как главный контракт;
- делать Telegram обязательной частью MVP;
- добавлять обязательный web UI;
- внедрять downloader в основной объём первой версии;
- строить дедупликацию и versioning;
- проектировать MVP вокруг word-level timestamps.

---

## 8. Практический ориентир для команды

Команда движется правильно, если:
- сначала появляется стабильный каркас моделей и интерфейсов;
- затем начинает работать ingest одной лекции;
- после этого появляется рабочий ask;
- rebuild добавляется как отдельный управляемый шаг;
- optional-возможности не мешают довести основной путь до рабочего состояния.

Если появляется желание одновременно строить bot, downloader, web UI и несколько альтернативных провайдеров до того, как работает основной сценарий, это почти наверняка ошибка приоритизации.
