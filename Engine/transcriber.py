"""
SNA-Dubber Engine: Speech Transcription Module
High-Performance Chunked Speech Transcription with Multi-Key Pool & Multi-Threaded Concurrency.
Supports both short clips and long videos (up to 2+ hours) without token truncation or API hangs.
Detects male and female voices automatically (piset vs sreymom).
NO HARDCODED / FAKE SEGMENTS.
"""

import os
import sys
import json
import shutil
import tempfile
import subprocess
from typing import List, Dict, Any
from concurrent.futures import ThreadPoolExecutor, as_completed
from translator import GeminiKeyPool, parse_api_keys

def get_ffmpeg_path() -> str:
    which = shutil.which("ffmpeg")
    if which and os.path.exists(which):
        return which
    candidates = [
        os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "Tools", "ffmpeg.exe")),
        os.path.abspath(os.path.join(os.path.dirname(__file__), "Tools", "ffmpeg.exe")),
        os.path.expandvars(r"%LOCALAPPDATA%\Programs\Tool Dubber\Tools\ffmpeg.exe"),
        r"C:\Users\BT2\AppData\Local\Programs\Tool Dubber\Tools\ffmpeg.exe",
    ]
    for c in candidates:
        if os.path.exists(c):
            return c
    return "ffmpeg"

def get_ffprobe_path() -> str:
    which = shutil.which("ffprobe")
    if which and os.path.exists(which):
        return which
    candidates = [
        os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "Tools", "ffprobe.exe")),
        os.path.abspath(os.path.join(os.path.dirname(__file__), "Tools", "ffprobe.exe")),
        os.path.expandvars(r"%LOCALAPPDATA%\Programs\Tool Dubber\Tools\ffprobe.exe"),
    ]
    for c in candidates:
        if os.path.exists(c):
            return c
    return "ffprobe"

def format_timecode(ms: int) -> str:
    total_sec = max(0, ms) // 1000
    rem_ms = max(0, ms) % 1000
    hh = total_sec // 3600
    mm = (total_sec % 3600) // 60
    ss = total_sec % 60
    return f"{hh:02d}:{mm:02d}:{ss:02d}.{rem_ms:03d}"

def get_media_duration_sec(media_path: str) -> float:
    ffprobe = get_ffprobe_path()
    cmd = [
        ffprobe, "-v", "error", "-show_entries", "format=duration",
        "-of", "default=noprint_wrappers=1:nokey=1", media_path
    ]
    try:
        res = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, check=True)
        return float(res.stdout.strip())
    except Exception:
        return 0.0

def _is_quota_error(error: Exception) -> bool:
    text = str(error or "").lower()
    return "resource_exhausted" in text or "quota" in text or "429" in text

def _is_unavailable_model(error: Exception) -> bool:
    text = str(error or "").lower()
    return "not found" in text or "not supported" in text or "unsupported" in text or "no longer available" in text

def extract_fast_audio(video_path: str, output_mp3: str) -> bool:
    """Extracts lightweight 16kHz mono 32k MP3 audio with speech EQ boost in seconds."""
    ffmpeg = get_ffmpeg_path()
    cmd = [
        ffmpeg, "-y", "-i", video_path,
        "-vn", "-af", "highpass=f=80,lowpass=f=7500,volume=2.2",
        "-acodec", "libmp3lame", "-ar", "16000", "-ac", "1", "-b:a", "32k",
        output_mp3
    ]
    try:
        subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)
        return os.path.exists(output_mp3) and os.path.getsize(output_mp3) > 100
    except Exception as e:
        sys.stderr.write(f"Fast MP3 extraction error: {e}, attempting simple fallback...\n")
        cmd_fallback = [
            ffmpeg, "-y", "-i", video_path,
            "-vn", "-acodec", "libmp3lame", "-ar", "16000", "-ac", "1", "-b:a", "32k",
            output_mp3
        ]
        try:
            subprocess.run(cmd_fallback, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)
            return os.path.exists(output_mp3) and os.path.getsize(output_mp3) > 100
        except Exception:
            return False

