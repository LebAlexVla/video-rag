# Implementation Guide

Практическое руководство по разработке и расширению проекта.

Документ отвечает на вопросы:

- куда класть новый код;
- какие зависимости допустимы;
- как добавлять новые providers и clients;
- какие проверки делать перед merge.

Подробная архитектура описана в [Architecture](./architecture.md). Здесь — прикладные правила для ежедневной разработки.

## 1. Структура проекта

```text
src/
  Domain/
  Application/
  Infrastructure/

Pages/
  встроенный Razor UI основного приложения

clients/
  внешние клиенты

shared/
  общие DTO-контракты

scripts/
  python-helper/

docs/
  документация

data/
  локальные данные проекта
```

## 2. Domain

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

- HTTP DTO;
- CLI DTO;
- JSON-модели Python helper;
- Qdrant payload models;
- provider-specific models;
- interfaces внешних интеграций;
- работу с файлами, HTTP, SDK или БД.

Правило: если тип описывает внешний транспорт, конкретный API или сценарий приложения, это не `Domain`.

## 3. Application

`Application` содержит сценарии, contracts и interfaces.

```text
Application/
  Abstractions/
  Contracts/
  Services/
```

### Abstractions

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

Новый interface добавляется только если появляется реальная граница ответственности или несколько возможных реализаций.

### Contracts

Здесь лежат request/response models прикладных сценариев.

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

Contracts не должны зависеть от provider SDK, HTTP clients, Qdrant-specific моделей или UI.

### Services

Здесь лежит orchestration и прикладная логика.

Примеры:

```text
AskService
LectureIngestService
ContextRetriever
Chunker
AnswerGenerator
```

Application services могут:

- валидировать requests;
- вызывать abstractions;
- управлять последовательностью use case;
- применять application rules;
- возвращать contracts.

Application services не должны:

- напрямую создавать HTTP clients;
- напрямую обращаться к Qdrant;
- напрямую вызывать provider SDK;
- напрямую запускать Python process;
- содержать UI-логику.

## 4. Infrastructure

`Infrastructure` содержит concrete implementations interfaces из `Application/Abstractions`.

Примеры:

