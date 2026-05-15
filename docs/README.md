# Documentation

Главная навигация по документации проекта Video Lecture RAG Assistant.

## Быстрый старт

- [Quick start](./quick-start.md)  
  Как установить зависимости, настроить providers, выполнить ingest, запустить API, UI и клиентов.

- [Troubleshooting](./troubleshooting.md)  
  Типовые ошибки локального запуска и способы исправления.

## Документы для разработки

- [Overview](./team/overview.md)  
  Краткое описание проекта, основных сценариев и компонентов.

- [Architecture](./team/architecture.md)  
  Подробная архитектура: роли компонентов, сценарии ingest / ask / rebuild, слои и зависимости.

- [Implementation guide](./team/implementation-guide.md)  
  Практические правила разработки: куда класть код, как добавлять providers и clients, что проверять перед merge.

## Контекст для AI/LLM

- [LLM context](./llm/llm-context.md)  
  Компактный технический контекст для AI/LLM-агентов, которые генерируют, проверяют или рефакторят код проекта.

## Архитектурные решения

- [ADR index](./adr/README.md)  
  Список зафиксированных архитектурных решений.

ADR объясняют, почему приняты ключевые решения. Они не заменяют quick start, architecture или implementation guide.

## Что читать по ситуации

### Нужно просто запустить проект

1. [Quick start](./quick-start.md)
2. [Troubleshooting](./troubleshooting.md), если запуск не проходит

### Нужно понять проект перед разработкой

1. [Overview](./team/overview.md)
2. [Architecture](./team/architecture.md)
3. [Implementation guide](./team/implementation-guide.md)

### Нужно изменить архитектуру

1. [Architecture](./team/architecture.md)
2. [ADR index](./adr/README.md)
3. [LLM context](./llm/llm-context.md)

Если изменение противоречит существующим ADR, нужно добавить новый ADR или обновить старый.

### Нужно работать с AI/LLM-агентом

Используй:

1. [LLM context](./llm/llm-context.md)
2. [Architecture](./team/architecture.md)
3. [Implementation guide](./team/implementation-guide.md)

`LLM context` должен быть первым файлом, который получает AI/LLM-агент.

## Основные части проекта

```text
src/                    основной C# код: Domain, Application, Infrastructure
Pages/                  встроенный Razor UI основного приложения
clients/                внешние клиенты
shared/                 общие DTO-контракты
scripts/python-helper/  Python helper для транскрибации
docs/                   документация
data/                   локальные данные проекта
```