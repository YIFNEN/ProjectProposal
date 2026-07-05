param(
    [string]$Python = "C:\Users\sun99\AppData\Local\Programs\Python\Python312\python.exe",
    [string]$Ffmpeg = "ffmpeg",
    [switch]$InstallYtDlp,
    [switch]$Overwrite
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$AudioDir = Join-Path $Root "Assets\_Project\Audio\BGM"
$BeatmapDir = Join-Path $Root "Assets\_Project\Beatmaps"
$SourceAudioDir = Join-Path $Root "SourceAudio"
$Extractor = Join-Path $Root "Tools\Beatmap\extract_beatmap.py"

if ($InstallYtDlp) {
    & $Python -m pip install yt-dlp
}

New-Item -ItemType Directory -Force -Path $AudioDir | Out-Null
New-Item -ItemType Directory -Force -Path $BeatmapDir | Out-Null
New-Item -ItemType Directory -Force -Path $SourceAudioDir | Out-Null

$songs = @(
    @{
        Url = "https://www.youtube.com/watch?v=myiJB8SiIcU"
        Wav = "Twinklestar.wav"
        Json = "Twinklestar_Normal.json"
        Name = "Snail's House - Twinklestar"
    },
    @{
        Url = "https://www.youtube.com/watch?v=QDCe1_SzHAc"
        Wav = "Utakata.wav"
        Json = "Utakata_Normal.json"
        Name = "Snail's House - Utakata"
    },
    @{
        Url = "https://www.youtube.com/watch?v=2CwBFr-Eoxg"
        Wav = "ShinkaiShoujo.wav"
        Json = "ShinkaiShoujo_Normal.json"
        Name = "ゆうゆ feat. 初音ミク - 深海少女"
    }
)

foreach ($song in $songs) {
    $wavPath = Join-Path $AudioDir $song.Wav
    $jsonPath = Join-Path $BeatmapDir $song.Json
    $sourceStem = [System.IO.Path]::GetFileNameWithoutExtension($song.Wav)
    $sourceTemplate = Join-Path $SourceAudioDir "$sourceStem.%(ext)s"
    $sourceCandidates = Get-ChildItem -LiteralPath $SourceAudioDir -File -Filter "$sourceStem.*" -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -notin @(".part", ".ytdl", ".json", ".description") } |
        Sort-Object LastWriteTime -Descending

    if ($Overwrite -or !$sourceCandidates) {
        $ytDlpArgs = @(
            "-m", "yt_dlp",
            "-f", "bestaudio",
            "--no-playlist",
            "--output", $sourceTemplate,
            $song.Url
        )

        if ($Overwrite) {
            $ytDlpArgs = @("-m", "yt_dlp", "--force-overwrites") + $ytDlpArgs[2..($ytDlpArgs.Length - 1)]
        }

        & $Python @ytDlpArgs

        $sourceCandidates = Get-ChildItem -LiteralPath $SourceAudioDir -File -Filter "$sourceStem.*" |
            Where-Object { $_.Extension -notin @(".part", ".ytdl", ".json", ".description") } |
            Sort-Object LastWriteTime -Descending
    }

    $sourcePath = $sourceCandidates | Select-Object -First 1
    if (!$sourcePath) {
        throw "Source audio download failed: $sourceStem"
    }

    if ($Overwrite -or !(Test-Path $wavPath)) {
        & $Ffmpeg `
            -hide_banner `
            -loglevel error `
            -y `
            -i $sourcePath.FullName `
            -vn `
            -acodec pcm_s16le `
            -ar 44100 `
            -ac 2 `
            $wavPath
    }

    Write-Host "Source audio preserved: $($sourcePath.FullName)"
    Write-Host "Unity WAV ready: $wavPath"
    & $Python $Extractor $wavPath $jsonPath --song-name $song.Name --enable-double-notes
}

Write-Host "Extra song audio and beatmaps are ready."
Write-Host "Original bestaudio files are preserved under SourceAudio."
Write-Host "Create 10-second preview clips manually under Assets\_Project\Audio\BGM\Previews and assign them through AO/Songs/Refresh Default Song Library."
