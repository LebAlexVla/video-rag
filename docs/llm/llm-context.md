# LLM Context

## 1. Назначение документа

Этот документ — канонический технический контекст для нейросетей, которые используются в проекте для:
- генерации кода;
- генерации отдельных модулей;
- проверки реализации;
- рефакторинга;
- анализа архитектурных изменений;
- оценки предлагаемых технологических решений.

Документ описывает только согласованную MVP-версию системы. Если код, предложение по рефакторингу или архитектурное изменение противоречит этому документу, приоритет у этого документа.

---

## 2. Назначение проекта

Video Lecture RAG Assistant — локальная система для поиска ответов по видеолекциям.

Пользователь задаёт вопрос по содержанию лекции. Система ищет релевантные фрагменты в транскрибированном и проиндексированном материале и формирует ответ только на основе найденного контекста.

Первая версия рассчитана на:
- локальный запуск на ноутбуке;
- малый объём данных;
- 1–10 лекций;
- команду из 4 студентов;
- реализацию рабочего MVP за короткий срок.

---

## 3. Цели MVP

MVP должен решить одну основную задачу: дать рабочий, простой и надёжный RAG по видеолекциям без лишней инфраструктурной сложности.

### Основные цели
1. Принимать локальный видеофайл и подготавливать его для поиска.
2. Давать ответы по содержанию лекций на основе реального контекста.
3. Минимизировать галлюцинации модели.
4. Сохранять C# как центральную часть системы.
5. Запускаться локально без обязательного облака.
6. Быть реализуемым небольшой командой за короткий срок.
7. Оставлять минимально необходимые точки расширения на будущее.

### Что считается успешным MVP
- можно загрузить локальную лекцию;
- можно задать вопрос;
- система возвращает полезный ответ или честный fallback;
- ответ содержит источник: лекция и примерное место в ней.

---

## 4. Границы MVP

### 4.1 Что входит в MVP

В MVP входит:
- локальный запуск системы;
- ingest локального видеофайла;
- транскрибация лекции;
- чтение транскрипта в C#;
- chunking в C#;
- построение векторов для чанков;
- сохранение векторов и метаданных в Qdrant;
- приём вопроса через Minimal API;
- retrieval релевантных чанков;
- генерация ответа на основе найденного контекста;
- возврат ответа с указанием источника;
- ручной rebuild индекса.

### 4.2 Что не входит в MVP

Сознательно не делаем в первой версии:
- загрузку видео по ссылке;
- downloader для Rutube / VK / YouTube;
- дедупликацию видео;
- автоматическое обновление индекса;
- очереди задач;
- брокеры сообщений;
- микросервисы;
- distributed workers;
- chat memory;
- auth и роли пользователей;
- multi-tenant режим;
- web UI;
- word-level timestamps;
- OCR слайдов и доски;
- сложный мониторинг;
- production deployment;
- полную матрицу провайдеров и всех комбинаций интеграций.

### 4.3 Жёсткие ограничения MVP

Следующие ограничения считаются обязательными и не должны нарушаться:

1. **C# — центр системы и единственная точка оркестрации.**
2. **Python helper только транскрибирует и пишет `transcript.json`.**
3. **Chunking выполняется в C#.**
4. **Нет микросервисов.**
5. **Нет внутреннего REST, gRPC и очередей между C# и Python.**
6. **Основной интерфейс MVP: `CLI ingest + Minimal API ask`.**
7. **Telegram bot — optional.**
8. **Web UI — вне MVP.**
9. **`ILLMProvider` не используется как основной контракт MVP.**
10. **Интерфейсы лежат в `Application/Abstractions`.**
11. **`Infrastructure` содержит только реализации и адаптеры.**
12. **Rebuild выполняется вручную.**
13. **Если меняется embedding space, обязателен rebuild.**

---

## 5. Финальная архитектура

### 5.1 Общая схема

Архитектура MVP:
- одно основное C# приложение;
- один Python CLI-helper для транскрибации;
- локальный Qdrant как vector store;
- локальная файловая система для видео, job input и transcript JSON.

Главный принцип: вся оркестрация находится в C#. Python используется только как технический helper там, где для транскрибации это практично.

```text
Пользователь / Админ
        |
        v
+---------------------------+
|        C# приложение      |
|---------------------------|
| CLI / API / orchestration |
| ingest / ask / rebuild    |
+------+-------------+------+
       |             |
       |             v
       |      +-------------+
       |      |   Qdrant    |
       |      | vector store|
       |      +-------------+
       |
       v
+---------------------------+
|     Python CLI helper     |
|---------------------------|
| transcription only        |
| output: transcript.json   |
+-------------+-------------+
              |
              v
         файловая система
```