```text
LocalFileVideoSource
PythonTranscriptionRunner
JsonTranscriptReader
OllamaEmbeddingProvider
OpenAiEmbeddingProvider
GeminiEmbeddingProvider
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
- provider integrations;
- чтение и валидацию внешнего JSON;
- configuration options;
- technical initialization.

В `Infrastructure` нельзя класть:

- UI-логику;
- orchestration всего пользовательского сценария;
- правила, которые должны жить в Application;
- domain decisions, не связанные с интеграцией.

## 5. Entry points

Entry points принимают ввод и передают управление в application layer.

К ним относятся:

- CLI;
- Minimal API;
- встроенный Razor UI;
- external clients.

Entry points могут:

- читать аргументы команды;
- принимать HTTP request;
- валидировать форму;
- создавать application request;
- вызывать application service;
- маппить response в DTO;
- показывать результат пользователю.

Entry points не должны:

- выполнять retrieval;
- строить embeddings;
- генерировать prompt;
- обращаться к Qdrant напрямую;
- запускать Python helper напрямую;
- вызывать provider APIs напрямую.

## 6. External clients

Внешние клиенты находятся в `clients/`.

Примеры:

```text
clients/VideoRag.TelegramBot
clients/VideoRag.WebUi
```

Клиенты используют основной HTTP API:

```text
POST /ask
GET /health
```

Если есть общий контракт, клиенты используют:

```text
shared/VideoRag.Contracts
```

Правила для клиентов:

- клиент не содержит RAG-логику;
- клиент не обращается напрямую к Qdrant;
- клиент не запускает Python helper;
- клиент не строит embeddings;
- клиент не вызывает answer provider;
- клиент не читает transcript/registry files;
- адрес API задаётся через конфигурацию;
- ошибки API должны отображаться пользователю понятным сообщением.

При изменении внешнего API нужно синхронно обновить:

1. shared contracts;
2. mapper в основном API;
3. клиенты;
4. quick start, если меняется запуск или формат запроса.

## 7. Python helper

Python helper находится в:

```text
scripts/python-helper/
```

Он используется только для транскрибации.

Разрешено:

- читать input JSON;
- читать локальный видеофайл;
- запускать transcription provider;
- писать transcript JSON;
- писать output JSON;
- возвращать exit code.

Запрещено:

- делать chunking;
- строить embeddings;
- писать в Qdrant;
- выполнять retrieval;
- генерировать answer;
- поднимать HTTP service.

Если добавляется новый transcription provider, он должен сохранить совместимый JSON-контракт C# ↔ Python.

## 8. Provider changes

### Новый embedding provider

Куда класть:

```text
src/Infrastructure/Embeddings/
```

Что сделать:

1. Реализовать `IEmbeddingProvider`.
2. Добавить provider-specific options.
3. Зарегистрировать provider в DI.
4. Проверить размерность output vector.
5. Описать, когда нужен rebuild.
6. Добавить parsing/validation tests, если response provider сложный.

Важно:

- `EmbedAsync` и `EmbedBatchAsync` должны возвращать vectors одной размерности;
- пустой input должен обрабатываться предсказуемо;
- смена embedding provider или model требует rebuild;
- embeddings разных моделей нельзя смешивать в одной Qdrant collection.

### Новый answer provider

Куда класть:

```text
src/Infrastructure/Answers/
```

Что сделать:

1. Реализовать `IAnswerGenerator`.
2. Добавить provider-specific options.
3. Зарегистрировать provider в DI.
4. Сохранить grounded answer rules.
5. Обработать fallback.
6. Добавить parsing/validation tests, если response provider сложный.

Answer provider не должен выполнять retrieval, строить embeddings или обращаться к Qdrant.

## 9. Rebuild и embedding space

Rebuild используется для ручной пересборки индекса.

Rebuild нужен, если меняется:

- embedding provider;
- embedding model;
- vector size;
- semantic embedding space;
- chunking strategy, если старые chunks больше не соответствуют новой логике.

Если меняется только answer provider, rebuild не нужен.

Если меняется vector size, может потребоваться удалить старую Qdrant collection или volume.

## 10. Минимальные проверки перед merge

Перед merge желательно проверить:

```bash
dotnet build
```

Если есть tests:

```bash
dotnet test
```

Для основного сценария:

```bash
docker compose up -d
dotnet run -- ingest "data/videos/lecture_0.mp4"
dotnet run
```

Проверить:

```text
GET /health
POST /ask
```

Если менялись клиенты:

```bash
dotnet run --project .\clients\VideoRag.TelegramBot\VideoRag.TelegramBot.csproj
dotnet run --project .\clients\VideoRag.WebUi\VideoRag.WebUi.csproj
```

Если менялись embeddings или chunking, проверить `rebuild`.

## 11. Документация при изменениях

Обновить документацию нужно, если изменились:

- команды запуска;
- конфигурация;
- env-переменные;
- внешний HTTP API;
- shared contracts;
- структура проекта;
- provider defaults;
- правила rebuild;
- архитектурные границы.

Куда вносить изменения:

```text
docs/quick-start.md              запуск и команды
docs/troubleshooting.md          типовые ошибки
docs/team/architecture.md        архитектурные правила
docs/team/implementation-guide.md практические правила разработки
docs/llm/llm-context.md          устойчивый контекст для AI/LLM
docs/adr/                        новые архитектурные решения
```

## 12. Типовые ошибки проектирования

Избегать:

- переноса RAG-логики в UI или clients;
- прямого вызова Qdrant из API endpoint;
- прямого вызова provider SDK из PageModel;
- запуска Python helper из клиента;
- помещения infrastructure classes в Application;
- помещения interfaces в Domain;
- смешивания embeddings разных моделей;
- изменения embedding provider без rebuild;
- расширения Python helper до второго backend.

<!-- URL_AUDIO_INGEST_IMPLEMENTATION_GUIDE_START -->
## URL audio ingest: что проверять перед merge

Если менялся URL ingest, проверить:

```powershell
dotnet build
```

CLI:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-test-url-ingest-cli.ps1 -Url "https://rutube.ru/video/..."
```

API:

```powershell
dotnet run
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-test-url-ingest-api.ps1 -Url "https://rutube.ru/video/..."
```

Web UI:

```powershell
dotnet run --project .\clients\VideoRag.WebUi\VideoRag.WebUi.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-test-url-ingest-webui.ps1
```

Telegram bot:

```powershell
dotnet run --project .\clients\VideoRag.TelegramBot\VideoRag.TelegramBot.csproj
```

Проверить в Telegram:

```text
/health
/add <url>
/status <jobId>
```

Документация и test-cases:

```text
docs/test-cases/url-ingest-final-checklist.md
docs/test-cases/url-ingest-cli.md
docs/test-cases/url-ingest-razor-ui.md
docs/test-cases/url-ingest-telegram-bot.md
```
<!-- URL_AUDIO_INGEST_IMPLEMENTATION_GUIDE_END -->