def _transcribe_single_audio_file(
    audio_path: str,
    pool: GeminiKeyPool,
    model_name: str = "gemini-3.5-flash-lite",
    offset_ms: int = 0
) -> List[Dict[str, Any]]:
    """Transcribes a single audio chunk (<= 120s) with Gemini Key rotation."""
    if not pool.has_keys():
        return []

    prompt = (
        "You are an expert movie subtitle and speech transcription engine with multi-speaker diarization. "
        "Listen to this audio track carefully and transcribe every spoken dialogue sentence with accurate timestamps in milliseconds (ms). "
        "Transcribe all speech even if whispered, soft, colloquial, rapid, or accompanied by background music or sound effects. "
        "Distinguish different voices in the conversation and assign consistent speaker labels: 'spk_1', 'spk_2', up to 'spk_8'. "
        "For each dialogue line, determine whether the speaker is male or female based on acoustic vocal tone: "
        "- Set 'gender': 'female' or 'male'. "
        "- Set 'voice': 'sreymom' for female voices, or 'piset' for male voices. "
        "Output strictly a valid JSON array of objects with the following schema: "
        "["
        "  {"
        "    \"start_ms\": 1500,"
        "    \"end_ms\": 4200,"
        "    \"speaker\": \"spk_1\","
        "    \"gender\": \"male\","
        "    \"voice\": \"piset\","
        "    \"chinese\": \"Actual spoken dialogue sentence from the video\","
        "    \"emotion\": \"Normal\""
        "  }"
        "]"
        "Rules:"
        "1. start_ms and end_ms must be in milliseconds relative to this audio file (e.g. 1500 for 1.5 seconds, not 1.5)."
        "2. Accurately transcribe actual spoken speech. Do not invent lines."
        "3. If there is no speech or silence, output an empty JSON array []."
        "4. Output ONLY valid JSON array."
    )

    valid_models = [
        "gemini-3.5-flash-lite",
        "gemini-3.8-flash",
        "gemini-3.5-flash",
        "gemini-2.5-flash",
        "gemini-flash-latest",
        "gemini-2.5-flash-lite"
    ]
    chosen_model = model_name if model_name in valid_models else "gemini-3.5-flash-lite"
    candidate_models = [chosen_model] + [m for m in valid_models if m != chosen_model]

    # Try each key at most once to prevent infinite loops
    max_attempts = len(pool.keys)
    for _ in range(max_attempts):
        if not pool.has_keys():
            break
        client, types, key = pool.get_client()
        if not client:
            break

        uploaded_file = None
        try:
            uploaded_file = client.files.upload(file=audio_path)
            for m in candidate_models:
                try:
                    response = client.models.generate_content(
                        model=m,
                        contents=[uploaded_file, prompt],
                        config={"response_mime_type": "application/json"}
                    )
                    text = (response.text or "").strip()
                    if text.startswith("```json"):
                        text = text[7:]
                    if text.startswith("```"):
                        text = text[3:]
                    if text.endswith("```"):
                        text = text[:-3]
                    text = text.strip()

                    if not text or text == "[]":
                        return []

                    data = json.loads(text)
                    if isinstance(data, list):
                        pool.record_usage(response, m)
                        normalized = []
                        for item in data:
                            if not isinstance(item, dict):
                                continue
                            try:
                                rel_start = int(float(item.get("start_ms", 0)))
                                rel_end = int(float(item.get("end_ms", rel_start + 2200)))
                            except Exception:
                                rel_start = 0
                                rel_end = 2200

                            # Fix: if model returns seconds instead of milliseconds (e.g. 1.5s -> 1 or 2, 107s -> 107)
                            if 0 < rel_end < 500:
                                rel_start = int(rel_start * 1000)
                                rel_end = int(rel_end * 1000)

                            if rel_end <= rel_start:
                                rel_end = rel_start + 2000

                            abs_start = rel_start + offset_ms
                            abs_end = rel_end + offset_ms

                            ch = str(item.get("chinese") or item.get("text") or item.get("dialogue") or "").strip()
                            if not ch:
                                continue

                            g = str(item.get("gender") or "").strip().lower()
                            gender = "female" if g == "female" else "male"
                            v = str(item.get("voice") or "").strip().lower()
                            voice = "sreymom" if (v == "sreymom" or gender == "female") else "piset"

                            normalized.append({
                                "start_ms": abs_start,
                                "end_ms": abs_end,
                                "start_time": format_timecode(abs_start),
                                "end_time": format_timecode(abs_end),
                                "speaker": str(item.get("speaker") or "Character"),
                                "gender": gender,
                                "voice": voice,
                                "chinese": ch,
                                "emotion": str(item.get("emotion") or "Normal")
                            })
                        return normalized
                except Exception as model_err:
                    if _is_quota_error(model_err):
                        pool.mark_exhausted(key, model_err)
                        break
                    elif not _is_unavailable_model(model_err):
                        sys.stderr.write(f"Model {m} warning on key: {model_err}\n")
                    continue
        except Exception as upload_err:
            if _is_quota_error(upload_err):
                pool.mark_exhausted(key, upload_err)
            else:
                sys.stderr.write(f"Gemini upload error: {upload_err}\n")
        finally:
            if uploaded_file and client:
                try:
                    client.files.delete(name=uploaded_file.name)
                except Exception:
                    pass

    return []

