#!/usr/bin/env python3
from __future__ import annotations

import concurrent.futures
import json
import math
import os
import re
import subprocess
import sys
import tempfile
import threading
import time
import unicodedata
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from faster_whisper import WhisperModel


EXIT_SUCCESS = 0
EXIT_PROCESSING_FAILED = 1
EXIT_INPUT_VALIDATION_FAILED = 2
EXIT_SOURCE_NOT_FOUND = 3
EXIT_INTERNAL_ERROR = 10

DEFAULT_OVERLAP_SEC = 2.0
MAX_WORKERS_CAP = 12


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
    chunk_time_sec: float | None


@dataclass(frozen=True)
class ChunkSpec:
    index: int
    core_start_sec: float
    core_end_sec: float
    extract_start_sec: float
    extract_end_sec: float
    is_last: bool


@dataclass(frozen=True)
class TranscribedChunk:
    chunk: ChunkSpec
    segments: list[dict[str, Any]]
    detected_language: str | None


class InputValidationError(Exception):
    pass


_THREAD_LOCAL = threading.local()

def parse_float(value: Any) -> float | None:
    try:
        if value is None:
            return None
        return float(value)
    except (TypeError, ValueError):
        return None

def extract_duration_from_ffprobe(raw: dict[str, Any]) -> float | None:
    if not isinstance(raw, dict):
        return None

    fmt = raw.get("format")
    if isinstance(fmt, dict):
        duration = parse_float(fmt.get("duration"))
        if duration is not None and duration > 0:
            return duration

    streams = raw.get("streams")
    if isinstance(streams, list):
        for stream in streams:
            if not isinstance(stream, dict):
                continue
            duration = parse_float(stream.get("duration"))
            if duration is not None and duration > 0:
                return duration

    return None

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

        start_time = time.time()
        transcript_json = transcribe_with_faster_whisper(payload, source_path)
        print(f"Transcription completed in {time.time() - start_time:.2f} seconds.")
        write_json(transcript_path, transcript_json)
        print(f"Transcript saved to: {transcript_path}")

        output_json = {"jobId": payload.job_id, "status": "success"}
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
    model_name = payload.transcription_model.strip()
    language = payload.language_hint

    if payload.chunk_time_sec is None:
        return transcribe_single_pass(
            payload=payload,
            source_path=source_path,
            model_name=model_name,
            language=language,
        )

    return transcribe_chunked(
        payload=payload,
        source_path=source_path,
        model_name=model_name,
        language=language,
        chunk_time_sec=payload.chunk_time_sec,
    )


def transcribe_single_pass(
    payload: InputPayload,
    source_path: Path,
    model_name: str,
    language: str | None,
) -> dict[str, Any]:
    duration_sec = probe_media_duration(source_path)
    model = create_whisper_model(model_name, cpu_threads=4)

    print("Before transcribe")
    segments_iter, info = model.transcribe(
        str(source_path),
        language=language,
        vad_filter=True,
    )
    print("After transcribe")

    segments = normalize_and_pack_segments(segments_iter, offset_sec=0.0)
    if not segments:
        raise RuntimeError("Transcription produced no valid segments.")

    title = payload.requested_title or source_path.stem
    detected_language = getattr(info, "language", None) if info is not None else None

    return build_transcript_payload(
        payload=payload,
        source_path=source_path,
        title=title,
        language=detect_language(detected_language if detected_language else language),
        duration_sec=duration_sec,
        segments=segments,
    )

def build_chunk_specs(
    duration_sec: float,
    chunk_time_sec: float,
    overlap_sec: float,
) -> list[ChunkSpec]:
    chunks: list[ChunkSpec] = []

    if duration_sec <= 0:
        return chunks

    index = 0
    start = 0.0

    while start < duration_sec:
        core_start = start
        core_end = min(start + chunk_time_sec, duration_sec)

        extract_start = max(0.0, core_start - overlap_sec)
        extract_end = min(duration_sec, core_end + overlap_sec)

        is_last = core_end >= duration_sec

        chunks.append(
            ChunkSpec(
                index=index,
                core_start_sec=core_start,
                core_end_sec=core_end,
                extract_start_sec=extract_start,
                extract_end_sec=extract_end,
                is_last=is_last,
            )
        )

        index += 1
        start += chunk_time_sec

    return chunks

