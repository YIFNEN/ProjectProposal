"""
AO beatmap extractor (librosa only).

This version restores the old A+B+C feel and removes the custom beat grid.

Algorithm:
    1. Use librosa.beat.beat_track for beat timestamps and BPM.
    2. Sample spectral centroid at each beat and convert it to semitone.
    3. Assign lanes by local median pitch contour on the 5-lane order:
       Down -> Left -> Center -> Right -> Up.
    4. Apply a one-step direction bonus when pitch rises/falls from the previous beat.
    5. Apply anti-stack when the same lane repeats too many times.
    6. Detect Fish from sustained pitch runs, keep Duration, and place Fish on its
       actual melody lane.
    7. Optionally add a second simultaneous Bubble on strong onset/RMS beats.
    8. Remove same-lane notes during Fish Duration + safety seconds.

Usage:
    python extract_beatmap.py <input.wav> <output.json> [--song-name "Title"]
"""

from __future__ import annotations

import argparse
import json
import os
import sys
from dataclasses import dataclass
from typing import Iterable

import librosa
import numpy as np
import soundfile as sf
from scipy.signal import resample_poly

if sys.stdout.encoding and sys.stdout.encoding.lower() != "utf-8":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


LANE_UP = 0
LANE_DOWN = 1
LANE_LEFT = 2
LANE_RIGHT = 3
LANE_CENTER = 4
LANE_NONE = 5

NOTE_BUBBLE = 0
NOTE_FISH = 1

LANE_NAMES = {
    LANE_UP: "Up",
    LANE_DOWN: "Down",
    LANE_LEFT: "Left",
    LANE_RIGHT: "Right",
    LANE_CENTER: "Center",
    LANE_NONE: "None",
}

LANES_LOW_TO_HIGH = [LANE_DOWN, LANE_LEFT, LANE_CENTER, LANE_RIGHT, LANE_UP]
LANE_INDEX_IN_ORDER = {lane: index for index, lane in enumerate(LANES_LOW_TO_HIGH)}
LANE_DELTA_CENTERS = {
    LANE_DOWN: -3.0,
    LANE_LEFT: -1.0,
    LANE_CENTER: 0.0,
    LANE_RIGHT: 1.0,
    LANE_UP: 3.0,
}


@dataclass
class Candidate:
    time: float
    note_type: int
    lane: int
    duration: float
    confidence: float
    pitch: float
    source: str
    beat_index: int
    is_double: bool = False


@dataclass
class LaneAssignment:
    lanes: list[int]
    local_medians: list[float]
    delta_medians: list[float]
    delta_dirs: list[float]
    direction_applied: int
    anti_stack_triggered: int


def hz_to_midi(hz: float) -> float:
    hz = max(float(hz), 1.0)
    return float(12.0 * np.log2(hz / 440.0) + 69.0)


def normalize(values: np.ndarray, lo: float = 5.0, hi: float = 95.0) -> np.ndarray:
    if values.size == 0:
        return values.astype(float)

    low = float(np.percentile(values, lo))
    high = float(np.percentile(values, hi))
    if high <= low:
        return np.zeros_like(values, dtype=float)

    return np.clip((values - low) / (high - low), 0.0, 1.0)