def transcribe_video(
    video_path: str,
    api_key_or_keys: Any = "",
    model_name: str = "gemini-3.5-flash-lite"
) -> List[Dict[str, Any]]:
    """
    High-Performance Speech Transcription.
    For short videos (<= 120s): processes in a single fast pass.
    For longer videos (> 120s): slices into 90s chunks and executes in parallel with ThreadPoolExecutor.
    """
    if not os.path.exists(video_path):
        raise FileNotFoundError(f"Video file not found: {video_path}")

    pool = GeminiKeyPool(api_key_or_keys)
    if not pool.has_keys():
        raise RuntimeError("Gemini API Key is required for speech recognition. Please enter your key in Settings.")

    ffmpeg = get_ffmpeg_path()
    total_dur_sec = get_media_duration_sec(video_path)
    sys.stderr.write(f"[Transcribe] Total media duration: {total_dur_sec:.1f}s (~{total_dur_sec/60.0:.1f} minutes)\n")

    with tempfile.NamedTemporaryFile(suffix=".mp3", delete=False) as tmp_audio:
        full_mp3_path = tmp_audio.name

    try:
        ok = extract_fast_audio(video_path, full_mp3_path)
        if not ok:
            raise RuntimeError("Failed to extract audio from video using FFmpeg.")

        CHUNK_LEN = 90  # 90 seconds per slice

        # Short audio (<= 120s) -> single pass
        if total_dur_sec <= 120:
            results = _transcribe_single_audio_file(full_mp3_path, pool, model_name, offset_ms=0)
            for idx, item in enumerate(results, 1):
                item["index"] = idx
            return results

        # Long audio (> 120s) -> slice and process in parallel
        chunks_meta = []
        cur_sec = 0.0
        c_idx = 0
        while cur_sec < total_dur_sec:
            slice_dur = min(CHUNK_LEN, total_dur_sec - cur_sec)
            chunks_meta.append((c_idx, cur_sec, slice_dur))
            cur_sec += slice_dur
            c_idx += 1

        sys.stderr.write(f"[Transcribe] Processing {len(chunks_meta)} audio chunks concurrently across key pool...\n")

        def process_chunk(meta):
            idx, start_s, dur_s = meta
            chunk_file = full_mp3_path + f".part_{idx}.mp3"
            try:
                cmd = [
                    ffmpeg, "-y", "-ss", f"{start_s:.2f}", "-t", f"{dur_s:.2f}",
                    "-i", full_mp3_path, "-c", "copy", chunk_file
                ]
                subprocess.run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=True)
                lines = _transcribe_single_audio_file(chunk_file, pool, model_name, offset_ms=int(start_s * 1000))
                return (idx, lines)
            finally:
                if os.path.exists(chunk_file):
                    try: os.remove(chunk_file)
                    except: pass

        chunk_results = []
        max_workers = min(4, len(chunks_meta))
        with ThreadPoolExecutor(max_workers=max_workers) as executor:
            futures = [executor.submit(process_chunk, m) for m in chunks_meta]
            for fut in as_completed(futures):
                try:
                    chunk_results.append(fut.result())
                except Exception as ex:
                    sys.stderr.write(f"[Transcribe] Chunk processing error: {ex}\n")

        # Merge and sort by start_ms
        chunk_results.sort(key=lambda x: x[0])
        combined_lines = []
        for _, lines in chunk_results:
            combined_lines.extend(lines)

        combined_lines.sort(key=lambda x: x.get("start_ms", 0))
        for idx, item in enumerate(combined_lines, 1):
            item["index"] = idx

        sys.stderr.write(f"[Transcribe] Successfully transcribed {len(combined_lines)} dialogue lines!\n")
        return combined_lines

    finally:
        if os.path.exists(full_mp3_path):
            try: os.remove(full_mp3_path)
            except: pass
