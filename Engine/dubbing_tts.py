"""
SNA-Dubber Engine: Khmer Voice Dubbing & Neural TTS Module
Features verified high-definition Microsoft Neural voices:
- km-KH-PisethNeural (Male Profile: 'Piset')
- km-KH-SreymomNeural (Female Profile: 'Srey Mom')
Supports speed (rate), volume, pitch adjustments, sentence-by-sentence preview,
and batch movie dialogue generation.
"""

import os
import sys
import asyncio
import subprocess
import edge_tts
from typing import List, Dict, Any, Optional

VOICE_MAP = {
    "piset": "km-KH-PisethNeural",
    "piseth": "km-KH-PisethNeural",
    "male": "km-KH-PisethNeural",
    "sreymom": "km-KH-SreymomNeural",
    "srey_mom": "km-KH-SreymomNeural",
    "female": "km-KH-SreymomNeural"
}

def resolve_voice(voice_id: str) -> str:
    """Resolves UI voice identifier to verified Neural voice tag."""
    key = (voice_id or "").strip().lower()
    if key in VOICE_MAP:
        return VOICE_MAP[key]
    if "sreymom" in key or "female" in key:
        return "km-KH-SreymomNeural"
    return "km-KH-PisethNeural"

async def _synthesize_async(
    text: str,
    output_path: str,
    voice_name: str,
    rate: str = "+0%",
    volume: str = "+0%",
    pitch: str = "+0Hz"
) -> bool:
    """Async generation with edge-tts."""
    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    communicate = edge_tts.Communicate(
        text=text,
        voice=voice_name,
        rate=rate,
        volume=volume,
        pitch=pitch
    )
    await communicate.save(output_path)
    return os.path.exists(output_path) and os.path.getsize(output_path) > 0

def get_audio_duration_ms(file_path: str) -> int:
    """Retrieves audio duration in milliseconds using ffprobe."""
    if not os.path.exists(file_path):
        return 0
    cmd = [
        "ffprobe", "-v", "error", "-show_entries", "format=duration",
        "-of", "default=noprint_wrappers=1:nokey=1", file_path
    ]
    try:
        res = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, check=True)
        dur_sec = float(res.stdout.strip())
        return int(dur_sec * 1000)
    except Exception:
        # Estimation fallback based on mp3 size (~16KB/sec at 128kbps)
        size = os.path.getsize(file_path)
        return int((size / 16000) * 1000)

def auto_fit_duration(file_path: str, max_duration_ms: int) -> bool:
    """
    Prevents dialogue audio overlap (កុំអោយនិយាយជាន់សំឡេងគ្នា).
    If audio duration exceeds max_duration_ms, speeds up via FFmpeg atempo
    so the spoken sentence finishes cleanly before the next sentence begins.
    """
    if not os.path.exists(file_path) or max_duration_ms <= 300:
        return False
    actual_ms = get_audio_duration_ms(file_path)
    if actual_ms <= max_duration_ms:
        return True

    tempo = actual_ms / float(max_duration_ms)
    clamped_tempo = min(1.48, max(1.05, tempo))
    temp_fitted = file_path + ".fit.mp3"
    try:
        cmd = [
            "ffmpeg", "-y", "-i", file_path,
            "-filter:a", f"atempo={clamped_tempo:.3f}",
            "-c:a", "libmp3lame", "-b:a", "192k",
            temp_fitted
        ]
        res = subprocess.run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=8)
        if res.returncode == 0 and os.path.exists(temp_fitted) and os.path.getsize(temp_fitted) > 0:
            fitted_ms = get_audio_duration_ms(temp_fitted)
            if fitted_ms > max_duration_ms + 60:
                trim_sec = max_duration_ms / 1000.0
                temp_trimmed = file_path + ".trim.mp3"
                trim_cmd = [
                    "ffmpeg", "-y", "-i", temp_fitted,
                    "-t", f"{trim_sec:.3f}",
                    "-af", f"afade=t=out:st={max(0.1, trim_sec - 0.08):.3f}:d=0.08",
                    "-c:a", "libmp3lame", "-b:a", "192k",
                    temp_trimmed
                ]
                subprocess.run(trim_cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=8)
                if os.path.exists(temp_trimmed) and os.path.getsize(temp_trimmed) > 0:
                    os.replace(temp_trimmed, file_path)
                    try: os.remove(temp_fitted)
                    except: pass
                    return True
            os.replace(temp_fitted, file_path)
            return True
    except Exception as e:
        sys.stderr.write(f"auto_fit_duration error: {e}\n")
    return False

