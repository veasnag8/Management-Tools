"""
SNA-Dubber Engine: AI Contextual Chinese-to-Khmer Translation Module
Integrates the Gemini API with Multi-Key Pooling & Intelligent Failover:
- Multi-Key Rotation & Load Balancing
- Auto-Failover when a key hits Quota/Rate Limit (429)
- Real Token Usage Extraction (usage_metadata)
- Original Chinese
- Draft Khmer (literal reference)
- Corrected Natural Khmer (fluent cinematic Khmer with scene, emotion, and character context)
With automatic high-accuracy Khmer web translation fallback.
"""

import sys
import json
import re
import urllib.request
import urllib.parse
import os
import tempfile
from typing import List, Dict, Any, Tuple, Optional

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

# Ensure UTF-8 streams
if sys.platform.startswith("win"):
    try:
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
        sys.stdin.reconfigure(encoding="utf-8")
    except Exception:
        pass

SYSTEM_INSTRUCTION = """
You are a master Chinese-to-Khmer movie dialogue translator and dubbing director.
Your mission is to translate spoken Chinese movie lines into completely natural, expressive, and fluent Khmer (ភាសាខ្មែររលូនបែបភាពយន្ត) suitable for professional voice acting and dubbing.

Contextual Translation Guidelines:
1. Context & Tone: Adapt vocabulary according to the movie genre (Historical, Romance, Action, Modern Drama, Suspense) and character status/relationships (Royal, Martial Arts, Friends, Lovers, Enemies, Elders).
2. Expressive Dialogue: Avoid robotic, word-for-word, or literal translations that sound awkward in Khmer.
   - For example, instead of translating "你没事吧？" literally as "អ្នកគ្មានរឿងអ្វីទេឬ?", use natural Khmer movie dialogue: "ឯងមិនអីទេមែនទេ?" or "បង/អូនកើតអីអត់?".
   - For emotional scenes, convey anger, tenderness, grief, or urgency naturally in spoken Khmer.
3. Pronouns & Honorifics: Use appropriate natural Khmer pronouns (បង, អូន, ឯង, ខ្ញុំ, លោក, អ្នកនាង, ព្រះអង្គ, មេទ័ព, etc.) rather than generic "អ្នក".
4. Names & Places: Keep Chinese character names recognizable and correctly phonetically transliterated into Khmer script.
5. Fidelity: Never invent plotlines or change the original intent of the scene. Preserve emotional weight, wit, humor, and punchlines.
6. Provide Two Translations for Every Line:
   - "draft_khmer": Direct/literal translation baseline.
   - "corrected_khmer": The polished, natural, cinema-grade Khmer dialogue for dubbing.
"""

def parse_api_keys(raw_input: Any) -> List[str]:
    """Extracts, cleans, and deduplicates Gemini API keys from string, list, or env."""
    keys: List[str] = []
    if not raw_input:
        env_val = os.environ.get("GEMINI_API_KEY", "")
        if env_val:
            raw_input = env_val

    if isinstance(raw_input, list):
        for item in raw_input:
            if isinstance(item, str):
                for part in re.split(r"[\r\n,;]+", item):
                    k = part.strip().strip("'\"`")
                    if k and not k.startswith("#") and len(k) > 10:
                        keys.append(k)
    elif isinstance(raw_input, str):
        for part in re.split(r"[\r\n,;]+", raw_input):
            k = part.strip().strip("'\"`")
            if k and not k.startswith("#") and len(k) > 10:
                keys.append(k)

    # Check local app data file if no keys given yet
    if not keys:
        try:
            local_appdata = os.environ.get("LOCALAPPDATA", "")
            if local_appdata:
                pool_file = os.path.join(local_appdata, "SNA-Dubber", "gemini_api_keys.txt")
                if os.path.exists(pool_file):
                    with open(pool_file, "r", encoding="utf-8") as pf:
                        for line in pf:
                            k = line.strip().strip("'\"`")
                            if k and not k.startswith("#") and len(k) > 10:
                                keys.append(k)
                if not keys:
                    single_file = os.path.join(local_appdata, "SNA-Dubber", "gemini_api_key.txt")
                    if os.path.exists(single_file):
                        with open(single_file, "r", encoding="utf-8") as sf:
                            k = sf.read().strip().strip("'\"`")
                            if k and len(k) > 10:
                                keys.append(k)
        except Exception:
            pass

    # Deduplicate while preserving order
    seen = set()
    unique_keys = []
    for k in keys:
        if k not in seen:
            seen.add(k)
            unique_keys.append(k)
    return unique_keys