def transcribe_chunked(
    payload: InputPayload,
    source_path: Path,
    model_name: str,
    language: str | None,
    chunk_time_sec: float,
) -> dict[str, Any]:
    if chunk_time_sec <= 0:
        raise InputValidationError("Field 'chunkTime' must be greater than 0.")

    duration_sec = probe_media_duration(source_path)
    overlap_sec = min(DEFAULT_OVERLAP_SEC, chunk_time_sec * 0.1)
    chunks = build_chunk_specs(duration_sec, chunk_time_sec, overlap_sec)
    if not chunks:
        raise RuntimeError("No chunks were generated for transcription.")

    worker_count = min(os.cpu_count() or 1, len(chunks), MAX_WORKERS_CAP)
    print(
        f"Chunked transcription enabled: duration={duration_sec:.2f}s, "
        f"chunkTime={chunk_time_sec:.2f}s, overlap={overlap_sec:.2f}s, workers={worker_count}."
    )

    with tempfile.TemporaryDirectory(prefix="transcribe_chunks_") as tmpdir:
        tmpdir_path = Path(tmpdir)
        full_audio_path = extract_full_audio(source_path, tmpdir_path)

        with concurrent.futures.ThreadPoolExecutor(max_workers=worker_count) as executor:
            futures = [
                executor.submit(
                    transcribe_chunk_from_audio,
                    full_audio_path,
                    tmpdir_path,
                    model_name,
                    language,
                    chunk,
                )
                for chunk in chunks
            ]
            results = [future.result() for future in concurrent.futures.as_completed(futures)]

    results.sort(key=lambda item: item.chunk.index)

    all_segments: list[dict[str, Any]] = []
    detected_languages: list[str] = []
    for result in results:
        if result.detected_language:
            detected_languages.append(result.detected_language)
        all_segments.extend(result.segments)

    merged_segments = merge_and_dedupe_segments(all_segments, chunk_overlap_sec=overlap_sec)
    if not merged_segments:
        raise RuntimeError("Transcription produced no valid segments.")

    title = payload.requested_title or source_path.stem
    language_value = detect_language(detected_languages[0]) if detected_languages else detect_language(language)

    return build_transcript_payload(
        payload=payload,
        source_path=source_path,
        title=title,
        language=language_value,
        duration_sec=duration_sec,
        segments=merged_segments,
    )


def transcribe_chunk_from_audio(
    full_audio_path: Path,
    tmpdir_path: Path,
    model_name: str,
    language: str | None,
    chunk: ChunkSpec,
) -> TranscribedChunk:
    chunk_audio_path = extract_audio_chunk(full_audio_path, tmpdir_path, chunk)
    return transcribe_chunk_file(model_name, language, chunk, chunk_audio_path)


def transcribe_chunk_file(
    model_name: str,
    language: str | None,
    chunk: ChunkSpec,
    chunk_audio_path: Path,
) -> TranscribedChunk:
    model = get_thread_local_model(model_name)

    print(
        f"Before transcribe chunk {chunk.index + 1} "
        f"[{chunk.core_start_sec:.2f}, {chunk.core_end_sec:.2f}]"
    )
    segments_iter, info = model.transcribe(
        str(chunk_audio_path),
        language=language,
        vad_filter=True,
    )
    print(f"After transcribe chunk {chunk.index + 1}")

    chunk_segments = normalize_and_pack_segments(
        segments_iter,
        offset_sec=chunk.extract_start_sec,
    )
    chunk_segments = keep_segments_from_chunk_core(chunk_segments, chunk)

    detected_language = getattr(info, "language", None) if info is not None else None
    if isinstance(detected_language, str) and not detected_language.strip():
        detected_language = None

    return TranscribedChunk(
        chunk=chunk,
        segments=chunk_segments,
        detected_language=detected_language,
    )


def get_thread_local_model(model_name: str) -> WhisperModel:
    cached = getattr(_THREAD_LOCAL, "model", None)
    cached_name = getattr(_THREAD_LOCAL, "model_name", None)
    if cached is None or cached_name != model_name:
        cached = create_whisper_model(model_name)
        _THREAD_LOCAL.model = cached
        _THREAD_LOCAL.model_name = model_name
    return cached


