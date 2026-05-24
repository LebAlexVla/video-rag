# HTTP API — URL audio ingest

## POST `/ingest/url`

Запускает фоновую ingest job для публичной Rutube/VK ссылки.

Request:

```json
{
  "url": "https://rutube.ru/video/...",
  "lectureTitle": "Lecture title"
}
```

Response:

```json
{
  "jobId": "00000000-0000-0000-0000-000000000000",
  "status": "Queued",
  "statusUrl": "/ingest/jobs/00000000-0000-0000-0000-000000000000"
}
```

## GET `/ingest/jobs/{jobId}`

Возвращает состояние ingest job.

Running response:

```json
{
  "jobId": "00000000-0000-0000-0000-000000000000",
  "status": "Running",
  "stage": "Transcribing",
  "message": "Audio downloaded, transcription started",
  "lectureTitle": "Lecture title",
  "localAudioPath": "data/downloads/audio/file.mp3",
  "transcriptPath": null,
  "error": null
}
```

Succeeded response:

```json
{
  "jobId": "00000000-0000-0000-0000-000000000000",
  "status": "Succeeded",
  "stage": "Completed",
  "message": "Lecture ingest completed",
  "lectureTitle": "Lecture title",
  "localAudioPath": "data/downloads/audio/file.mp3",
  "transcriptPath": "data/transcripts/file.transcript.json",
  "error": null
}
```

Failed response:

```json
{
  "jobId": "00000000-0000-0000-0000-000000000000",
  "status": "Failed",
  "stage": "DownloadingAudio",
  "message": "URL ingest failed",
  "lectureTitle": "Lecture title",
  "localAudioPath": null,
  "transcriptPath": null,
  "error": "Download failed. Check that the video is public and available."
}
```

## Ограничения

- поддерживаются публичные Rutube/VK ссылки;
- приватные видео не поддерживаются;
- скачивается только аудио;
- job storage in-memory, поэтому после перезапуска API старые jobId недоступны.
