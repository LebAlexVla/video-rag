# ADR

ADR фиксируют ключевые архитектурные решения проекта.

Это архив принятых решений, а не quick start и не руководство по реализации.

Для запуска проекта см.:

- [Quick start](../quick-start.md)
- [Troubleshooting](../troubleshooting.md)

Для разработки см.:

- [Overview](../team/overview.md)
- [Architecture](../team/architecture.md)
- [Implementation guide](../team/implementation-guide.md)

## Список ADR

- [ADR-001 — C# является центром системы](./ADR-001-csharp-is-the-core.md)
- [ADR-002 — Python используется как вспомогательный CLI для транскрибации](./ADR-002-python-helper-cli.md)
- [ADR-003 — MVP реализуется как локальный монолит, без микросервисов](./ADR-003-local-monolith-no-microservices.md)
- [ADR-004 — Связь C# и Python строится через запуск процесса и JSON-файл](./ADR-004-csharp-python-via-process-and-json.md)
- [ADR-005 — Qdrant используется как отдельное векторное хранилище](./ADR-005-qdrant-vector-store.md)
- [ADR-006 — Входом ingestion в MVP является локальный видеофайл](./ADR-006-local-file-ingestion.md)
- [ADR-007 — В MVP используются только приблизительные ссылки на место в лекции](./ADR-007-approximate-source-links-only.md)
- [ADR-008 — Повторная загрузка и дедупликация видео не входят в MVP](./ADR-008-no-dedup-in-mvp.md)
- [ADR-009 — Абстракции провайдеров сохраняются, но набор реализаций MVP ограничен](./ADR-009-provider-abstractions-limited.md)
- [ADR-010 — Первая версия запускается локально, а не в облаке](./ADR-010-local-first-run.md)
- [ADR-011 — Пересборка индекса выполняется вручную](./ADR-011-manual-rebuild.md)
- [ADR-012 — Razor Pages UI допускается как локальная test/demo панель](./ADR-012-razor-test-ui.md)

## Как использовать ADR

Перед архитектурным изменением нужно проверить, не противоречит ли оно существующим ADR.

Если изменение меняет одно из базовых решений проекта, нужно:

1. добавить новый ADR;
2. либо явно обновить существующий ADR;
3. затем синхронизировать `docs/team/architecture.md` и `docs/llm/llm-context.md`.