class GeminiKeyPool:
    """Manages multiple Gemini API keys with round-robin rotation, automatic failover, and token tracking."""
    def __init__(self, raw_input: Any):
        self.keys = parse_api_keys(raw_input)
        self.exhausted_keys = set()
        self.current_idx = 0
        self.clients: Dict[str, Any] = {}
        self.total_prompt_tokens = 0
        self.total_candidates_tokens = 0
        self.total_tokens = 0
        self.last_model_used = ""
        self.genai_module = None
        self.types_module = None

        if self.keys:
            try:
                from google import genai
                from google.genai import types as gtypes
                self.genai_module = genai
                self.types_module = gtypes
            except Exception as e:
                sys.stderr.write(f"Failed to import google.genai: {e}\n")

    def has_keys(self) -> bool:
        return len(self.keys) > 0 and len(self.exhausted_keys) < len(self.keys)

    def get_client(self) -> Tuple[Any, Any, str]:
        """Returns (client, types, api_key) or (None, None, '') if exhausted."""
        if not self.has_keys() or not self.genai_module:
            return None, None, ""

        active_keys = [k for k in self.keys if k not in self.exhausted_keys]
        if not active_keys:
            return None, None, ""

        chosen_key = active_keys[self.current_idx % len(active_keys)]
        self.current_idx = (self.current_idx + 1) % len(active_keys)

        if chosen_key not in self.clients:
            try:
                self.clients[chosen_key] = self.genai_module.Client(api_key=chosen_key)
            except Exception as e:
                sys.stderr.write(f"Failed to initialize Gemini Client for key {chosen_key[:8]}...: {e}\n")
                self.exhausted_keys.add(chosen_key)
                return self.get_client()

        return self.clients[chosen_key], self.types_module, chosen_key

    def mark_exhausted(self, key: str, err: Exception) -> None:
        """Marks a key as quota-exhausted and prints failover notice."""
        self.exhausted_keys.add(key)
        masked = key[:6] + "..." + key[-4:] if len(key) > 10 else "***"
        sys.stderr.write(f"[KeyPool] Key {masked} exhausted quota. Switching to next key in pool ({len(self.keys) - len(self.exhausted_keys)} active left)...\n")
        if len(self.exhausted_keys) >= len(self.keys):
            _mark_quota_exhausted(err)

    def record_usage(self, response: Any, model_name: str) -> None:
        """Accumulates prompt, candidate, and total token usage from Gemini response."""
        self.last_model_used = model_name
        metadata = getattr(response, "usage_metadata", None)
        if metadata:
            p_tok = getattr(metadata, "prompt_token_count", 0) or 0
            c_tok = getattr(metadata, "candidates_token_count", 0) or 0
            t_tok = getattr(metadata, "total_token_count", 0) or (p_tok + c_tok)
            self.total_prompt_tokens += p_tok
            self.total_candidates_tokens += c_tok
            self.total_tokens += t_tok

    def get_summary(self) -> Dict[str, Any]:
        return {
            "total_tokens": self.total_tokens,
            "prompt_tokens": self.total_prompt_tokens,
            "completion_tokens": self.total_candidates_tokens,
            "total_keys": len(self.keys),
            "exhausted_keys": len(self.exhausted_keys),
            "active_keys": len(self.keys) - len(self.exhausted_keys),
            "last_model_used": self.last_model_used
        }

