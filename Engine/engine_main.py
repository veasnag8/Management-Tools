"""
SNA-Dubber Engine: Main CLI Dispatcher
Entry point for C# WPF application to invoke Python AI & Audio capabilities via JSON IPC.
"""

import os
import sys
import json
import argparse
import subprocess

if sys.platform.startswith("win"):
    try:
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
        sys.stdin.reconfigure(encoding="utf-8")
    except Exception:
        pass

# Ensure bundled Tools directory (containing ffmpeg.exe) is in PATH
tools_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "Tools"))
if os.path.exists(tools_dir) and tools_dir not in os.environ.get("PATH", ""):
    os.environ["PATH"] = tools_dir + os.pathsep + os.environ.get("PATH", "")

from transcriber import transcribe_video
from translator import translate_dialogue_batch, regenerate_single_line
from dubbing_tts import synthesize_sentence, synthesize_batch
from audio_separator import separate_audio_tracks
from social_metadata import generate_social_copy

def check_environment() -> dict:
    """Verifies that Python, FFmpeg, and dependencies are operational."""
    ffmpeg_ok = False
    ffmpeg_ver = "Not found"
    try:
        res = subprocess.run(["ffmpeg", "-version"], stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        ffmpeg_ok = res.returncode == 0
        if ffmpeg_ok:
            ffmpeg_ver = res.stdout.splitlines()[0] if res.stdout else "Available"
    except Exception as e:
        ffmpeg_ver = str(e)

    genai_ok = False
    try:
        import google.genai
        genai_ok = True
    except Exception:
        genai_ok = False

    edge_tts_ok = False
    try:
        import edge_tts
        edge_tts_ok = True
    except Exception:
        edge_tts_ok = False

    return {
        "status": "ready" if (ffmpeg_ok and edge_tts_ok) else "degraded",
        "python_version": sys.version,
        "ffmpeg_available": ffmpeg_ok,
        "ffmpeg_version": ffmpeg_ver,
        "google_genai_available": genai_ok,
        "edge_tts_available": edge_tts_ok,
        "voices": ["km-KH-PisethNeural", "km-KH-SreymomNeural"]
    }

def main():
    parser = argparse.ArgumentParser(description="SNA-Dubber AI Engine")
    parser.add_argument("--action", required=True, help="Action name to execute")
    parser.add_argument("--payload", default="", help="JSON string payload or path to JSON file")
    args = parser.parse_args()

    payload_data = {}
    if args.payload:
        if os.path.exists(args.payload):
            try:
                with open(args.payload, "rb") as f:
                    raw_b = f.read()
                    if raw_b.startswith(b"\xef\xbb\xbf"):
                        raw_b = raw_b[3:]
                    payload_data = json.loads(raw_b.decode("utf-8", errors="replace"))
            except Exception as e:
                sys.stderr.write(f"Payload file read error: {e}\n")
        else:
            try:
                raw_str = args.payload.lstrip("\ufeff")
                payload_data = json.loads(raw_str)
            except Exception:
                pass

    if not payload_data and not sys.stdin.isatty():
        try:
            if hasattr(sys.stdin, "buffer"):
                raw_stdin = sys.stdin.buffer.read()
            else:
                raw_stdin = sys.stdin.read().encode("utf-8")
            if raw_stdin.startswith(b"\xef\xbb\xbf"):
                raw_stdin = raw_stdin[3:]
            payload_data = json.loads(raw_stdin.decode("utf-8", errors="replace"))
        except Exception:
            pass

    action = args.action.lower()
    response = {"success": False, "action": action}

    try:
        if action == "ping":
            response = {"success": True, "action": "ping", "data": check_environment()}

        elif action == "transcribe":
            video_path = payload_data.get("video_path", "")
            api_key = payload_data.get("api_keys") or payload_data.get("api_key", "")
            model_name = payload_data.get("model_name", "gemini-3.5-flash")
            segments = transcribe_video(video_path, api_key, model_name)
            response = {"success": True, "action": "transcribe", "segments": segments}

        elif action == "translate":
            dialogue = payload_data.get("dialogue", [])
            api_key = payload_data.get("api_keys") or payload_data.get("api_key", "")
            context = payload_data.get("context", "")
            model_name = payload_data.get("model_name", "gemini-3.5-flash")
            translated, usage = translate_dialogue_batch(dialogue, api_key, context, model_name, return_usage=True)
            response = {"success": True, "action": "translate", "dialogue": translated, "usage": usage}

        elif action == "regenerate_line":
            chinese = payload_data.get("chinese", "")
            api_key = payload_data.get("api_key", "")
            speaker = payload_data.get("speaker", "Character")
            emotion = payload_data.get("emotion", "Normal")
            scene_context = payload_data.get("scene_context", "")
            model = payload_data.get("model_name", "gemini-3.5-flash")
            res = regenerate_single_line(chinese, api_key, speaker, emotion, scene_context, model)
            response = {"success": True, "action": "regenerate_line", "result": res}

        elif action == "tts_single":
            text = payload_data.get("text", "")
            output_path = payload_data.get("output_path", "")
            voice_id = payload_data.get("voice_id", "piset")
            speed = float(payload_data.get("speed", 1.0))
            volume = float(payload_data.get("volume", 1.0))
            pitch = int(payload_data.get("pitch", 0))
            res = synthesize_sentence(text, output_path, voice_id, speed, volume, pitch)
            response = {"success": res.get("success", False), "action": "tts_single", "data": res}

        elif action == "tts_batch":
            dialogue = payload_data.get("dialogue", [])
            output_dir = payload_data.get("output_dir", "")
            default_voice = payload_data.get("default_voice", "piset")
            res = synthesize_batch(dialogue, output_dir, default_voice)
            response = {"success": True, "action": "tts_batch", "results": res}

        elif action == "separate_audio":
            video_path = payload_data.get("video_path", "")
            output_dir = payload_data.get("output_dir", "")
            res = separate_audio_tracks(video_path, output_dir)
            response = {"success": res.get("success", False), "action": "separate_audio", "data": res}

        elif action == "generate_social":
            title = payload_data.get("movie_title", "Chinese Movie")
            summary = payload_data.get("summary", "")
            api_key = payload_data.get("api_key", "")
            genre = payload_data.get("genre", "Drama")
            model = payload_data.get("model_name", "gemini-3.5-flash")
            res = generate_social_copy(title, summary, api_key, genre, model)
            response = {"success": res.get("success", False), "action": "generate_social", "data": res.get("data")}

        else:
            response = {"success": False, "error": f"Unknown action: {action}"}

    except Exception as e:
        response = {"success": False, "error": str(e)}

    # Output strictly UTF-8 JSON
    print(json.dumps(response, ensure_ascii=False))
    sys.stdout.flush()

if __name__ == "__main__":
    main()
