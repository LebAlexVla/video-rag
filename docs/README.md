# Documentation

Главная навигация по документации проекта.

Документация разделена на четыре группы:

- быстрый запуск и решение типовых проблем;
- документы для команды разработки;
- контекст для LLM / AI-agent;
- ADR с принятыми архитектурными решениями.

## Что читать

### Хочу просто запустить проект

1. [Quick start](./quick-start.md)
2. [Troubleshooting](./troubleshooting.md), если запуск не проходит

### Я новый разработчик в команде

1. [Overview](./team/overview.md)
2. [Architecture](./team/architecture.md)
3. [Implementation guide](./team/implementation-guide.md)
4. [ADR](./adr/README.md), если нужно понять причины решений

### Я меняю архитектуру

1. [Architecture](./team/architecture.md)
2. [LLM context](./llm/llm-context.md)
3. [ADR](./adr/README.md)

Перед изменением архитектурных решений нужно проверить, не противоречит ли изменение существующим ADR.

### Я использую LLM / AI-agent для генерации кода

Используй основной технический контекст:

- [LLM context](./llm/llm-context.md)

Этот документ должен быть главным источником ограничений для генерации кода.

## Быстрый запуск и диагностика

- [Quick start](./quick-start.md)  
  Что установить и как локально запустить проект.

- [Troubleshooting](./troubleshooting.md)  
  Типовые ошибки запуска и способы исправления.

## Документы для команды

- [Overview](./team/overview.md)  
  Кратко: что делает проект, что входит в MVP, что не входит и какой результат считается готовым.

- [Architecture](./team/architecture.md)  
  Архитектура системы, основные компоненты, сценарии ingest / ask / rebuild и правила зависимостей.

- [Implementation guide](./team/implementation-guide.md)  
  Практические правила разработки: куда класть код, как расширять систему и чего не делать.

## Контекст для LLM

- [LLM context](./llm/llm-context.md)  
  Канонический технический контекст для AI/LLM-агентов.

## ADR

- [ADR index](./adr/README.md)  
  Список принятых архитектурных решений.

ADR фиксируют причины ключевых решений. Они не заменяют quick start, architecture или implementation guide.
