# Python helper

Минимальный stub helper для первого end-to-end ingest flow.

## Что делает
- принимает `input.json` и `output.json` как positional args;
- валидирует входной JSON;
- пишет `transcript.json` по `outputTranscriptPath`;
- пишет `output.json` со статусом `success` или `failed`;
- возвращает exit code по контракту MVP.

## Запуск вручную

```bash
python scripts/python-helper/main.py data/jobs/example.input.json data/jobs/example.output.json