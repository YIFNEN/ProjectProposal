"""
Prepare AO song assets from local WAV files.

This script does not download or extract from streaming sites. Put WAV files you
are allowed to use into Assets/_Project/Audio/BGM first, then run this script to
create previews and beatmaps.
"""

from __future__ import annotations

import argparse
import os
import subprocess
import sys
import wave
from dataclasses import dataclass


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
EXTRACTOR = os.path.join(ROOT, "Tools", "Beatmap", "extract_beatmap.py")
PREVIEW_SECONDS = 10.0
PREVIEW_START_SECONDS = 30.0


@dataclass(frozen=True)
class SongSpec:
    song_id: str
    display_name: str
    wav_name: str
    preview_name: str
    beatmap_name: str
    source_url: str


SONGS = [
    SongSpec(
        "twinklestar",
        "Snail's House - Twinklestar",
        "Twinklestar.wav",
        "Twinklestar_Preview.wav",
        "Twinklestar_Normal.json",
        "https://www.youtube.com/watch?v=myiJB8SiIcU",
    ),
    SongSpec(
        "utakata",
        "Snail's House - Utakata",
        "Utakata.wav",
        "Utakata_Preview.wav",
        "Utakata_Normal.json",
        "https://www.youtube.com/watch?v=QDCe1_SzHAc",
    ),
    SongSpec(
        "shinkai_shoujo",
        "ゆうゆ feat. 初音ミク - 深海少女",
        "ShinkaiShoujo.wav",
        "ShinkaiShoujo_Preview.wav",
        "ShinkaiShoujo_Normal.json",
        "https://www.youtube.com/watch?v=2CwBFr-Eoxg",
    ),
]


def main() -> int:
    parser = argparse.ArgumentParser(description="Prepare AO previews and beatmaps from local WAV files.")
    parser.add_argument("--song", choices=[song.song_id for song in SONGS] + ["all"], default="all")
    parser.add_argument("--preview-start", type=float, default=PREVIEW_START_SECONDS)
    parser.add_argument("--preview-seconds", type=float, default=PREVIEW_SECONDS)
    parser.add_argument("--python", default=sys.executable)
    parser.add_argument("--skip-beatmap", action="store_true")
    args = parser.parse_args()

    selected = SONGS if args.song == "all" else [song for song in SONGS if song.song_id == args.song]
    missing = []

    for spec in selected:
        wav_path = os.path.join(ROOT, "Assets", "_Project", "Audio", "BGM", spec.wav_name)
        preview_path = os.path.join(ROOT, "Assets", "_Project", "Audio", "BGM", "Previews", spec.preview_name)
        beatmap_path = os.path.join(ROOT, "Assets", "_Project", "Beatmaps", spec.beatmap_name)

        if not os.path.exists(wav_path):
            missing.append((spec, wav_path))
            continue

        write_preview(wav_path, preview_path, args.preview_start, args.preview_seconds)
        print(f"[preview] {preview_path}")

        if not args.skip_beatmap:
            run_extractor(args.python, wav_path, beatmap_path, spec.display_name)
            print(f"[beatmap] {beatmap_path}")

    if missing:
        print("\nMissing local WAV files:")
        for spec, path in missing:
            print(f"  - {spec.display_name}: {path}")
            print(f"    source reference: {spec.source_url}")
        return 2

    print("\nDone. Run Unity menu AO/Songs/Refresh Default Song Library after Unity imports the files.")
    return 0


def write_preview(src: str, dst: str, start_seconds: float, duration_seconds: float) -> None:
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    with wave.open(src, "rb") as reader:
        params = reader.getparams()
        sample_rate = reader.getframerate()
        total = reader.getnframes()
        start = min(max(0, int(start_seconds * sample_rate)), max(0, total - 1))
        count = min(max(1, int(duration_seconds * sample_rate)), total - start)
        reader.setpos(start)
        frames = reader.readframes(count)

    with wave.open(dst, "wb") as writer:
        writer.setparams(params)
        writer.writeframes(frames)


def run_extractor(python: str, wav_path: str, beatmap_path: str, display_name: str) -> None:
    os.makedirs(os.path.dirname(beatmap_path), exist_ok=True)
    command = [
        python,
        EXTRACTOR,
        wav_path,
        beatmap_path,
        "--song-name",
        display_name,
        "--enable-double-notes",
        "--up-threshold",
        "3.0",
        "--down-threshold",
        "3.0",
        "--center-threshold",
        "1.5",
        "--dir-threshold",
        "2.0",
        "--sustain-threshold-semi",
        "3.0",
        "--sustain-step-threshold-semi",
        "2.0",
        "--sustain-variance-threshold",
        "2.3",
        "--sustain-min-beats",
        "3",
        "--fish-min-gap-beats",
        "16",
        "--fish-variants",
        "fish_cyan,fish_gold,fish_pink",
    ]
    subprocess.run(command, cwd=ROOT, check=True)


if __name__ == "__main__":
    raise SystemExit(main())
