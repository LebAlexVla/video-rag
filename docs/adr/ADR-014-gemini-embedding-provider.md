# ADR-014 — Google Gemini используется как облачный провайдер embeddings

- **ID:** ADR-014
- **Статус:** Принято

## Контекст

После перехода на DeepSeek для answer generation (ADR-013) embeddings оставались на локальном Ollama. Это противоречило задаче полного перехода с локальных моделей на облачные API.

DeepSeek не предоставляет endpoint для embeddings — только chat completions. Потребовался отдельный облачный embedding-провайдер.

## Решение

Google Gemini API используется как провайдер для построения embeddings (`IEmbeddingProvider`).

Модель: `gemini-embedding-001`.

Ключевые характеристики:
- поддерживает task types: `RETRIEVAL_QUERY` (для вопросов) и `RETRIEVAL_DOCUMENT` (для документов);
- размерность выходного вектора: 768, совпадает с `Qdrant.VectorSize`;
- поддерживает batch endpoint (`batchEmbedContents`).

Реализован новый `GeminiEmbeddingProvider` в `src/Infrastructure/Embeddings/`. Использует REST API Google Generative Language API (`/v1beta/models/{model}:embedContent` и `batchEmbedContents`). Аутентификация — через API key в query string (`?key=...`), что является стандартным для Google API.

Конфигурация в `appsettings.json`:

```json
"Embeddings": {
  "Provider": "gemini",
  "Gemini": {
    "BaseUrl": "https://generativelanguage.googleapis.com",
    "ApiKey": "",
    "Model": "gemini-embedding-001",
    "OutputDimensionality": 768
  }
}
```

API-ключ не хранится в `appsettings.json`. Передаётся через переменную окружения `GOOGLE_API_TOKEN` в файле `.env` (gitignored). При старте приложение читает `.env` и маппит токен в конфигурацию.

## Task types

`GeminiEmbeddingProvider` использует task types для улучшения качества RAG:

- `EmbedAsync` (вопрос пользователя) → `RETRIEVAL_QUERY`;
- `EmbedBatchAsync` (chunks лекций при ingest) → `RETRIEVAL_DOCUMENT`.

Это обязательное условие для корректного поиска: вопрос и документы должны встраиваться в совместимые embedding-пространства.

## Последствия

- Embeddings полностью переведены с локального Ollama на облачный Google Gemini API.
- Ollama больше не требуется для работы системы.
- Размерность 768 совпадает с существующей Qdrant collection — rebuild не требуется при первом запуске.
- При смене модели или провайдера embeddings требуется rebuild (ADR-011).

## Что это значит для реализации

Разрешено:
- использовать `gemini` как значение `Embeddings.Provider`;
- хранить API-ключ только в `.env` (не в `appsettings.json`).

Запрещено:
- коммитить `.env` в репозиторий;
- вызывать Gemini API в обход `IEmbeddingProvider`;
- смешивать embeddings от разных моделей в одной Qdrant collection.

Откладывается:
- переход на `gemini-embedding-2` (другое embedding-пространство, требует rebuild и обновлённого форматирования запросов).

## Какие варианты были отвергнуты

- OpenAI embeddings API (`text-embedding-3-small`) — уже реализован в проекте, но требует отдельного OpenAI-ключа.
- Оставить Ollama для embeddings — противоречит задаче перехода на облачные модели.
