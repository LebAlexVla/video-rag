# URL audio ingest — Razor UI test cases

Проверяет сценарий добавления лекции по ссылке через Web UI client.

## Предусловия

В одном терминале запущен backend API:

```powershell
dotnet run
```

В другом терминале запущен Web UI client:

```powershell
dotnet run --project .\clients\VideoRag.WebUi\VideoRag.WebUi.csproj
```

Backend должен быть доступен на `http://localhost:5000`.

## UI-01 — страница открывается

Открыть Web UI в браузере.

Ожидаемый результат:

- страница загрузилась;
- виден статус API;
- видна форма вопроса;
- видна форма добавления лекции по ссылке Rutube/VK.

## UI-02 — добавление Rutube URL

Действия:

1. Вставить публичную Rutube-ссылку.
2. При необходимости указать название лекции.
3. Нажать кнопку добавления лекции.
4. Дождаться статуса job.

Ожидаемый результат:

- UI показывает созданный Job ID;
- статус меняется с `Queued`/`Running` на `Succeeded`;
- при успехе показано, что лекция добавлена.

## UI-03 — добавление VK URL

Повторить сценарий UI-02 для публичной VK-ссылки.

## UI-04 — ошибка неподдерживаемой ссылки

Вставить:

```text
https://example.com/video
```

Ожидаемый результат:

- UI показывает понятную ошибку;
- пользователь видит, что поддерживаются Rutube/VK;
- страница не ломается.

## UI-05 — вопрос после ingest

После успешного ingest задать вопрос в форме вопроса.

Ожидаемый результат:

- ответ приходит через `/ask`;
- sources содержат добавленную лекцию;
- обычный ask flow не сломан.

## Smoke pre-check

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-test-url-ingest-webui.ps1
```
