# Video Lecture RAG Assistant

Локальная MVP-система для вопросов по видеолекциям.

Система принимает локальный видеофайл, создаёт транскрипт, индексирует фрагменты лекции и позволяет задавать вопросы по загруженным материалам. Ответ формируется только на основе найденного контекста и возвращается с источниками.

## Документация

Главная навигация по документации:

- [Docs](docs/README.md)

Основные документы:

- [Quick start](docs/quick-start.md)
- [Architecture](docs/team/architecture.md)
- [Implementation guide](docs/team/implementation-guide.md)
- [LLM context](docs/llm/llm-context.md)
- [ADR](docs/adr/README.md)

## Минимальный запуск

Подробная инструкция находится в [docs/quick-start.md](docs/quick-start.md).

Коротко:

```bash
docker compose up -d
```

```bash
pip install -r scripts/python-helper/requirements.txt
```

```bash
dotnet run -- ingest "data/videos/lecture_0.mp4"
```

```bash
dotnet run
```

После запуска:

```text
http://localhost:5000
```

## Структура

```text
src/                    основной C# код
Pages/                  локальный Razor UI для тестирования
scripts/python-helper/  Python helper для транскрибации
docs/                   документация проекта
data/                   локальные данные проекта
```