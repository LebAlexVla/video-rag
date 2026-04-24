# ADR-012 — Razor Pages UI допускается как локальная test/demo панель

- **ID:** ADR-012
- **Статус:** Принято

## Контекст

Основной контракт MVP остаётся простым:

- CLI `ingest`;
- Minimal API `/ask`.

Этого достаточно для реализации и проверки основной RAG-логики. Однако для ручного тестирования вопросов, демонстрации результата и быстрой проверки параметров `TopK` / `MinScore` удобен простой локальный UI.

При этом отдельный frontend-проект увеличил бы сложность MVP:

- отдельная сборка;
- отдельные зависимости;
- отдельный dev server;
- дополнительная интеграция;
- риск смещения фокуса с RAG pipeline на frontend.

## Решение

Разрешить простой Razor Pages UI внутри основного C# приложения.

Razor UI используется только как локальная test/demo панель.

Он может:

- показывать форму вопроса;
- передавать вопрос в application layer;
- отображать answer / fallback;
- отображать sources;
- помогать вручную тестировать ask-сценарий.

Он не является основным контрактом MVP.

Основной контракт остаётся:

- CLI `ingest`;
- Minimal API `/ask`.

## Правила реализации

Razor PageModel может зависеть от:

- `IAskService`.

Razor UI не должен напрямую зависеть от:

- Qdrant;
- Ollama;
- OpenAI;
- Python helper;
- concrete infrastructure implementations;
- provider SDK;
- файлов transcript/registry.

Razor UI не должен содержать:

- retrieval logic;
- embedding logic;
- prompt generation;
- answer generation;
- ingest/rebuild orchestration.

## Последствия

Плюсы:

- проще вручную тестировать ask-сценарий;
- не нужен отдельный frontend-проект;
- не появляется новый backend;
- не нарушается локальная монолитная архитектура;
- Minimal API `/ask` остаётся доступным и обязательным.

Минусы:

- появляется дополнительный UI-слой;
- нужно явно следить, чтобы Razor UI не начал содержать application или infrastructure logic.

## Что это значит для реализации

Разрешено:

- хранить Razor Pages в `/Pages`;
- использовать Razor UI для локального тестирования;
- вызывать из PageModel только application services;
- показывать answer, fallback и sources.

Запрещено:

- считать Razor UI основным продуктовым интерфейсом MVP;
- заменять Minimal API `/ask` Razor-страницами;
- обращаться из Razor UI напрямую к Qdrant или LLM providers;
- добавлять отдельный frontend-проект без нового архитектурного решения.

Откладывается:

- полноценный web UI;
- отдельный frontend;
- auth;
- история чатов;
- пользовательские кабинеты;
- production UI.

## Какие варианты были отвергнуты

- Не делать никакого UI и тестировать только через HTTP-запросы.
- Добавлять отдельный frontend-проект.
- Делать Razor UI основным интерфейсом MVP.