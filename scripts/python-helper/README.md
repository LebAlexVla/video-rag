# Python helper

CLI-helper для транскрибации видео в ingest flow.

## Что делает

- принимает `input.json` и `output.json` как positional args;
- валидирует входной JSON;
- транскрибирует локальный видеофайл;
- пишет `transcript.json` по `outputTranscriptPath`;
- пишет `output.json` со статусом `success` или `failed`;
- возвращает exit code по контракту проекта.

## Запуск вручную

```bash
python scripts/python-helper/main.py data/jobs/example.input.json data/jobs/example.output.json
```