### 5.2 Ответственность C#

C# отвечает за:
- входные сценарии системы;
- orchestration ingest, ask, rebuild;
- работу с источником видео;
- запуск Python helper;
- чтение `transcript.json`;
- chunking;
- построение embeddings;
- запись в Qdrant;
- retrieval;
- генерацию ответа;
- прикладную валидацию;
- перевод технических ошибок в понятные прикладные ответы.

### 5.3 Ответственность Python

Python отвечает только за:
- чтение локального видеофайла;
- запуск провайдера транскрибации;
- формирование нормализованного `transcript.json`;
- возврат exit code.

Python не должен:
- выполнять chunking;
- строить embeddings;
- писать в Qdrant;
- отвечать на вопросы;
- содержать API;
- содержать webhook;
- оркестрировать сценарии ingest / ask / rebuild.

---

## 6. Главные сценарии

## 6.1 Ingest

### Назначение
Подготовить одну лекцию к поиску.

### Поток выполнения

```text
Admin
 -> CLI ingest
 -> C# ILectureIngestService
 -> IVideoSource.ResolveAsync
 -> ITranscriptionRunner.RunAsync
 -> Python helper
 -> transcript.json
 -> ITranscriptReader.ReadAsync
 -> IChunker.Chunk
 -> IEmbeddingProvider.EmbedBatchAsync
 -> IVectorStore.UpsertLectureChunksAsync
```

### Результат
Лекция становится доступной для поиска.

---

## 6.2 Ask

### Назначение
Вернуть ответ по уже проиндексированным лекциям.

### Поток выполнения

```text
User
 -> Minimal API ask endpoint
 -> IAskService.AskAsync
 -> IContextRetriever.RetrieveAsync
 -> IEmbeddingProvider.EmbedAsync(question)
 -> IVectorStore.SearchAsync
 -> IAnswerGenerator.GenerateAsync
 -> AskResponse
```

### Результат
Пользователь получает:
- ответ;
- список источников;
- название лекции;
- примерное место в лекции.

Если подходящего контекста нет или найденный контекст недостаточно релевантен по порогу retrieval, система возвращает честный fallback.

---

## 6.3 Rebuild

### Назначение
Пересобрать индекс вручную.

### Поток выполнения

```text
Admin
 -> CLI rebuild
 -> ILectureIngestService.RebuildAsync
 -> IVectorStore.ClearAsync
 -> повторное чтение сохранённых transcript.json
 -> chunking
 -> embeddings
 -> повторная запись в Qdrant
```

### Когда rebuild обязателен
- изменился provider embeddings;
- изменилась модель embeddings;
- изменилась размерность embeddings;
- изменилась семантика embedding space;
- нужно очистить индекс после ошибки или повтора ingest.

---

## 7. Слои системы и правила зависимостей

## 7.1 Domain

Domain содержит:
- предметные сущности;
- их инварианты;
- value objects.

Примеры:
- `Lecture`
- `Transcript`
- `TranscriptSegment`
- `LectureChunk`
- `RetrievedContext`
- `SourceCitation`
- `AnswerResult`

Domain не должен содержать:
- DTO сценариев;
- JSON-модели;
- интерфейсы внешних зависимостей;
- зависимости на SDK;
- знания о Python helper, Qdrant, OpenAI, Ollama.

---

## 7.2 Application

Application содержит:
- сценарии системы;
- прикладные DTO;
- интерфейсы, через которые прикладной слой работает с внешним миром;
- orchestration-сервисы.

Структурно Application делится на:
- `Application/Abstractions`
- `Application/Contracts`
- `Application/Services`

### Application/Abstractions
Каноническое место для интерфейсов:
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

### Application/Contracts
DTO и прикладные контракты:
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

### Application/Services
Сервисы orchestration:
- `AskService`
- `LectureIngestService`
- `ContextRetriever`
- `AnswerGenerator`
- `Chunker`

---

## 7.3 Infrastructure

Infrastructure содержит:
- реализации интерфейсов из `Application/Abstractions`;
- адаптеры внешних систем;
- работу с процессами, JSON, файлами, SDK и Qdrant.

Примеры:
- `LocalFileVideoSource`
- `PythonTranscriptionRunner`
- `JsonTranscriptReader`
- `OllamaEmbeddingProvider`
- `OpenAiEmbeddingProvider`
- `QdrantVectorStore`
- `OllamaAnswerGenerator`
- `OpenAiAnswerGenerator`

