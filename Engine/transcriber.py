"""
SNA-Dubber Engine: Speech Transcription Module
Extracts audio from video with dynamic normalization and recognizes speech dialogue with precise timestamps.
Uses Gemini Multimodal Audio API with Multi-Key Pool failover and automated model fallback.
Detects male and female voices automatically (piset vs sreymom).
NO HARDCODED / FAKE SEGMENTS.
"""

import os
import sys
import json
import subprocess
import tempfile
from typing import List, Dict, Any
from translator import GeminiKeyPool, parse_api_keys

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
    return "not found" in text or "not supported" in text or "unsupported" in text

def extract_audio_for_transcription(video_path: str, output_wav: str, high_boost: bool = False) -> bool:
    """Extracts 16kHz mono audio optimized for speech recognition with dynamic range normalization."""
    # Enhance speech clarity and boost dialogue over background music
    filter_chain = "highpass=f=80,lowpass=f=7500,volume=2.4,dynaudnorm=f=150:g=15"
    if high_boost:
        filter_chain = "highpass=f=100,lowpass=f=7000,volume=3.5,dynaudnorm=f=100:g=21"

    cmd = [
        "ffmpeg", "-y", "-i", video_path,
        "-vn", "-af", filter_chain,
        "-acodec", "pcm_s16le", "-ar", "16000", "-ac", "1",
        output_wav
    ]
    try:
        subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)
        if os.path.exists(output_wav) and os.path.getsize(output_wav) > 1000:
            return True
    except Exception as e:
        sys.stderr.write(f"FFmpeg dynamic audio extraction error: {e}, attempting simple extraction\n")

    # Fallback to simple extraction if filter failed
    cmd_fallback = [
        "ffmpeg", "-y", "-i", video_path,
        "-vn", "-acodec", "pcm_s16le", "-ar", "16000", "-ac", "1",
        output_wav
    ]
    try:
        subprocess.run(cmd_fallback, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)
        return os.path.exists(output_wav) and os.path.getsize(output_wav) > 0
    except Exception as e:
        sys.stderr.write(f"FFmpeg audio extraction error: {e}\n")
        return False

def transcribe_with_gemini(audio_path: str, pool: GeminiKeyPool, model_name: str = "gemini-2.5-flash") -> List[Dict[str, Any]]:
    """Transcribes dialogue with timestamps using Gemini Multimodal Audio with Multi-Key rotation and model fallback."""
    if not pool.has_keys():
        raise ValueError("No active Gemini API Key available. Please check your Gemini API Keys in Settings.")

    prompt = (
        "You are an expert movie subtitle and speech transcription engine. "
        "Listen to this audio track carefully and transcribe every spoken dialogue sentence with accurate timestamps. "
        "Transcribe all speech even if whispered, soft, colloquial, rapid, or accompanied by background music or sound effects. "
        "For each dialogue line, determine whether the speaker is male or female based on acoustic vocal tone: "
        "- Set 'gender': 'female' or 'male'. "
        "- Set 'voice': 'sreymom' for female voices, or 'piset' for male voices. "
        "Output strictly a valid JSON array of objects with the following schema: "
        "["
        "  {"
        "    \"index\": 1,"
        "    \"start_time\": \"00:00:01.500\","
        "    \"end_time\": \"00:00:04.200\","
        "    \"start_ms\": 1500,"
        "    \"end_ms\": 4200,"
        "    \"speaker\": \"Character 1\","
        "    \"gender\": \"male\","
        "    \"voice\": \"piset\","
        "    \"chinese\": \"Actual spoken dialogue sentence from the video\","
        "    \"emotion\": \"Normal\""
        "  }"
        "]"
        "Rules:"
        "1. Output ONLY a valid raw JSON array."
        "2. Accurately transcribe actual spoken speech from this audio. Do not invent or fabricate lines."
        "3. If there is no speech or silence, output an empty JSON array []."
    )

    valid_models = ["gemini-2.5-flash", "gemini-2.5-flash-lite", "gemini-2.0-flash", "gemini-1.5-flash"]
    chosen_model = model_name if model_name in valid_models else "gemini-2.5-flash"
    candidate_models = [chosen_model] + [m for m in valid_models if m != chosen_model]

    last_error = None
    while pool.has_keys():
        client, types, key = pool.get_client()
        if not client or not types:
            break

        uploaded_file = None
        try:
            # Upload audio file to Gemini Files API
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
                        return data
                except Exception as model_err:
                    last_error = model_err
                    if _is_quota_error(model_err):
                        pool.mark_exhausted(key, model_err)
                        break
                    elif not _is_unavailable_model(model_err):
                        sys.stderr.write(f"Model {m} transcription warning: {model_err}\n")
                    continue
        except Exception as upload_err:
            last_error = upload_err
            if _is_quota_error(upload_err):
                pool.mark_exhausted(key, upload_err)
            else:
                sys.stderr.write(f"Gemini upload error on key: {upload_err}\n")
        finally:
            if uploaded_file and client:
                try:
                    client.files.delete(name=uploaded_file.name)
                except Exception:
                    pass

    if last_error:
        sys.stderr.write(f"All Gemini models/keys attempted: {last_error}\n")
        raise RuntimeError(f"Gemini transcription failed: {last_error}")
    return []

def transcribe_video(video_path: str, api_key_or_keys: Any = "", model_name: str = "gemini-2.5-flash") -> List[Dict[str, Any]]:
    """Full workflow: video -> audio extraction with normalization -> speech transcription with multi-key pool."""
    if not os.path.exists(video_path):
        raise FileNotFoundError(f"Video file not found: {video_path}")

    pool = GeminiKeyPool(api_key_or_keys)
    if not pool.has_keys():
        raise RuntimeError("Gemini API Key is required for speech recognition. Please enter your key in Settings.")

    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp_audio:
        tmp_audio_path = tmp_audio.name

    try:
        ok = extract_audio_for_transcription(video_path, tmp_audio_path, high_boost=False)
        if not ok:
            raise RuntimeError("Failed to extract audio from video using FFmpeg.")

        results = transcribe_with_gemini(tmp_audio_path, pool, model_name)

        # If empty, retry with higher speech amplification before giving up
        if not results:
            sys.stderr.write("Initial transcription returned no speech, retrying with high speech boost...\n")
            if extract_audio_for_transcription(video_path, tmp_audio_path, high_boost=True):
                try:
                    results = transcribe_with_gemini(tmp_audio_path, pool, model_name)
                except Exception:
                    pass

        return results if results is not None else []

    finally:
        if os.path.exists(tmp_audio_path):
            try:
                os.remove(tmp_audio_path)
            except Exception:
                pass
