#!/usr/bin/env python3
from __future__ import annotations

import json
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from faster_whisper import WhisperModel


EXIT_SUCCESS = 0
EXIT_PROCESSING_FAILED = 1
EXIT_INPUT_VALIDATION_FAILED = 2
EXIT_SOURCE_NOT_FOUND = 3
EXIT_INTERNAL_ERROR = 10


@dataclass(frozen=True)
class InputPayload:
    job_id: str
    input_video_path: str
    output_transcript_path: str
    transcription_provider: str
    transcription_model: str
    overwrite: bool
    requested_title: str | None
    language_hint: str | None


class InputValidationError(Exception):
    pass


def main() -> int:
    try:
        input_json_path, output_json_path = parse_args(sys.argv)
        payload = read_and_validate_input(input_json_path)

        source_path = Path(payload.input_video_path).resolve()
        if not source_path.exists() or not source_path.is_file():
            return fail(
                output_json_path=output_json_path,
                job_id=payload.job_id,
                exit_code=EXIT_SOURCE_NOT_FOUND,
                code="source_not_found",
                message="Input video file was not found or is not accessible.",
                details={"inputVideoPath": str(source_path)},
            )

        transcript_path = Path(payload.output_transcript_path).resolve()
        transcript_path.parent.mkdir(parents=True, exist_ok=True)

        if transcript_path.exists() and not payload.overwrite:
            return fail(
                output_json_path=output_json_path,
                job_id=payload.job_id,
                exit_code=EXIT_INPUT_VALIDATION_FAILED,
                code="transcript_exists",
                message="Output transcript already exists and overwrite is false.",
                details={"outputTranscriptPath": str(transcript_path)},
            )

        if payload.transcription_provider.strip().lower() != "faster-whisper":
            return fail(
                output_json_path=output_json_path,
                job_id=payload.job_id,
                exit_code=EXIT_INPUT_VALIDATION_FAILED,
                code="unsupported_transcription_provider",
                message="Only faster-whisper is supported by this helper.",
                details={"transcriptionProvider": payload.transcription_provider},
            )

        transcript_json = transcribe_with_faster_whisper(payload, source_path)
        write_json(transcript_path, transcript_json)

        output_json = {
            "jobId": payload.job_id,
            "status": "success",
        }
        write_json(output_json_path, output_json)

        return EXIT_SUCCESS

    except InputValidationError as ex:
        output_path = try_get_output_path(sys.argv)
        if output_path is not None:
            write_safe_error_output(
                output_json_path=output_path,
                job_id=try_get_job_id_from_input_arg(sys.argv),
                code="input_validation_failed",
                message=str(ex),
            )
        return EXIT_INPUT_VALIDATION_FAILED

    except FileNotFoundError as ex:
        output_path = try_get_output_path(sys.argv)
        if output_path is not None:
            write_safe_error_output(
                output_json_path=output_path,
                job_id=try_get_job_id_from_input_arg(sys.argv),
                code="file_not_found",
                message=str(ex),
            )
        return EXIT_SOURCE_NOT_FOUND

    except Exception as ex:
        output_path = try_get_output_path(sys.argv)
        if output_path is not None:
            write_safe_error_output(
                output_json_path=output_path,
                job_id=try_get_job_id_from_input_arg(sys.argv),
                code="transcription_failed",
                message="Failed to transcribe input video.",
                details={
                    "exceptionType": type(ex).__name__,
                    "exceptionMessage": str(ex),
                },
            )
        return EXIT_PROCESSING_FAILED


def transcribe_with_faster_whisper(payload: InputPayload, source_path: Path) -> dict[str, Any]:
    language = payload.language_hint
    model_name = payload.transcription_model.strip()

    model = WhisperModel(
        model_size_or_path=model_name,
        device="cpu",
        compute_type="int8",
    )

    segments_iter, info = model.transcribe(
        str(source_path),
        language=language,
        vad_filter=True,
    )

    segments: list[dict[str, Any]] = []

    for index, segment in enumerate(segments_iter):
        text = (segment.text or "").strip()
        if not text:
            continue

        start_sec = float(segment.start)
        end_sec = float(segment.end)

        if end_sec <= start_sec:
            continue

        segments.append(
            {
                "index": len(segments),
                "startSec": start_sec,
                "endSec": end_sec,
                "text": text,
            }
        )

    if not segments:
        raise RuntimeError("Transcription produced no valid segments.")

    title = payload.requested_title or source_path.stem

    return {
        "jobId": payload.job_id,
        "status": "success",
        "lecture": {
            "title": title,
            "sourceFileName": source_path.name,
            "sourcePath": str(source_path),
            "language": info.language if getattr(info, "language", None) else (language or "unknown"),
            "durationSec": segments[-1]["endSec"],
        },
        "transcriber": {
            "provider": payload.transcription_provider,
            "model": payload.transcription_model,
        },
        "segments": segments,
    }