Infrastructure не должна содержать:
- orchestration use case;
- правила сценариев;
- прикладную бизнес-логику сверх технической интеграции.

---

## 7.4 Разрешённые зависимости

Разрешено:
- `Application -> Domain`
- `Infrastructure -> Application`
- `Infrastructure -> Domain`

Запрещено:
- `Domain -> Application`
- `Domain -> Infrastructure`
- `Application -> Infrastructure` по concrete-классам

Дополнительные правила:
- API/CLI слой не должен напрямую работать с Qdrant;
- API/CLI слой не должен напрямую вызывать SDK моделей;
- Application знает только интерфейсы;
- Infrastructure реализует эти интерфейсы;
- связывание выполняется через DI.

---

## 8. Структура проекта

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

## 9. Канонические интерфейсы

Ниже перечислены интерфейсы, которые считаются официальными контрактами MVP.

## 9.1 IVideoSource

Назначение: нормализовать и проверить источник видео перед ingest.

```csharp
public interface IVideoSource
{
    string SourceType { get; }

    Task<VideoSourceDescriptor> ResolveAsync(
        string input,
        CancellationToken cancellationToken = default);
}
```

Обязан делать:
- принимать пользовательский ввод источника;
- проверять допустимость источника;
- возвращать нормализованный путь и базовые метаданные.

Не должен делать:
- запускать транскрибацию;
- читать `transcript.json`;
- писать в индекс.

---

## 9.2 ITranscriptionRunner

Назначение: запустить Python helper и вернуть результат выполнения.

```csharp
public interface ITranscriptionRunner
{
    Task<TranscriptionRunResult> RunAsync(
        TranscriptionRunRequest request,
        CancellationToken cancellationToken = default);
}
```

Обязан делать:
- подготовить входной JSON;
- запустить процесс;
- дождаться завершения;
- вернуть путь к транскрипту или ошибку.

Не должен делать:
- разбирать `transcript.json`;
- делать chunking;
- писать в Qdrant.

---

## 9.3 ITranscriptReader

Назначение: читать и валидировать `transcript.json`, переводить его в доменную модель.

```csharp
public interface ITranscriptReader
{
    Task<Transcript> ReadAsync(
        string transcriptPath,
        CancellationToken cancellationToken = default);
}
```

Обязан делать:
- читать JSON;
- валидировать обязательные поля;
- валидировать сегменты;
- возвращать `Transcript`.

Не должен делать:
- запускать Python helper;
- делать chunking;
- строить embeddings.

---

## 9.4 IChunker

Назначение: преобразовать транскрипт в чанки для индексации.

```csharp
public interface IChunker
{
    IReadOnlyList<LectureChunk> Chunk(Transcript transcript);
}
```

Обязан делать:
- сохранять порядок материала;
- присваивать `ChunkIndex`;
- выставлять приблизительные временные границы.

Не должен делать:
- обращаться к Qdrant;
- строить embeddings;
- менять исходный транскрипт.

---

## 9.5 IEmbeddingProvider

Назначение: построение векторов.

```csharp
public interface IEmbeddingProvider
{
    string ProviderName { get; }
    string ModelName { get; }

    Task<float[]> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);
}
```

Обязан делать:
- строить embedding для одного текста;
- строить embeddings пакетно;
- гарантировать стабильную размерность в рамках выбранной модели.

Не должен делать:
- искать контекст;
- применять `MinScore`;
- строить ответ пользователю.

`IEmbeddingProvider` — основной контракт провайдера векторов в MVP.

---

## 9.6 IVectorStore

Назначение: запись и поиск в vector store.

```csharp
public interface IVectorStore
{
    Task UpsertLectureChunksAsync(
        IReadOnlyList<EmbeddedLectureChunk> chunks,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RetrievedContext>> SearchAsync(
        float[] queryVector,
        int topK,
        CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
```

Обязан делать:
- сохранять embedding и payload;
- искать top-k по вектору;
- очищать индекс при rebuild.

Не должен делать:
- строить embedding вопроса;
- применять бизнес-порог релевантности;
- формировать ответ.

---

## 9.7 IContextRetriever

Назначение: получить релевантный контекст для ответа.

```csharp
public interface IContextRetriever
{
    Task<ContextRetrievalResult> RetrieveAsync(
        AskRequest request,
        CancellationToken cancellationToken = default);
}
```