def create_whisper_model(model_name: str, cpu_threads=1) -> WhisperModel:
    try:
        return WhisperModel(
            model_size_or_path=model_name,
            device="cpu",
            compute_type="int8",
            cpu_threads=cpu_threads,
        )
    except TypeError:
        return WhisperModel(
            model_size_or_path=model_name,
            device="cpu",
            compute_type="int8",
        )


def probe_media_duration(source_path: Path) -> float:
    commands = [
        [
            "ffprobe",
            "-v",
            "error",
            "-show_entries",
            "format=duration",
            "-of",
            "json",
            str(source_path),
        ],
        [
            "ffprobe",
            "-v",
            "error",
            "-show_entries",
            "stream=duration",
            "-of",
            "json",
            str(source_path),
        ],
    ]

    last_error: str | None = None
    for command in commands:
        try:
            proc = subprocess.run(
                command,
                check=True,
                capture_output=True,
                text=True,
            )
            raw = json.loads(proc.stdout)
            duration = extract_duration_from_ffprobe(raw)
            if duration is not None and duration > 0:
                return duration
        except FileNotFoundError as ex:
            raise RuntimeError(
                "ffprobe is required for chunked transcription but was not found in PATH."
            ) from ex
        except subprocess.CalledProcessError as ex:
            last_error = ex.stderr.strip() if ex.stderr else str(ex)
        except Exception as ex:
            last_error = str(ex)

    raise RuntimeError(
        "Failed to determine media duration for chunked transcription."
        + (f" ffprobe error: {last_error}" if last_error else "")
    )


def extract_full_audio(source_path: Path, tmpdir_path: Path) -> Path:
    output_path = tmpdir_path / "full_audio.wav"
    command = [
        "ffmpeg",
        "-hide_banner",
        "-loglevel",
        "error",
        "-y",
        "-i",
        str(source_path),
        "-vn",
        "-ac",
        "1",
        "-ar",
        "16000",
        "-f",
        "wav",
        str(output_path),
    ]

    try:
        subprocess.run(command, check=True, capture_output=True, text=True)
    except FileNotFoundError as ex:
        raise RuntimeError(
            "ffmpeg is required for chunked transcription but was not found in PATH."
        ) from ex
    except subprocess.CalledProcessError as ex:
        stderr = ex.stderr.strip() if ex.stderr else ""
        raise RuntimeError(f"Failed to extract audio from source video. {stderr}") from ex

    return output_path


def extract_audio_chunk(source_audio_path: Path, tmpdir_path: Path, chunk: ChunkSpec) -> Path:
    duration_sec = max(0.0, chunk.extract_end_sec - chunk.extract_start_sec)
    if duration_sec <= 0:
        raise RuntimeError(f"Invalid chunk duration for chunk {chunk.index}.")

    output_path = tmpdir_path / f"chunk_{chunk.index:05d}.wav"
    command = [
        "ffmpeg",
        "-hide_banner",
        "-loglevel",
        "error",
        "-y",
        "-ss",
        f"{chunk.extract_start_sec:.6f}",
        "-t",
        f"{duration_sec:.6f}",
        "-i",
        str(source_audio_path),
        "-ac",
        "1",
        "-ar",
        "16000",
        "-f",
        "wav",
        str(output_path),
    ]

    try:
        subprocess.run(command, check=True, capture_output=True, text=True)
    except FileNotFoundError as ex:
        raise RuntimeError(
            "ffmpeg is required for chunked transcription but was not found in PATH."
        ) from ex
    except subprocess.CalledProcessError as ex:
        stderr = ex.stderr.strip() if ex.stderr else ""
        raise RuntimeError(
            f"Failed to extract audio chunk {chunk.index} from source audio. {stderr}"
        ) from ex

    return output_path


def normalize_and_pack_segments(
    segments_iter: Any,
    offset_sec: float,
) -> list[dict[str, Any]]:
    segments: list[dict[str, Any]] = []

    for segment in segments_iter:
        text = (segment.text or "").strip()
        if not text:
            continue

        start_sec = float(segment.start) + offset_sec
        end_sec = float(segment.end) + offset_sec
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

    return segments