def free_translate_zh_to_km(text: str) -> str:
    """Translates Chinese text into Khmer using Google Translate web service as fallback."""
    clean = (text or "").strip()
    if not clean:
        return ""
    try:
        url = f"https://translate.googleapis.com/translate_a/single?client=gtx&sl=zh-CN&tl=km&dt=t&q={urllib.parse.quote(clean)}"
        req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"})
        with urllib.request.urlopen(req, timeout=10) as resp:
            data = json.loads(resp.read().decode("utf-8"))
            if data and isinstance(data, list) and len(data) > 0 and isinstance(data[0], list):
                translated_parts = [part[0] for part in data[0] if part and len(part) > 0 and part[0]]
                return "".join(translated_parts).strip()
    except Exception as e:
        sys.stderr.write(f"Free translate error for '{clean[:20]}': {e}\n")
    return ""

def free_translate_batch(chinese_texts: List[str]) -> List[str]:
    """Translates a batch of Chinese texts into Khmer."""
    results = []
    for t in chinese_texts:
        res = free_translate_zh_to_km(t)
        results.append(res if res else t)
    return results

def _extract_json_array(text: str) -> List[Dict[str, Any]]:
    """Safely extracts JSON array from model output."""
    raw = (text or "").strip()
    if raw.startswith("```json"):
        raw = raw[7:]
    if raw.startswith("```"):
        raw = raw[3:]
    if raw.endswith("```"):
        raw = raw[:-3]
    raw = raw.strip()

    try:
        data = json.loads(raw)
        if isinstance(data, list):
            return data
    except Exception:
        pass

    match = re.search(r"\[.*\]", raw, re.DOTALL)
    if match:
        try:
            data = json.loads(match.group(0))
            if isinstance(data, list):
                return data
        except Exception:
            pass

    return []

def _translate_chunk_with_pool(
    chunk: List[Dict[str, Any]],
    pool: GeminiKeyPool,
    movie_context: str,
    model_name: str
) -> Dict[int, Dict[str, str]]:
    """Translates a chunk using the Multi-Key Pool with automatic failover."""
    prompt_data = [
        {
            "i": item.get("index", idx + 1),
            "s": item.get("speaker", ""),
            "zh": item.get("chinese", ""),
            "km": item.get("draft_khmer", "")
        }
        for idx, item in enumerate(chunk)
    ]
    user_content = (
        f"Context:{movie_context}\n"
        f"Polish meaning and natural spoken Khmer:{json.dumps(prompt_data, ensure_ascii=False, separators=(',', ':'))}\n"
        "Return JSON only: [{\"index\":1,\"corrected_khmer\":\"...\"}]"
    )

    models_to_try = [
        model_name,
        "gemini-3.5-flash",
        "gemini-3.8-flash",
        "gemini-flash-latest",
        "gemini-flash-lite-latest",
        "gemini-3.1-flash-lite",
        "gemini-3.5-flash-lite",
    ]

    while pool.has_keys():
        client, types, key = pool.get_client()
        if not client or not types:
            break

        seen_models = set()
        chunk_quota_exhausted_on_key = False

        for m in models_to_try:
            if not m or m in seen_models:
                continue
            if "2.0" in m or "2.5" in m:
                continue
            seen_models.add(m)
            try:
                cfg = types.GenerateContentConfig(
                    system_instruction=SYSTEM_INSTRUCTION,
                    response_mime_type="application/json",
                    temperature=0.4
                )
                response = client.models.generate_content(
                    model=m,
                    contents=user_content,
                    config=cfg
                )
                items = _extract_json_array(response.text)
                if items:
                    pool.record_usage(response, m)
                    res_map = {}
                    for t in items:
                        if isinstance(t, dict) and "index" in t:
                            res_map[int(t["index"])] = t
                    return res_map
            except Exception as e:
                if _is_quota_error(e):
                    pool.mark_exhausted(key, e)
                    chunk_quota_exhausted_on_key = True
                    break # Break models loop to try NEXT KEY in pool!
                elif not _is_unavailable_model(e):
                    sys.stderr.write(f"Gemini model {m} chunk error: {e}\n")

        if not chunk_quota_exhausted_on_key:
            # Non-quota error, move to next key to give it a shot
            continue

    return {}