Обязан делать:
- строить embedding вопроса;
- выполнять поиск;
- применять `TopK`;
- применять `MinScore`;
- возвращать найденный контекст.

Не должен делать:
- вызывать модель ответа;
- форматировать пользовательский ответ.

---

## 9.8 IAnswerGenerator

Назначение: построить итоговый ответ по вопросу и найденному контексту.

```csharp
public interface IAnswerGenerator
{
    Task<AnswerResult> GenerateAsync(
        AskRequest request,
        IReadOnlyList<RetrievedContext> context,
        CancellationToken cancellationToken = default);
}
```

Обязан делать:
- собирать запрос к модели;
- вызывать модель;
- возвращать `AnswerResult`.

Не должен делать:
- искать контекст;
- запускать транскрибацию;
- работать с Qdrant напрямую.

`IAnswerGenerator` — основной контракт генерации ответа в MVP.

---

## 9.9 ILectureIngestService

Назначение: orchestration сценариев ingest и rebuild.

```csharp
public interface ILectureIngestService
{
    Task<LectureIngestResult> IngestAsync(
        LectureIngestRequest request,
        CancellationToken cancellationToken = default);

    Task<LectureRebuildResult> RebuildAsync(
        LectureRebuildRequest request,
        CancellationToken cancellationToken = default);
}
```

Обязан делать:
1. нормализовать источник;
2. запустить транскрибацию;
3. прочитать транскрипт;
4. нарезать чанки;
5. построить embeddings;
6. сохранить их в индекс.

Не должен делать:
- отвечать на вопросы;
- содержать UI-логику.

---

## 9.10 IAskService

Назначение: orchestration сценария ответа на вопрос.

```csharp
public interface IAskService
{
    Task<AskResponse> AskAsync(
        AskRequest request,
        CancellationToken cancellationToken = default);
}
```

Обязан делать:
1. валидировать вопрос;
2. получить контекст;
3. при пустом или недостаточно релевантном контексте вернуть fallback;
4. иначе вызвать генератор ответа;
5. вернуть `AskResponse`.

Не должен делать:
- запускать Python helper;
- работать с видеофайлами;
- знать детали транскрибации.

---

## 10. JSON-контракт C# ↔ Python

## 10.1 Общие правила

- формат: `UTF-8 JSON`;
- имена полей: `camelCase`;
- Python helper получает один входной JSON;
- Python helper пишет один выходной JSON;
- Python helper не пишет в Qdrant;
- C# продолжает ingest только если:
  - процесс завершился успешно;
  - выходной JSON валиден;
  - `status == "success"`.

---

## 10.2 Входной JSON

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

### Обязательные поля
- `jobId`
- `inputVideoPath`
- `outputTranscriptPath`
- `transcriptionProvider`
- `transcriptionModel`
- `overwrite`

### Необязательные поля
- `requestedTitle`
- `languageHint`

### Инварианты
- `jobId` непустой;
- `inputVideoPath` непустой;
- `outputTranscriptPath` непустой;
- `transcriptionProvider` непустой;
- `transcriptionModel` непустой.

---