def parse_args(argv: list[str]) -> tuple[Path, Path]:
    if len(argv) != 3:
        raise InputValidationError(
            "Usage: python main.py <input-json-path> <output-json-path>"
        )

    input_json_path = Path(argv[1]).resolve()
    output_json_path = Path(argv[2]).resolve()
    output_json_path.parent.mkdir(parents=True, exist_ok=True)

    return input_json_path, output_json_path


def read_and_validate_input(input_json_path: Path) -> InputPayload:
    if not input_json_path.exists() or not input_json_path.is_file():
        raise FileNotFoundError(f"Input JSON file was not found: {input_json_path}")

    try:
        raw = json.loads(input_json_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as ex:
        raise InputValidationError(f"Input JSON is invalid: {ex}") from ex

    if not isinstance(raw, dict):
        raise InputValidationError("Input JSON root must be an object.")

    job_id = require_non_empty_string(raw, "jobId")
    input_video_path = require_non_empty_string(raw, "inputVideoPath")
    output_transcript_path = require_non_empty_string(raw, "outputTranscriptPath")
    transcription_provider = require_non_empty_string(raw, "transcriptionProvider")
    transcription_model = require_non_empty_string(raw, "transcriptionModel")
    overwrite = require_bool(raw, "overwrite")

    requested_title = optional_string(raw, "requestedTitle")
    language_hint = optional_string(raw, "languageHint")

    return InputPayload(
        job_id=job_id,
        input_video_path=input_video_path,
        output_transcript_path=output_transcript_path,
        transcription_provider=transcription_provider,
        transcription_model=transcription_model,
        overwrite=overwrite,
        requested_title=requested_title,
        language_hint=language_hint,
    )


def require_non_empty_string(data: dict[str, Any], key: str) -> str:
    value = data.get(key)

    if not isinstance(value, str) or not value.strip():
        raise InputValidationError(f"Field '{key}' is required and must be a non-empty string.")

    return value.strip()


def require_bool(data: dict[str, Any], key: str) -> bool:
    value = data.get(key)

    if not isinstance(value, bool):
        raise InputValidationError(f"Field '{key}' is required and must be boolean.")

    return value


def optional_string(data: dict[str, Any], key: str) -> str | None:
    value = data.get(key)

    if value is None:
        return None

    if not isinstance(value, str):
        raise InputValidationError(f"Field '{key}' must be a string when provided.")

    normalized = value.strip()
    return normalized or None


def fail(
    output_json_path: Path,
    job_id: str,
    exit_code: int,
    code: str,
    message: str,
    details: dict[str, Any] | None = None,
) -> int:
    output = {
        "jobId": job_id,
        "status": "failed",
        "error": {
            "code": code,
            "message": message,
        },
    }

    if details:
        output["error"]["details"] = details

    write_json(output_json_path, output)
    return exit_code


def write_safe_error_output(
    output_json_path: Path,
    job_id: str | None,
    code: str,
    message: str,
    details: dict[str, Any] | None = None,
) -> None:
    payload: dict[str, Any] = {
        "jobId": job_id or "unknown-job",
        "status": "failed",
        "error": {
            "code": code,
            "message": message,
        },
    }

    if details:
        payload["error"]["details"] = details

    try:
        write_json(output_json_path, payload)
    except Exception:
        pass


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def try_get_output_path(argv: list[str]) -> Path | None:
    if len(argv) >= 3:
        return Path(argv[2]).resolve()
    return None


def try_get_job_id_from_input_arg(argv: list[str]) -> str | None:
    if len(argv) < 2:
        return None

    input_json_path = Path(argv[1]).resolve()
    if not input_json_path.exists() or not input_json_path.is_file():
        return None

    try:
        raw = json.loads(input_json_path.read_text(encoding="utf-8"))
    except Exception:
        return None

    job_id = raw.get("jobId")
    if isinstance(job_id, str) and job_id.strip():
        return job_id.strip()

    return None


if __name__ == "__main__":
    sys.exit(main())