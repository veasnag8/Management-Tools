"""
SNA-Dubber Engine: Speech Transcription Module
Extracts audio from video and recognizes speech dialogue with precise timestamps.
Uses Gemini Multimodal Audio API with automated model fallback and Multi-Key Pool rotation.
NO HARDCODED / FAKE SEGMENTS.
"""

import os
import sys
import json
import shutil
import subprocess
import tempfile
from typing import List, Dict, Any

def get_ffmpeg_path() -> str:
    """Finds ffmpeg executable from PATH, local Tools, or standard installation dirs."""
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

def parse_api_keys(raw_input: Any) -> List[str]:
    """Extracts, cleans, and deduplicates Gemini API keys from string, list, or env."""
    keys: List[str] = []

    if isinstance(raw_input, list):
        for item in raw_input:
            if isinstance(item, str):
                cleaned = item.strip().strip("'\"`").strip("\ufeff\u200b")
                if cleaned and not cleaned.startswith("#") and len(cleaned) > 10 and cleaned not in keys:
                    keys.append(cleaned)
    elif isinstance(raw_input, str) and raw_input:
        parts = [p.strip().strip("'\"`").strip("\ufeff\u200b") for p in raw_input.replace("\r", "\n").replace(";", "\n").replace(",", "\n").split("\n")]
        for p in parts:
            if p and not p.startswith("#") and len(p) > 10 and p not in keys:
                keys.append(p)

    # Fallback to local files if empty
    if not keys:
        local_appdata = os.environ.get("LOCALAPPDATA", "")
        if local_appdata:
            pool_file = os.path.join(local_appdata, "SNA-Dubber", "gemini_api_keys.txt")
            if os.path.exists(pool_file):
                try:
                    with open(pool_file, "r", encoding="utf-8-sig", errors="replace") as pf:
                        for line in pf:
                            k = line.strip().strip("'\"`").strip("\ufeff\u200b")
                            if k and not k.startswith("#") and len(k) > 10 and k not in keys:
                                keys.append(k)
                except Exception:
                    pass

            single_file = os.path.join(local_appdata, "SNA-Dubber", "gemini_api_key.txt")
            if os.path.exists(single_file):
                try:
                    with open(single_file, "r", encoding="utf-8-sig", errors="replace") as sf:
                        k = sf.read().strip().strip("'\"`").strip("\ufeff\u200b")
                        if k and not k.startswith("#") and len(k) > 10 and k not in keys:
                            keys.append(k)
                except Exception:
                    pass

    # Fallback to env
    if not keys:
        env_val = os.environ.get("GEMINI_API_KEY", "")
        if env_val:
            parts = [p.strip().strip("'\"`").strip("\ufeff\u200b") for p in env_val.replace("\r", "\n").replace(";", "\n").replace(",", "\n").split("\n")]
            for p in parts:
                if p and not p.startswith("#") and len(p) > 10 and p not in keys:
                    keys.append(p)

    return keys

def _mark_quota_exhausted(error: Exception) -> None:
    message = str(error or "")
    lowered = message.lower()
    if "resource_exhausted" not in lowered and "quota" not in lowered and "429" not in lowered:
        return
    try:
        flag_path = os.path.join(tempfile.gettempdir(), "sna_gemini_quota_exhausted.flag")
        with open(flag_path, "w", encoding="utf-8") as flag:
            flag.write(message)
    except Exception:
        pass

def _is_quota_error(error: Exception) -> bool:
    text = str(error or "").lower()
    return "resource_exhausted" in text or "quota" in text or "429" in text

def _is_unavailable_model(error: Exception) -> bool:
    text = str(error or "").lower()
    return "not found" in text or "not supported" in text or "unsupported" in text or "no longer available" in text

