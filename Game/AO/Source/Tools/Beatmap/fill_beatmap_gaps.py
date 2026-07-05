"""
Fill sparse AO beatmap gaps on the song BPM grid.

This is intended as a conservative post-process for songs where beat tracking
misses quiet intros, interludes, or outros. It preserves existing notes and
adds Bubble notes only when the BPM-grid candidate is far enough from existing
hit times. When an audio WAV is supplied, candidates in near-silent sections are
skipped by RMS threshold.
"""

from __future__ import annotations

import argparse
import bisect
import json
import math
import os
import struct
import wave
from dataclasses import dataclass
from typing import Any


NOTE_BUBBLE = 0
NOTE_FISH = 1

LANE_NAMES = {
    "up": 0,
    "down": 1,
    "left": 2,
    "right": 3,
    "center": 4,
}


@dataclass
class AudioRmsProbe:
    path: str
    sample_rate: int
    channels: int
    sample_width: int
    total_frames: int

    @classmethod
    def open(cls, path: str) -> "AudioRmsProbe":
        with wave.open(path, "rb") as reader:
            return cls(
                path=path,
                sample_rate=reader.getframerate(),
                channels=reader.getnchannels(),
                sample_width=reader.getsampwidth(),
                total_frames=reader.getnframes(),
            )

    def rms_at(self, time_seconds: float, window_seconds: float) -> float:
        if self.sample_width != 2:
            return 1.0

        half = window_seconds * 0.5
        start = max(0, int((time_seconds - half) * self.sample_rate))
        count = max(1, int(window_seconds * self.sample_rate))
        count = min(count, max(1, self.total_frames - start))

        with wave.open(self.path, "rb") as reader:
            reader.setpos(start)
            raw = reader.readframes(count)

        sample_count = len(raw) // self.sample_width
        if sample_count <= 0:
            return 0.0

        values = struct.unpack("<" + "h" * sample_count, raw)
        if self.channels > 1:
            values = values[:: self.channels]

        if not values:
            return 0.0

        mean_square = sum(value * value for value in values) / len(values)
        return math.sqrt(mean_square) / 32768.0


def parse_lane(value: Any) -> int:
    if isinstance(value, int):
        return value
    if isinstance(value, str):
        stripped = value.strip()
        if stripped.isdigit():
            return int(stripped)
        return LANE_NAMES.get(stripped.lower(), 4)
    return 4


def is_fish(note: dict[str, Any]) -> bool:
    note_type = note.get("Type", NOTE_BUBBLE)
    return note_type == NOTE_FISH or note_type == "Fish"


def parse_lane_sequence(raw: str) -> list[int]:
    lanes: list[int] = []
    for part in raw.split(","):
        token = part.strip()
        if not token:
            continue
        lanes.append(parse_lane(token))
    return lanes or [0, 3, 4, 2, 1]


def nearest_distance(sorted_times: list[float], value: float) -> float:
    index = bisect.bisect_left(sorted_times, value)
    best = float("inf")
    if index < len(sorted_times):
        best = min(best, abs(sorted_times[index] - value))
    if index > 0:
        best = min(best, abs(sorted_times[index - 1] - value))
    return best


def surrounding_gap(sorted_times: list[float], value: float) -> float:
    index = bisect.bisect_left(sorted_times, value)
    prev_time = sorted_times[index - 1] if index > 0 else None
    next_time = sorted_times[index] if index < len(sorted_times) else None

    if prev_time is None and next_time is None:
        return float("inf")
    if prev_time is None or next_time is None:
        return float("inf")
    return next_time - prev_time


def lane_blocked_by_fish(lane: int, time_seconds: float, fish_windows: list[tuple[float, float, int]]) -> bool:
    for start, end, fish_lane in fish_windows:
        if lane == fish_lane and start < time_seconds < end:
            return True
    return False


def choose_lane(
    grid_index: int,
    time_seconds: float,
    sequence: list[int],
    fish_windows: list[tuple[float, float, int]],
) -> int | None:
    for offset in range(len(sequence)):
        lane = sequence[(grid_index + offset) % len(sequence)]
        if not lane_blocked_by_fish(lane, time_seconds, fish_windows):
            return lane
    return None


def generate_grid_times(anchor: float, interval: float, start: float, end: float) -> list[tuple[int, float]]:
    first_index = math.ceil((start - anchor) / interval)
    result: list[tuple[int, float]] = []
    index = first_index
    while True:
        time_seconds = anchor + index * interval
        if time_seconds > end + 1e-6:
            break
        if time_seconds >= start - 1e-6:
            result.append((index, time_seconds))
        index += 1
    return result


