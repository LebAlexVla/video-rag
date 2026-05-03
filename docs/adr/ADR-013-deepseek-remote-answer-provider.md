# ADR-013 — DeepSeek используется как удалённый провайдер генерации ответа

- **ID:** ADR-013
- **Статус:** Принято

## Контекст

Ранее генерация ответа выполнялась через локальную модель Ollama.

Локальный inference на ноутбуке или ПК уступает по качеству и скорости облачным LLM, запущенным на серверном GPU. Качество ответов критично для RAG-сценария: модель должна строго следовать контексту и избегать галлюцинаций.

Было принято решение перейти на удалённый API-провайдер с подходящим соотношением цены и качества.

## Решение

DeepSeek API используется как провайдер для генерации ответа (`IAnswerGenerator`).

DeepSeek API совместим с форматом OpenAI (`/v1/chat/completions`). Существующий `OpenAiAnswerGenerator` переиспользуется без изменений — DeepSeek настраивается через отдельный `HttpClient` с `BaseAddress = https://api.deepseek.com`.

Конфигурация в `appsettings.json`:

```json
"Answers": {
  "Provider": "deepseek",
  "DeepSeek": {
    "BaseUrl": "https://api.deepseek.com",
    "ApiKey": "",
    "Model": "deepseek-chat"
  }
}
```

API-ключ не хранится в `appsettings.json`. Он передаётся через переменную окружения `DEEPSEEK_API_TOKEN` в файле `.env` (gitignored). При старте приложение читает `.env` и маппит токен в конфигурацию.

## Модели DeepSeek

- `deepseek-chat` — алиас для `deepseek-v4-flash`, рекомендуется для answer generation;
- `deepseek-reasoner` — алиас для `deepseek-v4-flash` в thinking-режиме (нецелесообразен для RAG).

## Embeddings

DeepSeek API не предоставляет модели для построения embeddings. Embedding provider настраивается независимо — см. ADR-014, где он заменён на Google Gemini.

## Последствия

- Качество ответов улучшается: облачная LLM обходит локальный inference.
- Стоимость: тарифицируется по токенам (см. https://api-docs.deepseek.com/quick_start/pricing).
- Зависимость от интернет-соединения при генерации ответа.
- Embedding provider остаётся независимым от answer provider.

## Что это значит для реализации

Разрешено:
- использовать `deepseek` как значение `Answers.Provider`;
- переиспользовать `OpenAiAnswerGenerator` с DeepSeek base URL;
- хранить API-ключ только в `.env` (не в `appsettings.json`).

Запрещено:
- коммитить `.env` в репозиторий;
- вызывать DeepSeek API в обход `IAnswerGenerator`;
- выполнять retrieval или embeddings в DeepSeek-слое.

Откладывается:
- оценка streaming-режима DeepSeek.

## Какие варианты были отвергнуты

- Оставить Ollama как основной answer provider (качество модели ограничено железом).
- OpenAI API (DeepSeek предложил аналогичный API с более низкой ценой).