def extract_audio_for_transcription(video_path: str, output_path: str) -> bool:
    """Extracts lightweight 16kHz mono audio (MP3 preferred, WAV fallback) optimized for speech recognition."""
    ffmpeg = get_ffmpeg_path()
    is_mp3 = output_path.lower().endswith(".mp3")
    
    if is_mp3:
        cmd = [
            ffmpeg, "-y", "-i", video_path,
            "-vn", "-acodec", "libmp3lame", "-ar", "16000", "-ac", "1", "-b:a", "32k",
            output_path
        ]
    else:
        cmd = [
            ffmpeg, "-y", "-i", video_path,
            "-vn", "-acodec", "pcm_s16le", "-ar", "16000", "-ac", "1",
            output_path
        ]

    try:
        subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)
        return os.path.exists(output_path) and os.path.getsize(output_path) > 0
    except Exception as e:
        sys.stderr.write(f"FFmpeg audio extraction primary attempt failed: {e}\n")
        # Fallback to PCM WAV if MP3 failed
        if is_mp3:
            wav_path = os.path.splitext(output_path)[0] + ".wav"
            cmd_wav = [
                ffmpeg, "-y", "-i", video_path,
                "-vn", "-acodec", "pcm_s16le", "-ar", "16000", "-ac", "1",
                wav_path
            ]
            try:
                subprocess.run(cmd_wav, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)
                if os.path.exists(wav_path) and os.path.getsize(wav_path) > 0:
                    shutil.move(wav_path, output_path)
                    return True
            except Exception as e2:
                sys.stderr.write(f"FFmpeg WAV fallback also failed: {e2}\n")
        return False

def _format_timecode(ms: int) -> str:
    """Formats milliseconds into hh:mm:ss.fff"""
    hours = ms // 3600000
    ms %= 3600000
    minutes = ms // 60000
    ms %= 60000
    seconds = ms // 1000
    millis = ms % 1000
    return f"{hours:02d}:{minutes:02d}:{seconds:02d}.{millis:03d}"

