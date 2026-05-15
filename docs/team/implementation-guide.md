# Implementation Guide

Практические правила для разработки и расширения проекта.

Документ отвечает на вопросы:

- куда класть код;
- какие зависимости допустимы;
- как добавлять новые реализации провайдеров;
- что считается архитектурным нарушением;
- какие минимальные тесты нужны.

## Структура C# проекта

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
```

## Domain

`Domain` содержит предметные сущности и их инварианты.

Примеры:

```text
Lecture
Transcript
TranscriptSegment
LectureChunk
RetrievedContext
SourceCitation
AnswerResult
```

В `Domain` можно класть:

- сущности предметной области;
- value objects;
- простые инварианты модели.

В `Domain` нельзя класть:

- DTO API;
- DTO CLI;
- JSON-модели helper;
- Qdrant payload models;
- interfaces внешних интеграций;
- зависимости на SDK, HTTP, файловую систему, Qdrant, Ollama, OpenAI.

Правило: если тип описывает внешний транспорт или конкретный сценарий приложения, это не `Domain`.

## Application

`Application` содержит прикладные сценарии, contracts и abstractions.

Application делится на три зоны:

```text
Application/Abstractions
Application/Contracts
Application/Services
```

### Application/Abstractions

Здесь лежат interfaces, через которые application layer работает с внешним миром.

Примеры:

```text
IVideoSource
ITranscriptionRunner
ITranscriptReader
IChunker
IEmbeddingProvider
IVectorStore
IContextRetriever
IAnswerGenerator
ILectureIngestService
IAskService
```

Правила:

- interfaces внешних зависимостей лежат здесь;
- application services зависят от interfaces, а не от concrete classes;
- новые provider interfaces добавляются только если существующих abstractions недостаточно.

Не нужно добавлять интерфейс, если он не используется для реальной границы или замены реализации.

### Application/Contracts

Здесь лежат DTO и contracts прикладных сценариев.

Примеры:

```text
AskRequest
AskResponse
LectureIngestRequest
LectureIngestResult
LectureRebuildRequest
LectureRebuildResult
ContextRetrievalResult
TranscriptionRunRequest
TranscriptionRunResult
VideoSourceDescriptor
EmbeddedLectureChunk
ErrorInfo
```

Правила:

- request/response models для application services лежат здесь;
- промежуточные модели между application и infrastructure лежат здесь;
- contracts должны быть независимы от SDK и конкретных провайдеров.

### Application/Services

Здесь лежит orchestration и прикладная логика.

Примеры:

```text
AskService
LectureIngestService
ContextRetriever
AnswerGenerator
Chunker
```

Application services могут:

- валидировать прикладные requests;
- вызывать abstractions;
- управлять последовательностью сценария;
- применять business/application rules;
- возвращать contracts.

Application services не должны:

- напрямую создавать HTTP clients;
- напрямую обращаться к Qdrant;
- напрямую вызывать Ollama/OpenAI SDK;
- напрямую запускать Python process;
- содержать UI-логику.

## Infrastructure

`Infrastructure` содержит concrete implementations interfaces из `Application/Abstractions`.

Примеры:

```text
LocalFileVideoSource
PythonTranscriptionRunner
JsonTranscriptReader
OllamaEmbeddingProvider
OpenAiEmbeddingProvider
QdrantVectorStore
OllamaAnswerGenerator
OpenAiAnswerGenerator
QdrantInitializationService
```

В `Infrastructure` можно класть:

- работу с HTTP;
- работу с файловой системой;
- запуск внешнего процесса;
- Qdrant integration;
- Ollama/OpenAI integration;
- чтение и валидацию внешнего JSON;
- provider-specific code;
- configuration options.

В `Infrastructure` нельзя класть:

- orchestration всего use case;
- UI-логику;
- правила выбора пользовательского сценария;
- domain decisions, которые не зависят от внешней интеграции.

## Pages

`Pages` — UI-слой для локального Razor test/demo UI.

Он находится отдельно от `src/Domain`, `src/Application` и `src/Infrastructure`, потому что это входной слой приложения, а не часть бизнес-логики.

Примеры:

```text
Pages/Index.cshtml
Pages/Index.cshtml.cs
Pages/_ViewImports.cshtml
```

### Правила для PageModel

PageModel может зависеть от:

```text
IAskService
```

PageModel не должен зависеть от:

```text
QdrantVectorStore
OllamaAnswerGenerator
OpenAiAnswerGenerator
OllamaEmbeddingProvider
PythonTranscriptionRunner
JsonTranscriptReader
HttpClient for providers
Qdrant HTTP API
Ollama HTTP API
OpenAI HTTP API
```

PageModel должен:

- принимать UI input;
- валидировать форму на уровне UI;
- создавать application request;
- вызывать application service;
- показывать result/error.

PageModel не должен:

- выполнять retrieval;
- строить embeddings;
- генерировать prompt;
- обращаться к Qdrant;
- обращаться к Ollama/OpenAI;
- запускать Python helper;
- читать transcript files напрямую.

## CLI / API composition

CLI и Minimal API являются входными слоями.

Они могут:

- принимать аргументы/HTTP requests;
- создавать application contracts;
- вызывать application services;
- возвращать ответ пользователю.

Они не должны напрямую работать с infrastructure classes.

Composition root связывает interfaces и implementations через DI. Это единственное место, где допустимо знать concrete infrastructure classes.

## Как добавлять новый transcription provider

Transcription provider добавляется внутри Python helper.

Правила:

- входной JSON C# -> Python не должен ломаться;
- выходной `transcript.json` должен сохранять тот же контракт;
- exit codes должны оставаться совместимыми;
- Python не должен выполнять chunking;
- Python не должен строить embeddings;
- Python не должен писать в Qdrant;
- Application C# не должен знать детали конкретного transcription provider.

Если нужно выбрать provider, выбор передаётся через существующие поля request/config, например:

```text
transcriptionProvider
transcriptionModel
```

## Как добавлять новый embedding provider

Новый embedding provider добавляется как implementation `IEmbeddingProvider`.

Куда класть:

```text
src/Infrastructure/Embeddings/
```

Что нужно сделать:

1. Реализовать `IEmbeddingProvider`.
2. Добавить provider-specific options в configuration.
3. Зарегистрировать реализацию в DI.
4. Документировать vector size.
5. Проверить совместимость с Qdrant collection.
6. Добавить минимальные tests/parsing checks, если provider возвращает сложный JSON.

Правила:

- provider должен возвращать стабильную размерность vectors;
- `EmbedAsync` и `EmbedBatchAsync` должны быть согласованы;
- пустой input должен обрабатываться предсказуемо;
- смена embedding model/provider/vector space требует rebuild;
- embeddings разных моделей нельзя смешивать в одной collection.

## Как добавлять новый answer provider

Новый answer provider добавляется как implementation `IAnswerGenerator`.

Куда класть:

```text
src/Infrastructure/Answers/
```

Что нужно сделать:

1. Реализовать `IAnswerGenerator`.
2. Добавить provider-specific options в configuration.
3. Зарегистрировать реализацию в DI.
4. Сохранить правило grounded answer.
5. Добавить fallback rule в prompt.

Правила:

- generator получает только вопрос и найденный context;
- generator не должен выполнять retrieval;
- generator не должен строить embeddings;
- generator не должен обращаться к Qdrant;
- generator не должен отвечать по внешним знаниям, если контекста недостаточно.

Prompt должен явно запрещать добавление фактов вне контекста.

### DeepSeek как текущий answer provider

DeepSeek API совместим с форматом OpenAI. Реализация переиспользует `OpenAiAnswerGenerator` без изменений кода — отличается только `HttpClient` (`deepseek-answers`) и `BaseAddress`.

Конфигурация провайдера — в `appsettings.json` секция `Answers.DeepSeek`. API-ключ — через `.env` (переменная `DEEPSEEK_API_TOKEN`). Загружается автоматически при старте через `LoadDotEnv()` в `Program.cs`.

Чтобы добавить другой OpenAI-совместимый провайдер по аналогии:

1. Добавить `XxxProviderOptions` в `AnswersOptions.cs`.
2. Добавить `HttpClient` в `ConfigureHttpClients`.
3. Добавить case в switch в `ConfigureApplicationServices`.
4. Добавить case в `IsSupportedProvider` и `ValidateAnswersProviderOptions`.
5. Добавить секцию в `appsettings.json`.

## Как добавлять downloader в будущем

Downloader добавляется как новая implementation `IVideoSource`.

Куда класть:

```text
src/Infrastructure/VideoSources/
```

Целевое поведение:

```text
input URL/path -> local video file path
```

Правила:

- после `ResolveAsync` pipeline должен получить локальный файл;
- downstream ingest flow не должен знать, был файл локальным или скачанным;
- скачивание не должно смешиваться с транскрибацией;
- downloader не должен вызывать Python helper;
- downloader не должен писать в Qdrant;
- downloader не должен менять transcript contract.

Если downloader требует сложной политики retry/cache/auth, это отдельное архитектурное решение.

## Rebuild и embedding space

Rebuild используется для ручной пересборки индекса.

Правила:

- rebuild использует сохранённые `transcript.json`;
- rebuild не обязан повторно запускать Python helper;
- rebuild может очищать индекс перед повторной записью;
- при смене embedding space старый индекс считается несовместимым.

Если меняется vector size, простого clear points может быть недостаточно. Нужно удалить старую Qdrant collection или volume и затем выполнить ingest/rebuild заново.

## Минимальные правила тестирования

Минимально полезные tests:

### Domain

- создание валидных entities;
- ошибка при пустых обязательных полях;
- ошибка при невалидных timestamps;
- проверка порядка transcript segments.

### Application

- `Chunker` сохраняет порядок текста;
- `Chunker` выставляет chunk indexes;
- `Chunker` выставляет примерные timestamps;
- `ContextRetriever` применяет `TopK` и `MinScore`;
- `AskService` возвращает fallback при пустом context;
- `LectureIngestService` корректно обрабатывает ошибку transcription runner.

### Infrastructure

- `JsonTranscriptReader` читает валидный transcript;
- `JsonTranscriptReader` падает на невалидном transcript;
- provider response parsing tests;
- Qdrant request payload shape tests, если возможно без реального Qdrant.

### UI

- PageModel создаёт `AskRequest`;
- PageModel вызывает `IAskService`;
- PageModel не содержит infrastructure calls.

Не нужно начинать с тяжёлых end-to-end tests. Сначала достаточно unit tests на стабильные правила.

## Признаки архитектурных нарушений

Нарушение, если:

- Python helper делает chunking;
- Python helper строит embeddings;
- Python helper пишет в Qdrant;
- Python helper отвечает пользователю;
- между C# и Python появляется internal REST/gRPC/queue;
- API endpoint напрямую вызывает Qdrant;
- Razor PageModel напрямую вызывает infrastructure implementation;
- Application service создаёт concrete infrastructure class;
- Domain зависит от Application или Infrastructure;
- Infrastructure содержит полный use case orchestration;
- Qdrant используется как общая база приложения;
- embeddings разных моделей смешиваются в одной collection;
- Razor UI становится основным продуктовым frontend-контуром;
- добавляется downloader без сохранения локального файла как результата `IVideoSource`.

## Общее правило

Если новая логика относится к сценарию пользователя — вероятно, это `Application`.

Если новая логика относится к внешней системе, файлам, HTTP, SDK или процессам — вероятно, это `Infrastructure`.

Если новая логика только принимает ввод и показывает результат — это входной слой: CLI, API или Pages.

Если новая модель описывает предметную сущность и не зависит от транспорта — это `Domain`.
