# AO Beatmap Extraction Tool

`extract_beatmap.py` generates `Assets/_Project/Beatmaps/*_Normal.json` files from song audio. The current project has four checked-in default Normal beatmaps connected through `SongLibrary.asset`.

## Current Algorithm

The extractor uses `librosa.beat.beat_track` only. There is no custom beat grid, fallback backend, or compare mode.

The mapping keeps the old A+B+C feel:

- A: Fish notes come from sustained pitch runs and keep a `Duration`.
- B: Bubble lanes use local median spectral-centroid contour plus anti-stack.
- C: Lane movement gets a direction bonus from the previous beat.

File loading uses `soundfile` because `librosa.load` can hang in the current Codex runtime, but beat detection itself is still pure `librosa.beat.beat_track`.

## Lanes

| Value | Lane |
|---:|---|
| 0 | Up |
| 1 | Down |
| 2 | Left |
| 3 | Right |
| 4 | Center |
| 5 | None |

New beatmaps should use lanes 0-4 only. `Lane.None = 5` remains for runtime compatibility.

## Recommended Twinkle Command

```powershell
C:\Users\sun99\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe `
  Tools\Beatmap\extract_beatmap.py `
  Assets\_Project\Audio\BGM\Twinkle_Original.wav `
  Assets\_Project\Beatmaps\Twinkle_Normal.json `
  --song-name "Synthion - Twinkle" `
  --enable-double-notes `
  --up-threshold 3.0 `
  --down-threshold 3.0 `
  --center-threshold 1.5 `
  --dir-threshold 2.0 `
  --sustain-threshold-semi 3.0 `
  --sustain-step-threshold-semi 2.0 `
  --sustain-variance-threshold 2.3 `
  --sustain-min-beats 3 `
  --fish-min-gap-beats 16
```

## Main Options

| Option | Default | Meaning |
|---|---:|---|
| `--analysis-sr` | `22050` | Analysis sample rate |
| `--hop-length` | `512` | Analysis hop length |
| `--beat-window` | `8` | Local median window radius in beats |
| `--up-threshold` | `2.0` | Local median delta for Up |
| `--down-threshold` | `2.0` | Local median delta for Down |
| `--center-threshold` | `0.6` | Delta band for Center |
| `--dir-threshold` | `1.0` | Previous-beat delta for one-step lane bonus |
| `--anti-stack-max` | `3` | Prevents long same-lane runs |
| `--enable-double-notes` | off | Adds up to 2 notes on strong beats |
| `--double-note-threshold` | `0.78` | Onset/RMS threshold for secondary notes |
| `--double-note-min-gap-beats` | `4` | Minimum beat gap between double notes |
| `--sustain-threshold-semi` | `2.5` | Fish start pitch tolerance |
| `--sustain-step-threshold-semi` | `1.5` | Fish adjacent pitch tolerance |
| `--sustain-variance-threshold` | `1.8` | Fish run variance limit |
| `--sustain-min-beats` | `4` | Minimum sustain run length |
| `--fish-min-gap-beats` | `6` | Minimum gap between Fish notes |
| `--fish-min-duration` | `0.8` | Minimum Fish duration in seconds |
| `--fish-safety-extra-seconds` | `0.15` | Same-lane safety after Fish duration |
| `--target-max-notes` | `0` | Optional trim for low-confidence double notes only; 0 disables trimming |

## Fish Notes

Fish detection uses a conservative sustain run:

- current pitch must stay close to the run start pitch,
- adjacent beat pitch steps must stay small,
- run variance must stay low,
- duration must be at least `0.8s`.

Fish lane is the sustain start lane, not Center/None by default. During `Duration + fish-safety-extra-seconds`, same-lane Bubble/double notes are removed. Other lanes may still keep notes, so two-controller play remains possible without unfair same-lane overlap.

## Current Checked-In Beatmaps

The default SongLibrary set currently has these Normal beatmaps:

| File | BPM estimate | Total notes | Bubble | Fish | Max simultaneous notes |
|---|---:|---:|---:|---:|---:|
| `Twinkle_Normal.json` | 89.10 | 416 | 400 | 16 | 2 |
| `Twinklestar_Normal.json` | 117.45 | 429 | 408 | 21 | 2 |
| `Utakata_Normal.json` | 86.13 | 474 | 455 | 19 | 2 |
| `ShinkaiShoujo_Normal.json` | 136.00 | 553 | 531 | 22 | 2 |

All four checked beatmaps use lanes 0-4, have max two simultaneous notes, and are intended for the current five-lane X layout. If regenerating on Windows, use the full Python 3.12 path instead of the WindowsApps `python.exe` alias.

## Unity Integration

1. JSON files in `Assets/_Project/Beatmaps/` are imported by Unity as `TextAsset`.
2. `SongDefinition` assets reference each Normal beatmap and BGM/Preview clip.
3. `SongLibrary.asset` exposes the four default songs to Lobby.
4. `RhythmEngine` parses the selected JSON through `BeatmapLoader`.
5. `NoteSpawner` maps lanes 0-4 to the configured X-layout lane offsets.

Manual editor:

```text
AO/Beatmap Editor
```

Use it to adjust note Type, Lane, HitTime, and Duration by hand after playtesting.

## BPM Gap Fill Drafts

For songs where `librosa.beat_track` misses quiet intros, interludes, or outros, use the conservative gap-fill post-process instead of replacing the whole beatmap:

```powershell
python Tools\Beatmap\fill_beatmap_gaps.py Assets\_Project\Beatmaps\Utakata_Normal_SparseBackup_20260612.json Assets\_Project\Beatmaps\Utakata_Normal.json --audio Assets\_Project\Audio\BGM\Utakata.wav --audio-rms-threshold 0.015 --min-gap-seconds 1.25 --min-spacing-seconds 0.32 --min-time 240 --max-time 300
```

The script preserves existing notes, aligns new Bubble notes to the current beatmap BPM phase, skips near-silent WAV sections by RMS threshold when `--audio` is supplied, and avoids same-lane Fish sustain overlap. `Utakata_Normal.json` currently uses the previous 469-note version plus only 5 BPM-grid Bubble notes in the interlude window (`240s-300s`); `Utakata_Normal_SparseBackup_20260612.json` preserves the previous 469-note version.