def transcribe_with_gemini(audio_path: str, api_key_input: Any, model_name: str = "gemini-3.5-flash") -> List[Dict[str, Any]]:
    """Transcribes dialogue with timestamps using Gemini Multimodal Audio with Multi-Key Pool & Model fallback."""
    keys = parse_api_keys(api_key_input)
    if not keys:
        raise ValueError("Gemini API Key is missing. Please enter your Gemini API Key in Settings or Key Pool.")

    from google import genai
    from google.genai import types

    prompt = (
        "You are an expert movie subtitle and speech transcription engine. "
        "Listen to this audio track carefully and transcribe every spoken dialogue sentence with accurate timestamps. "
        "Output strictly a valid JSON array of objects with the following schema: "
        "["
        "  {"
        "    \"index\": 1,"
        "    \"start_time\": \"00:00:01.500\","
        "    \"end_time\": \"00:00:04.200\","
        "    \"start_ms\": 1500,"
        "    \"end_ms\": 4200,"
        "    \"speaker\": \"Character 1\","
        "    \"chinese\": \"Actual spoken dialogue sentence from the video\","
        "    \"emotion\": \"Normal\""
        "  }"
        "]"
        "Rules:"
        "1. Output ONLY a valid raw JSON array."
        "2. Accurately transcribe actual spoken speech from this audio. Do not invent or fabricate lines."
        "3. If there is no speech or silence, output an empty JSON array []."
    )

    candidate_models = [
        "gemini-3.5-flash",
        "gemini-3.8-flash",
        "gemini-flash-latest",
        "gemini-3.1-flash-lite",
        "gemini-flash-lite-latest",
        "gemini-3.5-flash-lite"
    ]
    if model_name and "2.0" not in model_name and "2.5" not in model_name:
        if model_name in candidate_models:
            candidate_models.remove(model_name)
        candidate_models.insert(0, model_name)

    audio_size = os.path.getsize(audio_path)
    audio_bytes = None
    is_mp3 = audio_path.lower().endswith(".mp3")
    mime_type = "audio/mp3" if is_mp3 else "audio/wav"

    if audio_size <= 20 * 1024 * 1024:
        with open(audio_path, "rb") as af:
            audio_bytes = af.read()

    last_error = None
    quota_failures = 0
    non_quota_failures = 0
    last_quota_error = None

    for key_idx, key in enumerate(keys):
        sys.stderr.write(f"[Transcriber] Using API Key #{key_idx + 1} ({key[:8]}...)\n")
        client = None
        try:
            client = genai.Client(api_key=key)
        except Exception as client_err:
            sys.stderr.write(f"Failed to initialize Gemini client for key: {client_err}\n")
            continue

        uploaded_file = None
        if audio_bytes is None:
            try:
                uploaded_file = client.files.upload(file=audio_path)
            except Exception as up_err:
                sys.stderr.write(f"Audio upload failed for key: {up_err}\n")
                continue

        try:
            for m in candidate_models:
                try:
                    contents_payload = []
                    if audio_bytes is not None:
                        part = types.Part.from_bytes(data=audio_bytes, mime_type=mime_type)
                        contents_payload = [part, prompt]
                    else:
                        contents_payload = [uploaded_file, prompt]

                    response = client.models.generate_content(
                        model=m,
                        contents=contents_payload,
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
                        # Normalize segment objects
                        normalized: List[Dict[str, Any]] = []
                        for i, item in enumerate(data, start=1):
                            if not isinstance(item, dict):
                                continue
                            start_ms = int(item.get("start_ms", 0))
                            end_ms = int(item.get("end_ms", start_ms + 2500))
                            if end_ms <= start_ms:
                                end_ms = start_ms + 2000

                            start_time = item.get("start_time") or _format_timecode(start_ms)
                            end_time = item.get("end_time") or _format_timecode(end_ms)
                            chinese_text = str(item.get("chinese", "")).strip()

                            normalized.append({
                                "index": i,
                                "start_time": start_time,
                                "end_time": end_time,
                                "start_ms": start_ms,
                                "end_ms": end_ms,
                                "speaker": str(item.get("speaker") or f"Character {i}"),
                                "chinese": chinese_text,
                                "emotion": str(item.get("emotion") or "Normal")
                            })
                        return normalized

                except Exception as model_err:
                    last_error = model_err
                    if _is_quota_error(model_err):
                        quota_failures += 1
                        last_quota_error = model_err
                        sys.stderr.write(f"Key #{key_idx + 1} model {m} quota exhausted: {model_err}\n")
                        break  # Break model loop to rotate to next API key
                    elif not _is_unavailable_model(model_err):
                        non_quota_failures += 1
                    sys.stderr.write(f"Model {m} failed: {model_err}\n")
                    continue
        finally:
            if uploaded_file is not None:
                try:
                    client.files.delete(name=uploaded_file.name)
                except Exception:
                    pass

    if last_error:
        if quota_failures > 0 and non_quota_failures == 0:
            _mark_quota_exhausted(last_quota_error)
        sys.stderr.write(f"All Gemini models & keys failed: {last_error}\n")
        raise RuntimeError(f"Gemini transcription failed: {last_error}")

    return []

def transcribe_video(video_path: str, api_key: str = "", model_name: str = "gemini-3.5-flash") -> List[Dict[str, Any]]:
    """Full workflow: video -> audio extraction (MP3) -> speech transcription."""
    if not os.path.exists(video_path):
        raise FileNotFoundError(f"Video file not found: {video_path}")

    with tempfile.NamedTemporaryFile(suffix=".mp3", delete=False) as tmp_audio:
        tmp_audio_path = tmp_audio.name

    try:
        ok = extract_audio_for_transcription(video_path, tmp_audio_path)
        if not ok:
            raise RuntimeError("Failed to extract audio from video using FFmpeg.")

        results = transcribe_with_gemini(tmp_audio_path, api_key, model_name)
        return results if results is not None else []

    finally:
        if os.path.exists(tmp_audio_path):
            try:
                os.remove(tmp_audio_path)
            except Exception:
                pass
