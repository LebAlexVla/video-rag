# Architecture

## 1. Архитектурная идея

Архитектура MVP намеренно упрощена. Система строится вокруг одного основного C# приложения, которое управляет всеми сценариями: ingest, ask и rebuild. Python используется не как второй backend, а как узкий вспомогательный инструмент для транскрибации. Qdrant используется только как vector store. Всё запускается локально, без внутренней сетевой инфраструктуры между частями системы.

Цель такой архитектуры — сократить интеграционные риски и сделать систему достаточно простой для команды студентов, не жертвуя базовой расширяемостью.

---

## 2. Простая схема всей системы

```text
                 +----------------------+
                 |      Пользователь    |
                 |   ask / ingest /     |
                 |      rebuild         |
                 +----------+-----------+
                            |
                            v
                 +----------------------+
                 |     C# приложение    |
                 |----------------------|
                 | CLI / API / services |
                 | ask / ingest /       |
                 | rebuild orchestration|
                 +----+------------+----+
                      |            |
                      |            v
                      |      +-----------+
                      |      |  Qdrant   |
                      |      +-----------+
                      |
                      v
              +-------------------+
              | Python CLI helper |
              |-------------------|
              | transcription     |
              | -> transcript.json|
              +---------+---------+
                        |
                        v
                 +-------------+
                 | local files |
                 +-------------+
```

---

## 3. Роли основных частей системы

## 3.1 C# приложение

Это центральная часть системы. Оно отвечает за:

- команды и API;
- orchestration ingest;
- orchestration ask;
- orchestration rebuild;
- чтение `transcript.json`;
- chunking;
- построение embeddings;
- запись и поиск в Qdrant;
- генерацию ответа;
- прикладную валидацию и обработку ошибок.

Ключевое правило: **вся основная логика системы находится в C#**.

---

## 3.2 Python helper

Это отдельный CLI-скрипт, который запускается из C# как внешний процесс.

Он отвечает только за:
- чтение локального видео;
- транскрибацию;
- запись `transcript.json`;
- возврат кода завершения.

Python helper не отвечает за:
- chunking;
- embeddings;
- Qdrant;
- retrieval;
- answer generation;
- API;
- webhook.

Главная граница MVP: **Python только готовит транскрипт, C# делает всё остальное**.

---

## 3.3 Qdrant

Qdrant используется как отдельное векторное хранилище.

Он хранит:
- embeddings чанков;
- текст чанков;
- метаданные лекции и чанка.

Он не используется как:
- база пользователей;
- job storage;
- chat history storage;
- общая операционная БД.

---

## 3.4 Файловая система

Файловая система используется для:
- исходных видео;
- входных JSON для Python helper;
- выходных `transcript.json`;
- конфигурации;
- временных файлов.

---

## 4. Ingest flow

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

### Что здесь важно

1. Администратор передаёт путь к локальному видео.
2. C# нормализует источник и запускает helper.
3. Python helper пишет `transcript.json`.
4. C# валидирует JSON и переводит его в доменную модель.
5. Chunking выполняется только в C#.
6. Построение embeddings выполняется только в C#.
7. Запись в Qdrant выполняется только из C#.

### Практический смысл

Если в реализации chunking окажется в Python, это уже отклонение от канонической архитектуры.

---

## 5. Ask flow

```text
User
 -> Minimal API /ask
 -> IAskService.AskAsync
 -> IContextRetriever.RetrieveAsync
 -> IEmbeddingProvider.EmbedAsync(question)
 -> IVectorStore.SearchAsync
 -> IAnswerGenerator.GenerateAsync
 -> AskResponse
```

### Что здесь важно

1. Вопрос валидируется на входе.
2. Для вопроса строится embedding.
3. Выполняется similarity search в Qdrant.
4. Применяются `TopK` и `MinScore`.
5. Если подходящего контекста нет или найденный контекст не проходит порог достаточной релевантности, система возвращает fallback.
6. Только если контекст признан достаточным, генератор ответа формирует итоговый ответ.

### Где что выполняется

- embedding вопроса — C#;
- retrieval — C#;
- answer generation — C#;
- Python в ask flow не участвует.

---

## 6. Rebuild flow

```text
Admin
 -> CLI rebuild
 -> ILectureIngestService.RebuildAsync
 -> IVectorStore.ClearAsync
 -> повторное чтение сохранённых transcript.json
 -> IChunker.Chunk
 -> IEmbeddingProvider.EmbedBatchAsync
 -> IVectorStore.UpsertLectureChunksAsync
```

### Когда rebuild нужен

- изменился provider embeddings;
- изменилась модель embeddings;
- изменилась размерность embeddings;
- индекс нужно очистить после ошибок или повторного ingest.

### Почему rebuild ручной

Для MVP объём данных маленький. Проще и надёжнее дать админу явную операцию rebuild, чем строить автоматическое переиндексирование.

### Источник данных для rebuild

В MVP rebuild выполняется на основе уже сохранённых `transcript.json`.

Это означает:
- rebuild не обязан повторно запускать Python helper;
- rebuild повторно читает сохранённые транскрипты, заново выполняет chunking, строит embeddings и записывает индекс;
- если сохранённых `transcript.json` нет, rebuild для такой лекции считается недоступным без повторного ingest.

### Что должно быть сохранено после ingest

Чтобы rebuild был реально выполним, после успешного ingest C# должен сохранить:
- `transcript.json`;
- стабильный `lectureId`;
- минимальную metadata-запись или manifest/registry, из которого видно:
  - какая лекция существует;
  - где лежит её `transcript.json`;
  - какое имя лекции использовать при повторной индексации.

В MVP формат этого registry может быть простым локальным JSON-файлом. Отдельная база для этого не требуется.

---

## 7. Граница C# ↔ Python

Это самая важная граница в системе.

### Разрешено

- запуск Python как внешнего процесса;
- передача входного JSON;
- чтение `transcript.json`;
- анализ exit code;
- логирование stderr/stdout для диагностики.

### Запрещено

- внутренний REST;
- gRPC;
- очереди;
- webhooks;
- запись в Qdrant из Python;
- chunking в Python;
- ответ пользователю из Python.

### Практическое правило

Если для интеграции C# и Python требуется сетевое взаимодействие, значит реализация ушла от MVP-архитектуры.

---

## 8. Где выполняется логика

| Зона | Где находится |
|---|---|
| Нормализация источника | C# (`IVideoSource`) |
| Запуск транскрибации | C# вызывает Python (`ITranscriptionRunner`) |
| Транскрибация | Python helper |
| Чтение transcript JSON | C# (`ITranscriptReader`) |
| Chunking | C# (`IChunker`) |
| Построение embeddings | C# (`IEmbeddingProvider`) |
| Запись в Qdrant | C# (`IVectorStore`) |
| Retrieval | C# (`IContextRetriever`) |
| Генерация ответа | C# (`IAnswerGenerator`) |

---

## 9. Правила зависимостей

### Слои внутри C# приложения

```text
Domain
  - предметные сущности и инварианты

Application/Abstractions
  - интерфейсы

Application/Contracts
  - DTO и прикладные контракты

Application/Services
  - orchestration сценариев

Infrastructure
  - реализации интерфейсов и адаптеры
```

### Разрешённые зависимости

- `Application -> Domain`
- `Infrastructure -> Application`
- `Infrastructure -> Domain`