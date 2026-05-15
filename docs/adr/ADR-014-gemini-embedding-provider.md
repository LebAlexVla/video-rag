# ADR-014 — Google Gemini используется как cloud/API provider для embeddings

- **Статус:** Принято

## Контекст

После перехода answer generation на cloud/API режим embeddings тоже переводятся на внешний provider, чтобы снизить локальную нагрузку и улучшить качество retrieval.

DeepSeek не предоставляет embedding-модель, поэтому нужен отдельный embedding provider.

## Решение

Использовать Google Gemini как основной provider для `IEmbeddingProvider`.

Модель по умолчанию: `gemini-embedding-001`.

Настроенная размерность: `768`, чтобы соответствовать `Qdrant.VectorSize`.

Для RAG используются task types:
- пользовательский вопрос: `RETRIEVAL_QUERY`;
- chunks лекций: `RETRIEVAL_DOCUMENT`.

Ollama embedding provider не удаляется и остаётся локальным fallback-режимом.

## Последствия

Плюсы:
- embeddings строятся без нагрузки на локальную машину;
- можно использовать специализированные retrieval task types;
- provider остаётся за интерфейсом `IEmbeddingProvider`.

Минусы:
- нужен Gemini API key;
- нужен интернет;
- возможны rate limits;
- при смене embedding provider нужен rebuild индекса.

## Важно про rebuild

Совпадение размерности вектора не означает совместимость embedding space.

Если Qdrant collection уже содержит vectors, построенные через другого provider, например Ollama, индекс нужно пересобрать.

Нельзя смешивать embeddings от Gemini и Ollama в одной collection.

## Ограничения

Gemini используется только для embeddings.

Запрещено:
- вызывать Gemini API в обход `IEmbeddingProvider`;
- смешивать embeddings от разных моделей в одной Qdrant collection;
- менять embedding provider без rebuild индекса.