## 10.3 Выходной JSON при успехе

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
      "text": "Сегодня мы начинаем разбирать основы термодинамики."
    }
  ]
}
```

### Обязательные поля
Корень:
- `jobId`
- `status`
- `lecture`
- `transcriber`
- `segments`

`lecture`:
- `title`
- `sourceFileName`
- `sourcePath`

`transcriber`:
- `provider`
- `model`

`segments[*]`:
- `index`
- `startSec`
- `endSec`
- `text`

### Необязательные поля
- `lecture.language`
- `lecture.durationSec`

### Инварианты
- `status == "success"`;
- `segments.length >= 1`;
- сегменты отсортированы по `index`;
- `index` уникален в рамках файла;
- для каждого сегмента:
  - `index >= 0`;
  - `startSec >= 0`;
  - `endSec > startSec`;
  - `text` непустой после `trim`.

---

## 10.4 Выходной JSON при ошибке

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

### Обязательные поля
- `jobId`
- `status`
- `error.code`
- `error.message`

### Инварианты
- `status == "failed"`;
- `error != null`.

---

## 10.5 Коды завершения

| Код | Значение | Поведение C# |
|---:|---|---|
| `0` | Успех | Читать и валидировать `transcript.json` |
| `1` | Ожидаемая ошибка обработки | Завершить ingest с понятной ошибкой |
| `2` | Ошибка входных данных | Считать ошибкой валидации/конфигурации |
| `3` | Файл не найден или недоступен | Считать ошибкой источника |
| `10` | Внутренняя ошибка helper | Считать ошибкой интеграции |

### Правило обработки
- `exitCode == 0` недостаточно;
- C# обязан проверить:
  - существование выходного файла;
  - корректность JSON;
  - `status == "success"`.

---

## 11.1 Файловая система

Используется для:
- исходных видео;
- входных JSON для helper;
- выходных `transcript.json`;
- локального manifest/registry для rebuild;
- конфигурации;
- временных файлов.

Минимальное правило MVP:
- после успешного ingest система должна сохранять `transcript.json`;
- для каждой проиндексированной лекции система должна сохранять минимальную запись, достаточную для rebuild;
- rebuild не должен зависеть от повторной транскрибации, если нужные артефакты уже сохранены локально.

Минимально необходимая запись для rebuild должна содержать:
- `lectureId`;
- `lectureTitle`;
- путь к сохранённому `transcript.json`;
- исходный `sourcePath` или `sourceFileName` как справочную информацию.

Пример:

```text
/data/videos/
/data/transcripts/
/data/jobs/
/data/registry/lectures.json
```

---

## 11.2 Qdrant

Qdrant хранит только retrieval-представление данных.

### Канонический payload MVP

```json
{
  "chunkId": "chunk-001",
  "lectureId": "lecture-001",
  "lectureTitle": "Physics Lecture 1",
  "chunkIndex": 12,
  "text": "...",
  "approxMinute": 24,
  "approxStartSec": 1410.0,
  "approxEndSec": 1470.0
}
```

### Обязательные поля
- `chunkId`
- `lectureId`
- `lectureTitle`
- `chunkIndex`
- `text`
- `approxMinute`

### Необязательные поля
- `approxStartSec`
- `approxEndSec`

Qdrant не используется для:
- истории чатов;
- статусов задач;
- пользовательских данных;
- конфигурации проекта.

---

## 12. Инварианты и правила валидации

## 12.1 AskRequest
- `Question` обязателен;
- `Question.Trim()` не пустой;
- рекомендуемый предел для MVP: до `1000` символов;
- `TopK` в диапазоне `1..10`;
- `MinScore` в диапазоне `0..1`.

## 12.1.1 Минимальный AskResponse

Минимальный ответ MVP должен содержать:
- итоговый текст ответа;
- признак, найден ли достаточный контекст;
- список источников;
- при наличии — диагностическое сообщение для fallback.

### Пример успешного ответа

```json
{
  "answer": "Во второй части лекции объясняется, что градиентный спуск минимизирует функцию потерь итеративно, обновляя параметры модели по направлению антиградиента.",
  "usedContext": true,
  "sources": [
    {
      "lectureTitle": "ML Lecture 2",
      "chunkIndex": 7,
      "approxMinute": 18,
      "approxStartSec": 1060.0,
      "approxEndSec": 1125.0
    }
  ],
  "message": null
}
```

### Пример fallback-ответа
```json
{
  "answer": null,
  "usedContext": false,
  "sources": [],
  "message": "Недостаточно релевантного контекста для уверенного ответа по загруженным лекциям."
}
```

## 12.2 LectureIngestRequest
- `InputPath` обязателен;
- путь должен указывать на существующий локальный файл;
- если `RequestedTitle` задан, он не должен быть пустым после `trim`.

## 12.3 TranscriptSegment
Для каждого сегмента:
- `Index >= 0`;
- `StartSec >= 0`;
- `EndSec > StartSec`;
- `Text.Trim()` не пустой.

Для списка сегментов:
- индексы уникальны;
- порядок по `Index` возрастающий;
- временные границы не должны идти назад.

## 12.4 LectureChunk
Для каждого чанка:
- `ChunkIndex >= 0`;
- `Text.Trim()` не пустой;
- `LectureId` непустой;
- `LectureTitle` непустой.

Дополнительное правило MVP:
- chunker обязан сохранять порядок материала;
- размер чанка может быть конфигурационным;
- в первой версии достаточно стабильной и простой стратегии chunking.

## 12.5 Rebuild
- rebuild — ручная операция;
- rebuild может полностью очищать индекс;
- rebuild обязателен при смене provider/model embeddings, если меняется векторное пространство;
- rebuild не запускается автоматически при изменении конфигурации.
- rebuild по умолчанию использует ранее сохранённые `transcript.json`, а не повторную транскрибацию;

## 12.6 Смена embedding model/provider
Если меняется:
- модель embeddings;
- провайдер embeddings;
- размерность embedding;
- семантика embedding space,

то:
- старый индекс считается несовместимым;
- требуется ручной rebuild.

Смешивание embeddings разных моделей в одном индексе в MVP запрещено.

---

## 13. Правила расширения

## 13.1 Новый провайдер транскрибации
Добавляется в Python helper.

Обязательные условия:
- принимает тот же входной JSON;
- возвращает тот же выходной JSON;
- сохраняет обязательные поля и инварианты;
- не требует изменений Application-логики C#.

Запрещено:
- делать формат `transcript.json` зависимым от конкретного провайдера;
- переносить логику провайдера в `ITranscriptReader`.

---

## 13.2 Новый провайдер векторов
Добавляется как реализация `IEmbeddingProvider`.

Обязательные условия:
- реализует `EmbedAsync` и `EmbedBatchAsync`;
- фиксирует `ProviderName` и `ModelName`;
- документирует размерность embeddings;
- требует rebuild при смене embedding space.

Запрещено:
- смешивать embeddings разных моделей в одном индексе без отдельного решения.

---

## 13.3 Новый генератор ответа
Добавляется как реализация `IAnswerGenerator`.

Обязательные условия:
- принимает вопрос и найденный контекст;
- возвращает `AnswerResult`;
- не меняет контракт `IAskService`.

Запрещено:
- встраивать retrieval в генератор ответа;
- связывать генератор ответа с Qdrant напрямую.

---

## 13.4 Downloader в будущем
Добавляется как новая реализация `IVideoSource`.

Целевое правило:
- после `ResolveAsync` система должна получить локальный путь к файлу;
- дальнейший pipeline не должен зависеть от типа источника.

Запрещено:
- смешивать скачивание и транскрибацию в одном интерфейсе;
- заставлять прикладной слой знать детали платформенных URL.

## 13.5 Основной стек провайдеров для первой реализации MVP

Для первой рабочей реализации команда выбирает один основной локальный путь и доводит до конца именно его.

Приоритет MVP:
1. сначала локальный рабочий стек;
2. затем, только при наличии времени, optional / запасные реализации.

### Канонический путь первой реализации
- транскрибация: один выбранный Python transcription provider;
- embeddings: один основной локальный `IEmbeddingProvider`;
- генерация ответа: один основной локальный `IAnswerGenerator`;
- vector store: локальный Qdrant.

### Правило приоритета
Пока не работает полный путь `ingest -> transcript -> chunking -> embeddings -> Qdrant -> ask -> answer`, команда не должна распараллеливать разработку на несколько равноправных provider-комбинаций.

### Optional после основного пути
После завершения основного локального пути допускается:
- добавить один запасной provider embeddings;
- добавить один запасной provider answer generation.

---

## 14. Сознательные упрощения MVP

1. Один пользователь / маленькая команда.
2. Нет auth.
3. Нет chat memory.
4. Нет duplicate detection.
5. Один ingestion за раз.
6. Админ вручную запускает rebuild.
7. Approximate timestamps only.
8. Ошибки можно чинить вручную.
9. Основной интерфейс MVP: `CLI ingest + Minimal API ask`.
10. Telegram bot — optional.
11. Web UI — out of scope.

---

## 15. Правила, которые нельзя нарушать при генерации кода

LLM, генерирующая код для проекта, не должна:
- превращать Python helper в сервис;
- переносить chunking в Python;
- добавлять внутренний REST между C# и Python;
- вводить очередь задач;
- переносить интерфейсы из `Application/Abstractions` в `Domain`;
- класть concrete-реализации в `Application`;
- использовать `ILLMProvider` как основной официальный контракт MVP;
- делать Telegram обязательной частью MVP;
- добавлять web UI как обязательную часть первой версии;
- реализовывать downloader, дедупликацию и точные таймкоды внутри MVP без отдельного запроса;
- писать в Qdrant из Python;
- смешивать retrieval-данные с общей операционной моделью проекта.

---

## 16. Итог

Код и архитектурные изменения считаются согласованными с MVP, если они сохраняют следующие свойства:
- C# остаётся центром системы;
- Python остаётся transcription helper;
- chunking выполняется в C#;
- интерфейсы находятся в `Application/Abstractions`;
- `Infrastructure` содержит только реализации;
- основной интерфейс MVP — `CLI ingest + Minimal API ask`;
- storage model Qdrant остаётся совместимой с каноническим payload этого документа.