def synthesize_sentence(
    text: str,
    output_path: str,
    voice_id: str = "piset",
    speed: float = 1.0,
    volume: float = 1.0,
    pitch_hz: int = 0,
    max_duration_ms: int = 0
) -> Dict[str, Any]:
    """
    Synthesizes a single Khmer sentence with automatic volume boost and duration fitting.
    speed: 0.5 to 2.0 (1.0 is normal).
    volume: 0.0 to 2.0 (1.0 is normal).
    pitch_hz: -50 to +50 Hz.
    max_duration_ms: if > 0, compresses audio tempo so it never overlaps following lines.
    """
    clean_text = (text or "").strip()
    if not clean_text:
        return {"success": False, "error": "Empty text"}

    voice_tag = resolve_voice(voice_id)

    # Convert speed float to rate string (e.g. 1.15 -> +15%, 0.85 -> -15%)
    rate_pct = int(round((speed - 1.0) * 100))
    rate_str = f"{'+' if rate_pct >= 0 else ''}{rate_pct}%"

    # User requested: Khmer speech to be much louder and punchier
    vol_str = "+100%"

    effective_pitch = pitch_hz
    if effective_pitch == 0 and voice_id:
        v_low = voice_id.lower()
        if "spk_3" in v_low or "speaker 3" in v_low:
            effective_pitch = -2
        elif "spk_4" in v_low or "speaker 4" in v_low:
            effective_pitch = 3
        elif "spk_5" in v_low or "speaker 5" in v_low:
            effective_pitch = 2
        elif "spk_6" in v_low or "speaker 6" in v_low:
            effective_pitch = -2
        elif "spk_7" in v_low or "speaker 7" in v_low:
            effective_pitch = -4
        elif "spk_8" in v_low or "speaker 8" in v_low:
            effective_pitch = 4

    pitch_str = f"{'+' if effective_pitch >= 0 else ''}{effective_pitch}Hz"

    try:
        ok = asyncio.run(_synthesize_async(
            text=clean_text,
            output_path=output_path,
            voice_name=voice_tag,
            rate=rate_str,
            volume=vol_str,
            pitch=pitch_str
        ))
        
        # Apply ffmpeg volume gain booster to maximize speech loudness without clipping
        if ok and os.path.exists(output_path) and os.path.getsize(output_path) > 0:
            try:
                temp_boost = output_path + ".loud.mp3"
                boost_res = subprocess.run([
                    "ffmpeg", "-y", "-i", output_path,
                    "-filter:a", "volume=2.6,alimiter=limit=0.98",
                    "-c:a", "libmp3lame", "-b:a", "192k",
                    temp_boost
                ], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=6)
                if boost_res.returncode == 0 and os.path.exists(temp_boost) and os.path.getsize(temp_boost) > 0:
                    os.replace(temp_boost, output_path)
            except Exception:
                pass

        # Auto-fit duration if requested to prevent speech overlapping
        if max_duration_ms > 0:
            auto_fit_duration(output_path, max_duration_ms)

        # Get audio duration using ffprobe if available
        duration_ms = get_audio_duration_ms(output_path)

        return {
            "success": ok,
            "output_path": output_path,
            "voice": voice_tag,
            "duration_ms": duration_ms
        }
    except Exception as e:
        return {
            "success": False,
            "error": str(e)
        }

def synthesize_batch(
    dialogue_lines: List[Dict[str, Any]],
    output_dir: str,
    default_voice: str = "piset"
) -> List[Dict[str, Any]]:
    """Synthesizes all approved Khmer dialogue lines in a project."""
    os.makedirs(output_dir, exist_ok=True)
    results = []

    for idx, line in enumerate(dialogue_lines):
        line_id = line.get("id") or f"line_{idx+1}"
        text = line.get("corrected_khmer") or line.get("khmer_text") or ""
        voice = line.get("voice_profile") or default_voice
        speed = float(line.get("speed", 1.0))
        volume = float(line.get("volume", 1.0))
        pitch = int(line.get("pitch", 0))

        start_ms = int(line.get("start_ms", 0) or 0)
        end_ms = int(line.get("end_ms", 0) or 0)
        max_dur = 0
        if idx + 1 < len(dialogue_lines):
            next_start = int(dialogue_lines[idx+1].get("start_ms", 0) or 0)
            if next_start > start_ms:
                max_dur = max(500, next_start - start_ms - 100)
        if max_dur == 0 and end_ms > start_ms:
            max_dur = end_ms - start_ms + 800

        out_file = os.path.join(output_dir, f"{line_id}.mp3")
        res = synthesize_sentence(
            text=text,
            output_path=out_file,
            voice_id=voice,
            speed=speed,
            volume=volume,
            pitch_hz=pitch,
            max_duration_ms=max_dur
        )
        res["id"] = line_id
        res["start_ms"] = start_ms
        res["end_ms"] = end_ms
        results.append(res)

    return results
