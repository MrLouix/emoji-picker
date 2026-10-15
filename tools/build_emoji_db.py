#!/usr/bin/env python3
"""
Rebuilds src/EmojiPick/Data/emojis.json.gzip with Unicode CLDR keywords.

The original database only had the words of each English name as tags, so searching
"happy" or "love" found nothing useful. This script:
  * keeps one fully-qualified entry per emoji (no unqualified duplicates, no skin-tone variants),
  * adds CLDR keywords for every language given (English + French by default),
  * adds the localized short name under "names" (shown in the picker footer),
  * folds tags to lowercase ASCII-like form (accents removed) so "fete" matches "fête".

Usage (CLDR JSON from https://github.com/unicode-org/cldr-json, e.g. tag 46.0.0):
  python3 tools/build_emoji_db.py <cldr-dir> [lang ...]
where <cldr-dir> contains ann-<lang>.json (cldr-annotations-full/annotations/<lang>/annotations.json)
and der-<lang>.json (cldr-annotations-derived-full/annotationsDerived/<lang>/annotations.json).
"""
import gzip
import json
import re
import sys
import unicodedata
from pathlib import Path

DB = Path(__file__).resolve().parent.parent / "src" / "EmojiPick" / "Data" / "emojis.json.gzip"
VS16 = "️"
EXCLUDED_STATUSES = {"minimally-qualified", "unqualified", "component"}
STOP_WORDS = {
    "a", "an", "and", "the", "of", "with", "in", "on", "at", "to", "for", "or",
    "le", "la", "les", "de", "des", "du", "un", "une", "et", "en", "au", "aux", "d", "l",
}


def fold(text: str) -> str:
    text = text.lower().replace("œ", "oe").replace("æ", "ae").replace("’", "'")
    text = unicodedata.normalize("NFKD", text)
    return "".join(c for c in text if not unicodedata.combining(c))


def words(text: str):
    for word in re.split(r"[^\w]+", fold(text)):
        if len(word) >= 2 and word not in STOP_WORDS and not word.isdigit():
            yield word


def load_annotations(cldr_dir: Path, lang: str) -> dict:
    merged = {}
    for name, root in ((f"der-{lang}.json", "annotationsDerived"), (f"ann-{lang}.json", "annotations")):
        path = cldr_dir / name
        if not path.exists():
            continue
        data = json.loads(path.read_text(encoding="utf-8"))[root]["annotations"]
        for key, value in data.items():
            merged[key.replace(VS16, "")] = value
    return merged


def main() -> None:
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    cldr_dir = Path(sys.argv[1])
    langs = sys.argv[2:] or ["en", "fr"]
    annotations = {lang: load_annotations(cldr_dir, lang) for lang in langs}

    entries = json.loads(gzip.decompress(DB.read_bytes()).decode("utf-8"))
    output, seen, enriched = [], set(), 0

    for entry in entries:
        char = entry.get("sequence") or ""
        if not char or char in seen:
            continue
        if entry.get("category") in EXCLUDED_STATUSES or "skin tone" in entry.get("name", ""):
            continue
        seen.add(char)

        tags = list(dict.fromkeys(words(entry["name"])))
        names = {}
        key = char.replace(VS16, "")
        for lang, table in annotations.items():
            ann = table.get(key)
            if not ann:
                continue
            for keyword in ann.get("default", []) + ann.get("tts", []):
                for word in words(keyword):
                    if word not in tags:
                        tags.append(word)
            if lang != "en" and ann.get("tts"):
                names[lang] = ann["tts"][0]
        if len(tags) > len(list(words(entry["name"]))):
            enriched += 1

        item = dict(entry)
        item["tags"] = tags
        if names:
            item["names"] = names
        output.append(item)

    payload = json.dumps(output, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    DB.write_bytes(gzip.compress(payload, compresslevel=9, mtime=0))
    print(f"{len(output)} emoji written ({enriched} enriched with CLDR keywords) -> {DB}")


if __name__ == "__main__":
    main()