def translate_dialogue_batch(
    dialogue_items: List[Dict[str, Any]],
    api_key_or_keys: Any,
    movie_context: str = "Chinese Drama / Movie",
    model_name: str = "gemini-3.5-flash",
    return_usage: bool = False
) -> Any:
    """Translates dialogue lines from Chinese to natural Khmer with Multi-Key Pooling and intelligent fallback."""
    if not dialogue_items:
        return ([], {}) if return_usage else []

    pool = GeminiKeyPool(api_key_or_keys)

    # Produce the inexpensive literal baseline before calling Gemini.
    for item in dialogue_items:
        if not (item.get("draft_khmer") or "").strip():
            item["draft_khmer"] = free_translate_zh_to_km(item.get("chinese", ""))

    CHUNK_SIZE = 30
    gemini_map: Dict[int, Dict[str, str]] = {}

    if pool.has_keys():
        sys.stderr.write(f"[KeyPool] Translating {len(dialogue_items)} lines using {len(pool.keys)} Gemini API keys in pool...\n")
        for offset in range(0, len(dialogue_items), CHUNK_SIZE):
            chunk = dialogue_items[offset:offset + CHUNK_SIZE]
            chunk_results = _translate_chunk_with_pool(chunk, pool, movie_context, model_name)
            gemini_map.update(chunk_results)
    else:
        sys.stderr.write("[KeyPool] No active Gemini keys in pool. Using high-accuracy Khmer web fallback.\n")

    # Merge results and identify any lines needing fallback translation
    merged: List[Dict[str, Any]] = []
    lines_needing_fallback = []

    for idx, item in enumerate(dialogue_items):
        item_idx = item.get("index", idx + 1)
        chinese = item.get("chinese", "").strip()
        t = gemini_map.get(item_idx, {})

        draft = (item.get("draft_khmer", "") or "").strip()
        corrected = t.get("corrected_khmer", "").strip()
        notes = "Gemini natural-language review (Multi-Key Pool)"

        merged_item = dict(item)
        merged_item["index"] = item_idx
        merged_item["chinese"] = item.get("chinese", "")
        merged_item["notes"] = notes

        def is_valid_translation(txt: str) -> bool:
            if not txt:
                return False
            return bool(re.search(r"[\u1780-\u17FF]", txt))

        if is_valid_translation(corrected):
            merged_item["draft_khmer"] = draft if is_valid_translation(draft) else corrected
            merged_item["corrected_khmer"] = corrected
        else:
            lines_needing_fallback.append((idx, chinese))
            merged_item["draft_khmer"] = ""
            merged_item["corrected_khmer"] = ""

        merged.append(merged_item)

    # Perform Google Translate fallback on any missing/failed lines
    if lines_needing_fallback:
        sys.stderr.write(f"Using high-accuracy Khmer web fallback for {len(lines_needing_fallback)} lines...\n")
        for orig_idx, ch_text in lines_needing_fallback:
            if ch_text:
                km = free_translate_zh_to_km(ch_text)
                if not km:
                    km = ch_text
                merged[orig_idx]["draft_khmer"] = km
                merged[orig_idx]["corrected_khmer"] = km
                merged[orig_idx]["notes"] = "Translated via high-accuracy Khmer fallback engine"
            else:
                merged[orig_idx]["draft_khmer"] = ""
                merged[orig_idx]["corrected_khmer"] = ""

    usage_summary = pool.get_summary()

    if return_usage:
        return merged, usage_summary
    return merged

def regenerate_single_line(
    chinese_text: str,
    api_key_or_keys: Any,
    speaker: str = "Character",
    emotion: str = "Normal",
    scene_context: str = "",
    model_name: str = "gemini-3.5-flash"
) -> Dict[str, str]:
    """Regenerates translation for a single dialogue line."""
    items = [{
        "index": 1,
        "speaker": speaker,
        "chinese": chinese_text,
        "emotion": emotion
    }]
    ctx = scene_context or "Chinese Movie Dialogue"
    res = translate_dialogue_batch(items, api_key_or_keys, ctx, model_name, return_usage=False)
    if res and len(res) > 0:
        return {
            "chinese": chinese_text,
            "draft_khmer": res[0].get("draft_khmer", ""),
            "corrected_khmer": res[0].get("corrected_khmer", "")
        }
    
    fallback_km = free_translate_zh_to_km(chinese_text) or chinese_text
    return {
        "chinese": chinese_text,
        "draft_khmer": fallback_km,
        "corrected_khmer": fallback_km
    }