def load_audio_mono(audio_path: str, target_sr: int | None) -> tuple[np.ndarray, int, float]:
    data, native_sr = sf.read(audio_path, dtype="float32", always_2d=True)
    duration = data.shape[0] / native_sr
    y = np.mean(data, axis=1).astype(np.float32)

    if target_sr is not None and target_sr > 0 and target_sr != native_sr:
        divisor = int(np.gcd(native_sr, target_sr))
        y = resample_poly(y, target_sr // divisor, native_sr // divisor).astype(np.float32)
        return y, target_sr, duration

    return y, native_sr, duration


def value_at_time(times: np.ndarray, values: np.ndarray, time: float, window: float = 0.06) -> float:
    if values.size == 0:
        return 0.0

    mask = (times >= time - window) & (times <= time + window)
    if mask.any():
        return float(np.max(values[mask]))

    index = int(np.argmin(np.abs(times - time)))
    return float(values[index])


def beat_pitches(y: np.ndarray, sr: int, beat_times: np.ndarray, hop_length: int) -> np.ndarray:
    centroid_frames = librosa.feature.spectral_centroid(y=y, sr=sr, hop_length=hop_length)[0]
    centroid_times = librosa.frames_to_time(
        np.arange(len(centroid_frames)),
        sr=sr,
        hop_length=hop_length,
    )

    pitches_hz = np.zeros(len(beat_times), dtype=float)
    for index, beat_time in enumerate(beat_times):
        mask = (centroid_times >= beat_time - 0.1) & (centroid_times <= beat_time + 0.1)
        if mask.any():
            pitches_hz[index] = float(np.mean(centroid_frames[mask]))
        else:
            nearest = int(np.argmin(np.abs(centroid_times - beat_time)))
            pitches_hz[index] = float(centroid_frames[nearest])

    return np.asarray([hz_to_midi(value) for value in pitches_hz], dtype=float)


def beat_intensities(y: np.ndarray, sr: int, beat_times: np.ndarray, hop_length: int) -> list[float]:
    onset = librosa.onset.onset_strength(y=y, sr=sr, hop_length=hop_length)
    rms = librosa.feature.rms(y=y, hop_length=hop_length)[0]
    onset = normalize(onset)
    rms = normalize(rms)
    onset_times = librosa.frames_to_time(np.arange(len(onset)), sr=sr, hop_length=hop_length)
    rms_times = librosa.frames_to_time(np.arange(len(rms)), sr=sr, hop_length=hop_length)

    return [
        float(
            np.clip(
                0.65 * value_at_time(onset_times, onset, float(beat_time))
                + 0.35 * value_at_time(rms_times, rms, float(beat_time)),
                0.0,
                1.0,
            )
        )
        for beat_time in beat_times
    ]


def base_lane(delta_med: float, up_threshold: float, down_threshold: float, center_threshold: float) -> int:
    if delta_med > up_threshold:
        return LANE_UP
    if delta_med > center_threshold:
        return LANE_RIGHT
    if delta_med >= -center_threshold:
        return LANE_CENTER
    if delta_med > -down_threshold:
        return LANE_LEFT
    return LANE_DOWN


def move_lane_by_direction(lane: int, delta_dir: float, dir_threshold: float) -> int:
    index = LANE_INDEX_IN_ORDER[lane]
    if delta_dir > dir_threshold and index < len(LANES_LOW_TO_HIGH) - 1:
        return LANES_LOW_TO_HIGH[index + 1]
    if delta_dir < -dir_threshold and index > 0:
        return LANES_LOW_TO_HIGH[index - 1]
    return lane


def closest_alternative_lane(delta_med: float, blocked_lane: int, blocked_lanes: Iterable[int] = ()) -> int:
    blocked = set(blocked_lanes)
    blocked.add(blocked_lane)
    candidates = [
        (lane, abs(delta_med - center))
        for lane, center in LANE_DELTA_CENTERS.items()
        if lane not in blocked
    ]
    candidates.sort(key=lambda item: item[1])
    return candidates[0][0] if candidates else blocked_lane


def assign_lanes(
    pitches: np.ndarray,
    beat_window: int,
    up_threshold: float,
    down_threshold: float,
    center_threshold: float,
    dir_threshold: float,
    anti_stack_max: int,
) -> LaneAssignment:
    lanes: list[int] = []
    local_medians: list[float] = []
    delta_medians: list[float] = []
    delta_dirs: list[float] = []
    direction_applied = 0
    anti_stack_triggered = 0

    for index, pitch in enumerate(pitches):
        lo = max(0, index - beat_window)
        hi = min(len(pitches), index + beat_window + 1)
        local_median = float(np.median(pitches[lo:hi]))
        delta_med = float(pitch - local_median)
        delta_dir = float(pitch - pitches[index - 1]) if index > 0 else 0.0

        lane = base_lane(delta_med, up_threshold, down_threshold, center_threshold)
        directed_lane = move_lane_by_direction(lane, delta_dir, dir_threshold)
        if directed_lane != lane:
            direction_applied += 1
            lane = directed_lane

        if index >= anti_stack_max and all(lanes[-step] == lane for step in range(1, anti_stack_max + 1)):
            lane = closest_alternative_lane(delta_med, lane)
            anti_stack_triggered += 1

        lanes.append(lane)
        local_medians.append(local_median)
        delta_medians.append(delta_med)
        delta_dirs.append(delta_dir)

    return LaneAssignment(lanes, local_medians, delta_medians, delta_dirs, direction_applied, anti_stack_triggered)


def detect_fish_notes(
    beat_times: np.ndarray,
    pitches: np.ndarray,
    lanes: list[int],
    threshold_semi: float,
    step_threshold_semi: float,
    variance_threshold: float,
    min_beats: int,
    min_gap_beats: int,
    min_duration: float,
) -> list[Candidate]:
    fish_notes: list[Candidate] = []
    last_fish_index = -100000
    index = 0

    while index < len(pitches):
        start_pitch = float(pitches[index])
        run_values = [start_pitch]
        end = index + 1

        while end < len(pitches):
            value = float(pitches[end])
            previous = float(pitches[end - 1])
            trial = run_values + [value]

            if abs(value - start_pitch) > threshold_semi:
                break
            if abs(value - previous) > step_threshold_semi:
                break
            if len(trial) >= 3 and float(np.std(trial)) > variance_threshold:
                break

            run_values.append(value)
            end += 1

        run_len = end - index
        if run_len >= min_beats and index - last_fish_index >= min_gap_beats:
            duration = float(beat_times[end - 1] - beat_times[index])
            if duration >= min_duration:
                fish_notes.append(
                    Candidate(
                        time=float(beat_times[index]),
                        note_type=NOTE_FISH,
                        lane=lanes[index],
                        duration=duration,
                        confidence=1.0,
                        pitch=start_pitch,
                        source="fish",
                        beat_index=index,
                    )
                )
                last_fish_index = index
                index = end
                continue

        index += 1

    return fish_notes


def within_same_lane_fish_safety(candidate: Candidate, fish_notes: list[Candidate], extra_seconds: float) -> bool:
    for fish in fish_notes:
        if candidate is fish:
            continue
        if candidate.lane != fish.lane:
            continue
        if fish.time < candidate.time < fish.time + fish.duration + extra_seconds:
            return True
    return False


def choose_double_lane(main_lane: int, delta_dir: float) -> int:
    if main_lane == LANE_UP:
        return LANE_DOWN
    if main_lane == LANE_DOWN:
        return LANE_UP
    if main_lane == LANE_LEFT:
        return LANE_RIGHT
    if main_lane == LANE_RIGHT:
        return LANE_LEFT
    return LANE_RIGHT if delta_dir >= 0 else LANE_LEFT


def build_candidates(
    beat_times: np.ndarray,
    pitches: np.ndarray,
    assignment: LaneAssignment,
    intensities: list[float],
    fish_notes: list[Candidate],
    args: argparse.Namespace,
) -> tuple[list[Candidate], int]:
    fish_by_index = {fish.beat_index: fish for fish in fish_notes}
    candidates: list[Candidate] = []
    double_added = 0
    last_double_index = -100000

    for index, beat_time in enumerate(beat_times):
        if index in fish_by_index:
            candidates.append(fish_by_index[index])
            continue

        lane = assignment.lanes[index]
        intensity = intensities[index]
        melodic_strength = min(
            abs(assignment.delta_medians[index]) / max(args.up_threshold, args.down_threshold, 0.001),
            1.0,
        )
        direction_strength = min(abs(assignment.delta_dirs[index]) / max(args.dir_threshold, 0.001), 1.0)
        confidence = float(np.clip(0.45 + 0.25 * intensity + 0.2 * melodic_strength + 0.1 * direction_strength, 0.0, 1.0))

        primary = Candidate(
            time=float(beat_time),
            note_type=NOTE_BUBBLE,
            lane=lane,
            duration=0.0,
            confidence=confidence,
            pitch=float(pitches[index]),
            source="beat",
            beat_index=index,
        )

        if not within_same_lane_fish_safety(primary, fish_notes, args.fish_safety_extra_seconds):
            candidates.append(primary)

        if not args.enable_double_notes:
            continue
        if intensity < args.double_note_threshold:
            continue
        if index - last_double_index < args.double_note_min_gap_beats:
            continue

        double_lane = choose_double_lane(lane, assignment.delta_dirs[index])
        if double_lane == lane:
            continue

        double = Candidate(
            time=float(beat_time),
            note_type=NOTE_BUBBLE,
            lane=double_lane,
            duration=0.0,
            confidence=float(np.clip(0.35 + 0.55 * intensity, 0.0, 1.0)),
            pitch=float(pitches[index]),
            source="double",
            beat_index=index,
            is_double=True,
        )

        if within_same_lane_fish_safety(double, fish_notes, args.fish_safety_extra_seconds):
            continue

        candidates.append(double)
        double_added += 1
        last_double_index = index

    return candidates, double_added


def trim_to_target(notes: list[Candidate], target_max: int) -> list[Candidate]:
    if target_max <= 0 or len(notes) <= target_max:
        return notes

    result = list(notes)
    removable = sorted(
        [note for note in result if note.is_double],
        key=lambda note: note.confidence,
    )
    while len(result) > target_max and removable:
        result.remove(removable.pop(0))

    return result


def validate_notes(notes: list[Candidate], fish_extra_seconds: float) -> dict:
    sorted_notes = sorted(notes, key=lambda note: (note.time, note.lane))
    lane_counts = {lane: 0 for lane in range(0, 6)}
    simultaneous: dict[float, list[Candidate]] = {}

    for note in sorted_notes:
        lane_counts[note.lane] = lane_counts.get(note.lane, 0) + 1
        simultaneous.setdefault(round(note.time, 6), []).append(note)

    max_simultaneous = max((len(group) for group in simultaneous.values()), default=0)
    duplicate_lane_simultaneous = sum(
        1
        for group in simultaneous.values()
        if len([note.lane for note in group]) != len({note.lane for note in group})
    )

    fish_same_lane_violations = 0
    fish_notes = [note for note in sorted_notes if note.note_type == NOTE_FISH]
    for fish in fish_notes:
        for note in sorted_notes:
            if note is fish or note.lane != fish.lane:
                continue
            if fish.time < note.time < fish.time + fish.duration + fish_extra_seconds:
                fish_same_lane_violations += 1

    run_hist = {"1": 0, "2": 0, "3": 0, "4+": 0}
    max_run = 0
    current_lane = None
    current_run = 0
    for note in [note for note in sorted_notes if note.note_type == NOTE_BUBBLE]:
        if note.lane == current_lane:
            current_run += 1
        else:
            if current_run > 0:
                key = str(current_run) if current_run < 4 else "4+"
                run_hist[key] += 1
                max_run = max(max_run, current_run)
            current_lane = note.lane
            current_run = 1

    if current_run > 0:
        key = str(current_run) if current_run < 4 else "4+"
        run_hist[key] += 1
        max_run = max(max_run, current_run)

    return {
        "lane_counts": lane_counts,
        "max_simultaneous": max_simultaneous,
        "duplicate_lane_simultaneous": duplicate_lane_simultaneous,
        "fish_same_lane_violations": fish_same_lane_violations,
        "run_hist": run_hist,
        "max_run": max_run,
    }


def print_final_stats(notes: list[Candidate], assignment: LaneAssignment, double_added: int, validation: dict) -> None:
    bubble_count = sum(1 for note in notes if note.note_type == NOTE_BUBBLE)
    fish_notes = [note for note in notes if note.note_type == NOTE_FISH]
    double_count = sum(1 for note in notes if note.is_double)

    print("[7/7] Final statistics...")
    print(f"    Total = {len(notes)} ({bubble_count} bubble, {len(fish_notes)} fish)")
    print(f"    Direction bonus applied = {assignment.direction_applied}")
    print(f"    Anti-stack triggered    = {assignment.anti_stack_triggered}")
    print(f"    Double notes added      = {double_added} ({double_count} kept)")
    print(f"    Max simultaneous notes  = {validation['max_simultaneous']}")
    print(f"    Duplicate same-time lanes = {validation['duplicate_lane_simultaneous']}")
    print(f"    Fish same-lane safety violations = {validation['fish_same_lane_violations']}")

    if fish_notes:
        durations = [fish.duration for fish in fish_notes]
        print(f"    Fish duration = {min(durations):.2f}~{max(durations):.2f}s (avg {sum(durations) / len(durations):.2f}s)")
    else:
        print("    Fish duration = n/a")

    print(
        "    Consecutive same-lane runs: "
        f"1={validation['run_hist']['1']} "
        f"2={validation['run_hist']['2']} "
        f"3={validation['run_hist']['3']} "
        f"4+={validation['run_hist']['4+']} "
        f"(max {validation['max_run']})"
    )

    for lane in LANES_LOW_TO_HIGH:
        count = validation["lane_counts"][lane]
        ratio = count / max(1, len(notes))
        print(f"    {LANE_NAMES[lane]:>6}: {count:4d} ({ratio * 100:5.1f}%)")

    if validation["lane_counts"].get(LANE_NONE, 0) > 0:
        print(f"    WARNING: Lane.None notes = {validation['lane_counts'][LANE_NONE]}")


def extract(args: argparse.Namespace) -> dict:
    if not os.path.exists(args.input):
        raise FileNotFoundError(f"Audio file not found: {args.input}")

    print(f"[1/7] Loading audio: {args.input}")
    y, sr, duration = load_audio_mono(args.input, args.analysis_sr)
    print(f"    sr = {sr} Hz, duration = {duration:.2f}s")

    print("[2/7] Estimating tempo and beat times with librosa.beat.beat_track...")
    tempo, beat_frames = librosa.beat.beat_track(y=y, sr=sr, hop_length=args.hop_length, units="frames")
    tempo_value = float(tempo) if np.isscalar(tempo) else float(tempo[0])
    beat_times = librosa.frames_to_time(beat_frames, sr=sr, hop_length=args.hop_length)
    beat_times = np.asarray(beat_times, dtype=float)
    beat_times = beat_times[beat_times >= args.min_first_beat_time]
    if beat_times.size == 0:
        raise RuntimeError("No beats detected. Check audio file.")
    print(f"    BPM = {tempo_value:.2f}, beats = {len(beat_times)}, first beat = {beat_times[0]:.3f}s")

    print("[3/7] Computing per-beat spectral centroid pitch...")
    pitches = beat_pitches(y, sr, beat_times, args.hop_length)
    intensities = beat_intensities(y, sr, beat_times, args.hop_length)
    print(f"    pitch range = {float(np.min(pitches)):.1f} ~ {float(np.max(pitches)):.1f} semi")

    print("[4/7] Assigning 5 lanes with local median + direction bonus + anti-stack...")
    assignment = assign_lanes(
        pitches,
        args.beat_window,
        args.up_threshold,
        args.down_threshold,
        args.center_threshold,
        args.dir_threshold,
        args.anti_stack_max,
    )
    print(f"    Direction bonus applied = {assignment.direction_applied}")
    print(f"    Anti-stack triggered    = {assignment.anti_stack_triggered}")

    print("[5/7] Detecting Fish sustain notes...")
    fish_notes = detect_fish_notes(
        beat_times,
        pitches,
        assignment.lanes,
        args.sustain_threshold_semi,
        args.sustain_step_threshold_semi,
        args.sustain_variance_threshold,
        args.sustain_min_beats,
        args.fish_min_gap_beats,
        args.fish_min_duration,
    )
    print(f"    Fish count = {len(fish_notes)}")

    print("[6/7] Building Bubble candidates and optional double notes...")
    notes, double_added = build_candidates(beat_times, pitches, assignment, intensities, fish_notes, args)
    notes = trim_to_target(notes, args.target_max_notes)
    notes = sorted(notes, key=lambda note: (note.time, note.lane, note.note_type))

    validation = validate_notes(notes, args.fish_safety_extra_seconds)
    print_final_stats(notes, assignment, double_added, validation)

    song_name = args.song_name or os.path.splitext(os.path.basename(args.input))[0]
    return {
        "SongName": song_name,
        "Bpm": tempo_value,
        "StartOffsetSeconds": 0.0,
        "SongLengthSeconds": duration,
        "Notes": [note_to_json(note, args) for note in notes],
    }


def note_to_json(note: Candidate, args: argparse.Namespace) -> dict:
    item = {
        "Type": note.note_type,
        "Lane": note.lane,
        "HitTime": float(note.time),
        "Duration": float(note.duration),
    }

    variants = parse_variant_list(args.fish_variants)
    if note.note_type == NOTE_FISH and variants:
        item["Variant"] = variants[note.beat_index % len(variants)]

    return item


def parse_variant_list(raw: str) -> list[str]:
    if not raw:
        return []

    return [part.strip() for part in raw.split(",") if part.strip()]


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="AO beatmap extractor, librosa beat_track only.")
    parser.add_argument("input", help="Input audio path")
    parser.add_argument("output", help="Output JSON path")
    parser.add_argument("--song-name", default="")

    parser.add_argument("--analysis-sr", type=int, default=22050)
    parser.add_argument("--hop-length", type=int, default=512)
    parser.add_argument("--min-first-beat-time", type=float, default=0.0)

    parser.add_argument("--beat-window", type=int, default=8)
    parser.add_argument("--up-threshold", type=float, default=2.0)
    parser.add_argument("--down-threshold", type=float, default=2.0)
    parser.add_argument("--center-threshold", type=float, default=0.6)
    parser.add_argument("--dir-threshold", type=float, default=1.0)
    parser.add_argument("--anti-stack-max", type=int, default=3)

    parser.add_argument("--sustain-threshold-semi", type=float, default=2.5)
    parser.add_argument("--sustain-step-threshold-semi", type=float, default=1.5)
    parser.add_argument("--sustain-variance-threshold", type=float, default=1.8)
    parser.add_argument("--sustain-min-beats", type=int, default=4)
    parser.add_argument("--fish-min-gap-beats", type=int, default=6)
    parser.add_argument("--fish-min-duration", type=float, default=0.8)
    parser.add_argument("--fish-safety-extra-seconds", type=float, default=0.15)
    parser.add_argument(
        "--fish-variants",
        default="",
        help="Optional comma-separated Fish prefab variant ids, for example fish_cyan,fish_gold,fish_pink.",
    )

    parser.add_argument("--enable-double-notes", action="store_true")
    parser.add_argument("--double-note-threshold", type=float, default=0.78)
    parser.add_argument("--double-note-min-gap-beats", type=int, default=4)
    parser.add_argument("--target-max-notes", type=int, default=0)
    return parser


def main() -> None:
    args = build_parser().parse_args()
    beatmap = extract(args)

    os.makedirs(os.path.dirname(os.path.abspath(args.output)), exist_ok=True)
    with open(args.output, "w", encoding="utf-8") as file:
        json.dump(beatmap, file, indent=2, ensure_ascii=False)

    print(f"\nWrote: {args.output}")
    print(f"  SongName           = {beatmap['SongName']}")
    print(f"  Bpm                = {beatmap['Bpm']:.2f}")
    print(f"  SongLengthSeconds  = {beatmap['SongLengthSeconds']:.2f}")
    print(f"  Notes              = {len(beatmap['Notes'])}")


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"\nERROR: {exc}", file=sys.stderr)
        sys.exit(1)