def keep_segments_from_chunk_core(
    segments: list[dict[str, Any]],
    chunk: ChunkSpec,
) -> list[dict[str, Any]]:
    kept: list[dict[str, Any]] = []

    for segment in segments:
        start_sec = float(segment["startSec"])
        end_sec = float(segment["endSec"])
        midpoint = (start_sec + end_sec) / 2.0

        if chunk.is_last:
            in_core = chunk.core_start_sec <= midpoint <= chunk.core_end_sec + 1e-9
        else:
            in_core = chunk.core_start_sec <= midpoint < chunk.core_end_sec

        if in_core:
            kept.append(segment)

    return kept


def merge_and_dedupe_segments(
    segments: list[dict[str, Any]],
    chunk_overlap_sec: float,
) -> list[dict[str, Any]]:
    if not segments:
        return []

    sorted_segments = sorted(
        segments,
        key=lambda item: (float(item["startSec"]), float(item["endSec"]), item["text"]),
    )

    deduped: list[dict[str, Any]] = []
    tolerance_sec = min(2.0, max(0.25, chunk_overlap_sec / 2.0))

    for segment in sorted_segments:
        if not deduped:
            deduped.append(segment)
            continue

        previous = deduped[-1]
        if is_duplicate_segment(previous, segment, tolerance_sec):
            continue

        deduped.append(segment)

    for index, segment in enumerate(deduped):
        segment["index"] = index

    return deduped


def is_duplicate_segment(
    previous: dict[str, Any],
    current: dict[str, Any],
    tolerance_sec: float,
) -> bool:
    previous_text = normalize_text_for_dedupe(str(previous.get("text", "")))
    current_text = normalize_text_for_dedupe(str(current.get("text", "")))

    if not previous_text or not current_text:
        return False

    if previous_text != current_text:
        return False

    previous_start = float(previous["startSec"])
    previous_end = float(previous["endSec"])
    current_start = float(current["startSec"])
    current_end = float(current["endSec"])

    overlap_sec = min(previous_end, current_end) - max(previous_start, current_start)
    gap_sec = current_start - previous_end
    reverse_gap_sec = previous_start - current_end

    return overlap_sec >= -tolerance_sec or gap_sec <= tolerance_sec or reverse_gap_sec <= tolerance_sec


def normalize_text_for_dedupe(text: str) -> str:
    normalized = unicodedata.normalize("NFKC", text).casefold()
    normalized = re.sub(r"[\W_]+", " ", normalized, flags=re.UNICODE)
    normalized = re.sub(r"\s+", " ", normalized).strip()
    return normalized


def detect_language(language: str | None) -> str:
    if isinstance(language, str) and language.strip():
        return language.strip()
    return "unknown"


def build_transcript_payload(
    payload: InputPayload,
    source_path: Path,
    title: str,
    language: str,
    duration_sec: float,
    segments: list[dict[str, Any]],
) -> dict[str, Any]:
    return {
        "jobId": payload.job_id,
        "status": "success",
        "lecture": {
            "title": title,
            "sourceFileName": source_path.name,
            "sourcePath": str(source_path),
            "language": language,
            "durationSec": duration_sec,
        },
        "transcriber": {
            "provider": payload.transcription_provider,
            "model": payload.transcription_model,
        },
        "segments": segments,
    }


def parse_args(argv: list[str]) -> tuple[Path, Path]:
    if len(argv) != 3:
        raise InputValidationError("Usage: python main.py <input-json-path> <output-json-path>")

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

    return InputPayload(
        job_id=require_non_empty_string(raw, "jobId"),
        input_video_path=require_non_empty_string(raw, "inputVideoPath"),
        output_transcript_path=require_non_empty_string(raw, "outputTranscriptPath"),
        transcription_provider=require_non_empty_string(raw, "transcriptionProvider"),
        transcription_model=require_non_empty_string(raw, "transcriptionModel"),
        overwrite=require_bool(raw, "overwrite"),
        requested_title=optional_string(raw, "requestedTitle"),
        language_hint=optional_string(raw, "languageHint"),
        chunk_time_sec=optional_positive_number(raw, "chunkTime"),
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


def optional_positive_number(data: dict[str, Any], key: str) -> float | None:
    value = data.get(key)
    if value is None:
        return None
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise InputValidationError(f"Field '{key}' must be a positive number when provided.")
    number = float(value)
    if not math.isfinite(number) or number <= 0:
        raise InputValidationError(f"Field '{key}' must be a positive number when provided.")
    return number


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
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


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
