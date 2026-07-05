# AO Song Addition Workflow

This project uses `SongDefinition` assets and one `SongLibrary` asset.

## 1. Prepare Files

Put the song WAV file and beatmap JSON in these folders:

- `Assets/_Project/Audio/BGM/<SongName>.wav`
- `Assets/_Project/Audio/BGM/Previews/<SongName>_Preview.wav`
- `Assets/_Project/Beatmaps/<SongName>_Normal.json`

For local school-only YouTube sources, download and extract audio only after the usage scope is approved.
Preview clips should be manually trimmed to the best 15 seconds of the song. Lobby playback uses this clip directly instead of seeking inside the full BGM.

For the 3 default YouTube placeholders, `Tools/Beatmap/download_extra_songs.ps1` downloads `yt-dlp -f bestaudio` source files into root-level `SourceAudio/` first, then converts those preserved `.webm`/`.m4a`-style source files into Unity-ready WAV files under `Assets/_Project/Audio/BGM`.

Current default songs are already wired in `SongLibrary.asset` with BGM, 15-second Preview, Normal Beatmap, and Thumbnail references:

| SongId | Display name | WAV | Preview | Beatmap |
|---|---|---|---|---|
| `twinkle` | `Synthion - Twinkle` | `Twinkle_Original.wav` | `Twinkle_Preview.wav` | `Twinkle_Normal.json` |
| `twinklestar` | `Snail's House - Twinklestar` | `Twinklestar.wav` | `Twinklestar_Preview.wav` | `Twinklestar_Normal.json` |
| `utakata` | `Snail's House - Utakata` | `Utakata.wav` | `Utakata_Preview.wav` | `Utakata_Normal.json` |
| `shinkai_shoujo` | `ゆうゆ feat. 初音ミク - 深海少女` | `ShinkaiShoujo.wav` | `ShinkaiShoujo_Preview.wav` | `ShinkaiShoujo_Normal.json` |

## 2. Generate Beatmap

Fast path for the 3 remaining default songs after local WAV files are present:

```powershell
python Tools\Beatmap\prepare_song_assets.py --song all
```

Expected local WAV paths:

- `Assets/_Project/Audio/BGM/Twinklestar.wav`
- `Assets/_Project/Audio/BGM/Utakata.wav`
- `Assets/_Project/Audio/BGM/ShinkaiShoujo.wav`

If the source is a media file or direct HTTP(S) media URL instead of a ready WAV, convert it first:

```powershell
python Tools\Beatmap\extract_audio_from_media.py --song twinklestar --input "D:\Media\Twinklestar.mp4" --prepare
```

Batch conversion for all 3 remaining default songs:

```powershell
python Tools\Beatmap\extract_audio_from_media.py --twinklestar "D:\Media\Twinklestar.mp4" --utakata "D:\Media\Utakata.webm" --shinkai-shoujo "D:\Media\ShinkaiShoujo.mp3" --prepare
```

`extract_audio_from_media.py` accepts local media file paths or direct HTTP(S) media URLs. It uses `ffmpeg` to create Unity-ready 44.1kHz stereo PCM WAV files.

If the source is an approved YouTube watch URL for the built-in placeholder songs, use the dedicated helper instead:

```powershell
.\Tools\Beatmap\download_extra_songs.ps1
```

Add `-InstallYtDlp` the first time if `yt-dlp` is not installed, and `-Overwrite` when you intentionally want to refresh both the preserved source audio and WAV outputs.

`download_extra_songs.ps1` creates:

- preserved bestaudio source files in `SourceAudio`
- Unity-ready WAV files in `Assets/_Project/Audio/BGM`
- normal beatmaps in `Assets/_Project/Beatmaps`

Then create preview clips either manually or with:

```powershell
python Tools\Beatmap\prepare_song_assets.py --song all --skip-beatmap
```

The checked-in built-in preview clips are currently 15 seconds long. Before using older helper defaults to overwrite them, confirm the intended preview length and start offset.

Then run Unity menu:

```text
AO/Songs/Refresh Default Song Library
```

That targeted setup refreshes the built-in `SongDefinition` and `SongLibrary` references without regenerating Title/Lobby/Gameplay/Result scenes.

Manual extractor path:

```powershell
"C:\Users\sun99\AppData\Local\Programs\Python\Python312\python.exe" Tools\Beatmap\extract_beatmap.py Assets\_Project\Audio\BGM\<SongName>.wav Assets\_Project\Beatmaps\<SongName>_Normal.json --song-name "<Display Name>" --enable-double-notes
```

Optional Fish variant ids:

```powershell
python Tools\Beatmap\extract_beatmap.py Assets\_Project\Audio\BGM\<SongName>.wav Assets\_Project\Beatmaps\<SongName>_Normal.json --song-name "<Display Name>" --enable-double-notes --fish-variants fish_cyan,fish_gold,fish_pink
```

## 3. Create SongDefinition

In Unity:

1. Run `AO/Songs/Create Blank Song Definition`.
2. Set `SongId` to a stable lowercase id, for example `twinklestar`.
3. Set `DisplayName`.
4. Assign `BgmClip`.
5. Assign `PreviewClip`.
6. Assign `NormalBeatmap`.
7. Tune default `Playback Speed`, `Note Speed`, and `Audio Offset` only if the song needs it.

The new song is added to `SongLibrary` automatically.

For the built-in placeholder list, run `AO/Songs/Refresh Default Song Library`.
This updates `SongLibrary.asset` without regenerating Title/Lobby/Gameplay/Result scenes.

## 4. Validate

- Open `02_Lobby`.
- Confirm all four default song cards are visible and show READY.
- If `NORMAL` shows `MISSING`, the WAV or JSON reference is still empty.
- Start Normal mode and verify music, note timing, score, oxygen, and Result.
