"""
SNA-Dubber Engine: Social Media Metadata & AI Copywriting Module
Generates high-engagement titles, captions, and viral hashtags tailored for:
- TikTok
- Facebook Reels / Facebook Video
- YouTube Shorts / YouTube Video
in both Khmer and English.
"""

import sys
import json
from typing import Dict, Any

def generate_social_copy(
    movie_title: str,
    dialogue_summary: str,
    api_key: str,
    genre: str = "Drama / Martial Arts",
    model_name: str = "gemini-2.5-flash"
) -> Dict[str, Any]:
    """Generates platform-specific viral titles, descriptions, and hashtags using Gemini."""
    try:
        from google import genai
        from google.genai import types

        client = genai.Client(api_key=api_key)

        prompt = (
            f"You are an expert social media manager and movie content creator in Cambodia. "
            f"Generate viral, click-worthy titles, descriptions, and trending hashtags for a Chinese drama/movie clip dubbed in Khmer.\n\n"
            f"Movie Title: {movie_title}\n"
            f"Genre: {genre}\n"
            f"Clip Summary / Dialogue: {dialogue_summary}\n\n"
            f"Generate tailored metadata for 3 platforms:\n"
            f"1. TikTok (Catchy, punchy Khmer hook, short cliffhanger caption, viral tags)\n"
            f"2. Facebook (Emotional Khmer story angle, questions to spark comments, hashtags)\n"
            f"3. YouTube (High-CTR SEO title with episode hook, detailed description, search tags)\n\n"
            f"Output strictly a raw JSON object with the following schema:\n"
            f"{{\n"
            f"  \"tiktok\": {{\n"
            f"    \"title\": \"Catchy Khmer Title\",\n"
            f"    \"caption\": \"Short punchy caption in Khmer with emojis\",\n"
            f"    \"hashtags\": \"#រឿងភាគចិន #សម្រាយរឿង #snadubber #tiktokkhmer #movie\"\n"
            f"  }},\n"
            f"  \"facebook\": {{\n"
            f"    \"title\": \"Engaging Facebook Post Title\",\n"
            f"    \"caption\": \"Emotional story teaser in Khmer urging viewers to watch and share\",\n"
            f"    \"hashtags\": \"#សម្រាយរឿងចិន #រឿងពេញ #khmerdubbed #fbreels\"\n"
            f"  }},\n"
            f"  \"youtube\": {{\n"
            f"    \"title\": \"High CTR YouTube Title (Khmer & English)\",\n"
            f"    \"description\": \"Comprehensive video synopsis, timestamp guide, and call to subscribe\",\n"
            f"    \"tags\": \"chinesedrama, khmer dubbing, snadubber, action movie\"\n"
            f"  }}\n"
            f"}}"
        )

        response = client.models.generate_content(
            model=model_name,
            contents=prompt,
            config=types.GenerateContentConfig(
                temperature=0.7
            )
        )

        text = response.text.strip()
        if text.startswith("```json"):
            text = text[7:]
        if text.startswith("```"):
            text = text[3:]
        if text.endswith("```"):
            text = text[:-3]
        text = text.strip()

        data = json.loads(text)
        return {"success": True, "data": data}

    except Exception as e:
        sys.stderr.write(f"Gemini social metadata error: {e}\n")
        return {
            "success": False,
            "error": str(e),
            "data": {
                "tiktok": {
                    "title": f"{movie_title} ភាគថ្មីល្អមើលខ្លាំង",
                    "caption": f"មើលឈុតពិសេសរឿង {movie_title} បញ្ចូលសំឡេងខ្មែរពិរោះដោយ SNA-Dubber!",
                    "hashtags": "#រឿងភាគចិន #សម្រាយរឿង #snadubber #khmermovie"
                },
                "facebook": {
                    "title": f"ឈុតឆាកជក់ចិត្តរឿង {movie_title} បញ្ចូលសំឡេងខ្មែរ",
                    "caption": f"សាច់រឿងកំពុងតែដល់វគ្គល្អមើល កុំភ្លេចចុច Like & Share ដើម្បីទទួលបានភាគបន្តទៀត!",
                    "hashtags": "#សម្រាយរឿង #រឿងភាគចិន #snadubber #fbreels"
                },
                "youtube": {
                    "title": f"{movie_title} - សម្រាយរឿងភាគចិន និយាយខ្មែរ Full HD",
                    "description": f"ទស្សនារឿង {movie_title} បញ្ចូលសំឡេងខ្មែរដោយក្រុមការងារ SNA-Dubber។ សូមចុច Subscribe ដើម្បីកុំអោយរំលងភាគថ្មីៗ!",
                    "tags": "chinesedrama, khmer dubbing, snadubber, movie"
                }
            }
        }
