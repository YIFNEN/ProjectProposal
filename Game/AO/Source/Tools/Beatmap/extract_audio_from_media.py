"""
Extract AO-ready WAV files from media files.

This helper passes a local path or direct HTTP(S) media URL to ffmpeg and writes
an AO-ready WAV file.
"""

from __future__ import annotations

import argparse
import os
import subprocess
import sys
from dataclasses import dataclass


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
PREPARE_SCRIPT = os.path.join(ROOT, "Tools", "Beatmap", "prepare_song_assets.py")
BGM_DIR = os.path.join(ROOT, "Assets", "_Project", "Audio", "BGM")


@dataclass(frozen=True)
class SongTarget:
    song_id: str
    display_name: str
    wav_name: str


SONGS = {
    "twinklestar": SongTarget("twinklestar", "Snail's House - Twinklestar", "Twinklestar.wav"),
    "utakata": SongTarget("utakata", "Snail's House - Utakata", "Utakata.wav"),
    "shinkai_shoujo": SongTarget(
        "shinkai_shoujo",
        "ゆうゆ feat. 初音ミク - 深海少女",
        "ShinkaiShoujo.wav",
    ),
}


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Convert media files into AO BGM WAV files, optionally preparing previews and beatmaps."
    )
    parser.add_argument("--song", choices=sorted(SONGS), help="Target built-in song id.")
    parser.add_argument("--input", help="Source media path or direct HTTP(S) media URL for --song.")
    parser.add_argument("--twinklestar", help="Media path or direct HTTP(S) media URL for Twinklestar.")
    parser.add_argument("--utakata", help="Media path or direct HTTP(S) media URL for Utakata.")
    parser.add_argument(
        "--shinkai-shoujo",
        dest="shinkai_shoujo",
        help="Media path or direct HTTP(S) media URL for Shinkai Shoujo.",
    )
    parser.add_argument("--output", help="Optional explicit output WAV path. Only valid with --song.")
    parser.add_argument("--ffmpeg", default="ffmpeg", help="ffmpeg executable path.")
    parser.add_argument("--sample-rate", type=int, default=44100)
    parser.add_argument("--channels", type=int, default=2)
    parser.add_argument("--overwrite", action="store_true")
    parser.add_argument("--prepare", action="store_true", help="Run prepare_song_assets.py after conversion.")
    parser.add_argument("--skip-beatmap", action="store_true", help="Pass --skip-beatmap to prepare_song_assets.py.")
    parser.add_argument("--python", default=sys.executable)
    args = parser.parse_args()

    jobs = collect_jobs(args)
    if not jobs:
        parser.error("Provide either --song with --input, or one or more batch inputs such as --twinklestar.")

    for song, source, output in jobs:
        convert_to_wav(
            ffmpeg=args.ffmpeg,
            source=source,
            output=output,
            sample_rate=args.sample_rate,
            channels=args.channels,
            overwrite=args.overwrite,
        )
        print(f"[audio] {song.display_name}: {output}")

    if args.prepare:
        for song, _, _ in jobs:
            run_prepare(args.python, song.song_id, args.skip_beatmap)

    print("\nDone.")
    if args.prepare:
        print("Run Unity menu AO/Songs/Refresh Default Song Library after Unity imports the files.")
    else:
        print("Next: run python Tools\\Beatmap\\prepare_song_assets.py --song all")

    return 0


def collect_jobs(args: argparse.Namespace) -> list[tuple[SongTarget, str, str]]:
    jobs: list[tuple[SongTarget, str, str]] = []

    if args.song or args.input or args.output:
        if not args.song or not args.input:
            raise SystemExit("--song mode requires both --song and --input.")
        song = SONGS[args.song]
        output = os.path.abspath(args.output) if args.output else os.path.join(BGM_DIR, song.wav_name)
        jobs.append((song, normalize_source(args.input), output))

    batch_inputs = {
        "twinklestar": args.twinklestar,
        "utakata": args.utakata,
        "shinkai_shoujo": args.shinkai_shoujo,
    }
    for song_id, source in batch_inputs.items():
        if not source:
            continue
        song = SONGS[song_id]
        jobs.append((song, normalize_source(source), os.path.join(BGM_DIR, song.wav_name)))

    if args.output and len(jobs) != 1:
        raise SystemExit("--output can only be used with a single --song/--input job.")

    return jobs


def normalize_source(source: str) -> str:
    if looks_like_url(source):
        return source

    path = os.path.abspath(os.path.expanduser(source))
    if not os.path.isfile(path):
        raise SystemExit(f"Source file does not exist: {path}")
    return path


def looks_like_url(value: str) -> bool:
    lowered = value.lower()
    return lowered.startswith("http://") or lowered.startswith("https://")


def convert_to_wav(
    *,
    ffmpeg: str,
    source: str,
    output: str,
    sample_rate: int,
    channels: int,
    overwrite: bool,
) -> None:
    os.makedirs(os.path.dirname(output), exist_ok=True)
    if os.path.exists(output) and not overwrite:
        print(f"[skip] WAV already exists, use --overwrite to replace: {output}")
        return

    command = [
        ffmpeg,
        "-hide_banner",
        "-loglevel",
        "error",
        "-y" if overwrite else "-n",
        "-i",
        source,
        "-vn",
        "-acodec",
        "pcm_s16le",
        "-ar",
        str(sample_rate),
        "-ac",
        str(channels),
        output,
    ]
    subprocess.run(command, cwd=ROOT, check=True)


def run_prepare(python: str, song_id: str, skip_beatmap: bool) -> None:
    command = [python, PREPARE_SCRIPT, "--song", song_id]
    if skip_beatmap:
        command.append("--skip-beatmap")
    subprocess.run(command, cwd=ROOT, check=True)


if __name__ == "__main__":
    raise SystemExit(main())
