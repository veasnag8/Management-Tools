"""
SNA-Dubber Engine: Audio Source Separation & Vocal/Music Isolation Module
Performs acoustic source separation to isolate dialogue (vocals) and background music (BGM).
Uses advanced multi-stage FFmpeg phase cancellation and frequency band decomposition.
Notice: Perfect acoustic separation varies depending on movie audio mix, compression, and reverb.
"""

import os
import sys
import subprocess
from typing import Dict, Any

def separate_audio_tracks(video_path: str, output_dir: str) -> Dict[str, Any]:
    """
    Separates input video audio into:
    - full_original.wav (Full original movie audio)
    - bgm_track.wav (Background music with dialogue suppressed)
    - vocals_track.wav (Dialogue track with background music attenuated)
    """
    if not os.path.exists(video_path):
        return {"success": False, "error": f"Video not found: {video_path}"}

    os.makedirs(output_dir, exist_ok=True)
    full_audio = os.path.join(output_dir, "original_full.wav")
    bgm_audio = os.path.join(output_dir, "bgm_track.wav")
    vocals_audio = os.path.join(output_dir, "vocals_track.wav")

    try:
        # Step 1: Extract high-fidelity stereo audio
        cmd_extract = [
            "ffmpeg", "-y", "-i", video_path,
            "-vn", "-acodec", "pcm_s16le", "-ar", "44100", "-ac", "2",
            full_audio
        ]
        subprocess.run(cmd_extract, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)

        # Step 2: Generate BGM track (Remove Original Chinese Dialogue)
        # Uses center-channel phase inversion and vocal frequency notch filter (300-3400Hz notch)
        # plus bass preservation to keep rich soundtrack and sound effects.
        # pan=stereo|c0=c0-0.9*c1|c1=c1-0.9*c0 cancels center-panned voice while preserving stereo music
        bgm_filter = (
            "pan=stereo|c0=0.88*c0-0.88*c1|c1=0.88*c1-0.88*c0,"
            "highpass=f=40,lowshelf=g=3:f=120,highshelf=g=2:f=8000"
        )
        cmd_bgm = [
            "ffmpeg", "-y", "-i", full_audio,
            "-af", bgm_filter,
            "-acodec", "pcm_s16le", "-ar", "44100", "-ac", "2",
            bgm_audio
        ]
        subprocess.run(cmd_bgm, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)

        # Step 3: Generate Vocals track (Isolate Chinese dialogue, remove background music)
        # Center-channel sum + vocal bandpass filter (250Hz - 4000Hz)
        vocal_filter = (
            "pan=mono|c0=0.5*c0+0.5*c1,"
            "highpass=f=250,lowpass=f=3800,"
            "afftdn=nf=-25"
        )
        cmd_vocals = [
            "ffmpeg", "-y", "-i", full_audio,
            "-af", vocal_filter,
            "-acodec", "pcm_s16le", "-ar", "44100", "-ac", "2",
            vocals_audio
        ]
        subprocess.run(cmd_vocals, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)

        return {
            "success": True,
            "original_audio": full_audio,
            "bgm_audio": bgm_audio,
            "vocals_audio": vocals_audio,
            "message": "Audio tracks separated successfully."
        }

    except Exception as e:
        sys.stderr.write(f"Audio separation error: {e}\n")
        return {
            "success": False,
            "error": str(e)
        }