def fill(args: argparse.Namespace) -> dict[str, Any]:
    with open(args.input, "r", encoding="utf-8") as file:
        beatmap = json.load(file)

    notes = list(beatmap.get("Notes", []))
    notes.sort(key=lambda note: (float(note.get("HitTime", 0.0)), parse_lane(note.get("Lane", 4))))

    if not notes:
        raise RuntimeError("Beatmap has no notes to anchor the BPM grid.")

    bpm = float(beatmap.get("Bpm", 0.0))
    if bpm <= 0:
        raise RuntimeError("Beatmap BPM must be greater than zero.")

    song_length = float(beatmap.get("SongLengthSeconds", 0.0))
    if song_length <= 0:
        song_length = max(float(note.get("HitTime", 0.0)) for note in notes)

    interval = 60.0 / bpm
    hit_times = sorted(float(note.get("HitTime", 0.0)) for note in notes)
    anchor = hit_times[0]
    start = max(0.0, args.min_time)
    end = song_length if args.max_time <= 0 else min(song_length, args.max_time)
    lanes = parse_lane_sequence(args.lane_sequence)

    fish_windows: list[tuple[float, float, int]] = []
    for note in notes:
        if not is_fish(note):
            continue
        start_time = float(note.get("HitTime", 0.0))
        end_time = start_time + max(0.0, float(note.get("Duration", 0.0))) + args.fish_safety_extra_seconds
        fish_windows.append((start_time, end_time, parse_lane(note.get("Lane", 4))))

    audio_probe = AudioRmsProbe.open(args.audio) if args.audio else None
    added: list[dict[str, Any]] = []

    for grid_index, time_seconds in generate_grid_times(anchor, interval, start, end):
        if nearest_distance(hit_times, time_seconds) < args.min_spacing_seconds:
            continue
        if surrounding_gap(hit_times, time_seconds) < args.min_gap_seconds:
            continue
        if audio_probe is not None:
            rms = audio_probe.rms_at(time_seconds, args.audio_window_seconds)
            if rms < args.audio_rms_threshold:
                continue

        lane = choose_lane(grid_index, time_seconds, lanes, fish_windows)
        if lane is None:
            continue

        note = {
            "Type": NOTE_BUBBLE,
            "Lane": lane,
            "HitTime": float(time_seconds),
            "Duration": 0.0,
        }
        added.append(note)
        bisect.insort(hit_times, time_seconds)

    output_notes = notes + added
    output_notes.sort(key=lambda note: (float(note.get("HitTime", 0.0)), parse_lane(note.get("Lane", 4)), parse_lane(note.get("Type", 0))))
    beatmap["Notes"] = output_notes

    os.makedirs(os.path.dirname(os.path.abspath(args.output)), exist_ok=True)
    with open(args.output, "w", encoding="utf-8") as file:
        json.dump(beatmap, file, indent=2, ensure_ascii=False)
        file.write("\n")

    return {
        "input_notes": len(notes),
        "added_notes": len(added),
        "output_notes": len(output_notes),
        "first_added": min((note["HitTime"] for note in added), default=None),
        "last_added": max((note["HitTime"] for note in added), default=None),
    }


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Fill sparse AO beatmap gaps on the BPM grid.")
    parser.add_argument("input", help="Input beatmap JSON")
    parser.add_argument("output", help="Output beatmap JSON")
    parser.add_argument("--audio", default="", help="Optional WAV path for RMS activity gating.")
    parser.add_argument("--audio-rms-threshold", type=float, default=0.015)
    parser.add_argument("--audio-window-seconds", type=float, default=1.0)
    parser.add_argument("--min-gap-seconds", type=float, default=1.25)
    parser.add_argument("--min-spacing-seconds", type=float, default=0.32)
    parser.add_argument("--min-time", type=float, default=0.0)
    parser.add_argument("--max-time", type=float, default=0.0, help="0 means song length.")
    parser.add_argument("--fish-safety-extra-seconds", type=float, default=0.15)
    parser.add_argument("--lane-sequence", default="Up,Right,Center,Left,Down")
    return parser


def main() -> int:
    args = build_parser().parse_args()
    stats = fill(args)
    print(f"Wrote: {args.output}")
    print(f"  Input notes : {stats['input_notes']}")
    print(f"  Added notes : {stats['added_notes']}")
    print(f"  Output notes: {stats['output_notes']}")
    if stats["first_added"] is not None:
        print(f"  Added range : {stats['first_added']:.2f}s - {stats['last_added']:.2f}s")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
