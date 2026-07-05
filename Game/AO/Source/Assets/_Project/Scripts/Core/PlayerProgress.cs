using System.Collections.Generic;
using UnityEngine;

namespace AO.Core
{
    public struct NormalSongRecord
    {
        public int BestScore;
        public int BestCombo;
        public float BestAccuracy;
        public string BestRank;
    }

    public struct EternalSongRecord
    {
        public int BestScore;
        public int BestCombo;
        public float BestAccuracy;
        public double BestSurvivalSeconds;
    }

    public static class PlayerProgress
    {
        private const string Prefix = "AO.Progress.";
        private const float DefaultBgmVolume = 1f;
        private const float DefaultSfxVolume = 1f;
        private const float MinPlaybackSpeed = 0.5f;
        private const float MaxPlaybackSpeed = 3f;
        private const float PlaybackSpeedStep = 0.5f;
        private const float TemporaryEternalUnlockAccuracyThreshold = 0.5f;

        private static readonly HashSet<string> RuntimeEternalUnlocks = new HashSet<string>();
        private static readonly HashSet<string> PendingEternalUnlockPresentations = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            RuntimeEternalUnlocks.Clear();
            PendingEternalUnlockPresentations.Clear();
        }

        public static NormalSongRecord GetNormalRecord(string songId)
        {
            return new NormalSongRecord
            {
                BestScore = PlayerPrefs.GetInt(Key(songId, "Normal.Score"), 0),
                BestCombo = PlayerPrefs.GetInt(Key(songId, "Normal.Combo"), 0),
                BestAccuracy = PlayerPrefs.GetFloat(Key(songId, "Normal.Accuracy"), 0f),
                BestRank = PlayerPrefs.GetString(Key(songId, "Normal.Rank"), "-")
            };
        }

        public static EternalSongRecord GetEternalRecord(string songId)
        {
            return new EternalSongRecord
            {
                BestScore = PlayerPrefs.GetInt(Key(songId, "Eternal.Score"), 0),
                BestCombo = PlayerPrefs.GetInt(Key(songId, "Eternal.Combo"), 0),
                BestAccuracy = PlayerPrefs.GetFloat(Key(songId, "Eternal.Accuracy"), 0f),
                BestSurvivalSeconds = PlayerPrefs.GetFloat(Key(songId, "Eternal.Survival"), 0f)
            };
        }

        public static bool IsEternalUnlocked(string songId)
        {
            return !string.IsNullOrEmpty(songId) && RuntimeEternalUnlocks.Contains(songId);
        }

        public static bool UnlockEternal(string songId)
        {
            if (string.IsNullOrEmpty(songId)) return false;

            bool newlyUnlocked = RuntimeEternalUnlocks.Add(songId);
            if (newlyUnlocked) PendingEternalUnlockPresentations.Add(songId);
            return newlyUnlocked;
        }

        public static bool ConsumePendingEternalUnlockPresentation(string songId)
        {
            if (string.IsNullOrEmpty(songId)) return false;
            if (!PendingEternalUnlockPresentations.Contains(songId)) return false;

            PendingEternalUnlockPresentations.Remove(songId);
            return true;
        }

        public static void SaveResult(SessionResult result)
        {
            if (string.IsNullOrEmpty(result.SongId)) return;

            if (result.Mode == PlayMode.Normal)
            {
                SaveNormal(result);
                if (result.Status == SessionResultStatus.Cleared || IsTemporaryEternalUnlockResult(result))
                {
                    UnlockEternal(result.SongId);
                }
            }
            else
            {
                SaveEternal(result);
            }

            PlayerPrefs.Save();
        }

        public static float GetPlaybackSpeed(SongDefinition song)
        {
            if (song == null) return 1f;
            return QuantizePlaybackSpeed(PlayerPrefs.GetFloat(Key(song.SongId, "PlaybackSpeed"), song.DefaultPlaybackSpeed));
        }

        public static void SetPlaybackSpeed(string songId, float value)
        {
            if (string.IsNullOrEmpty(songId)) return;
            PlayerPrefs.SetFloat(Key(songId, "PlaybackSpeed"), QuantizePlaybackSpeed(value));
        }

        public static float GetNoteSpeed(SongDefinition song)
        {
            if (song == null) return 1f;
            return PlayerPrefs.GetFloat(Key(song.SongId, "NoteSpeed"), song.DefaultNoteSpeed);
        }

        public static void SetNoteSpeed(string songId, float value)
        {
            if (string.IsNullOrEmpty(songId)) return;
            PlayerPrefs.SetFloat(Key(songId, "NoteSpeed"), Mathf.Clamp(value, 0.5f, 2f));
        }

        public static float GetAudioOffset(SongDefinition song)
        {
            if (song == null) return 0f;
            return PlayerPrefs.GetFloat(Key(song.SongId, "AudioOffset"), song.DefaultAudioOffsetSeconds);
        }

        public static void SetAudioOffset(string songId, float value)
        {
            if (string.IsNullOrEmpty(songId)) return;
            PlayerPrefs.SetFloat(Key(songId, "AudioOffset"), Mathf.Clamp(value, -0.3f, 0.3f));
        }

        public static void ResetTimingOptions(SongDefinition song)
        {
            if (song == null || string.IsNullOrEmpty(song.SongId)) return;

            SetPlaybackSpeed(song.SongId, song.DefaultPlaybackSpeed);
            SetNoteSpeed(song.SongId, song.DefaultNoteSpeed);
            SetAudioOffset(song.SongId, song.DefaultAudioOffsetSeconds);
        }

        public static void ResetTimingOptions(SongLibrary library)
        {
            if (library == null || library.Songs == null) return;

            for (int i = 0; i < library.Songs.Length; i++)
            {
                ResetTimingOptions(library.Songs[i]);
            }

            PlayerPrefs.Save();
        }

        public static float GetBgmVolume()
        {
            return PlayerPrefs.GetFloat(Prefix + "Audio.BgmVolume", DefaultBgmVolume);
        }

        public static void SetBgmVolume(float value)
        {
            PlayerPrefs.SetFloat(Prefix + "Audio.BgmVolume", Mathf.Clamp01(value));
        }

        public static float GetSfxVolume()
        {
            return PlayerPrefs.GetFloat(Prefix + "Audio.SfxVolume", DefaultSfxVolume);
        }

        public static void SetSfxVolume(float value)
        {
            PlayerPrefs.SetFloat(Prefix + "Audio.SfxVolume", Mathf.Clamp01(value));
        }

        private static void SaveNormal(SessionResult result)
        {
            NormalSongRecord current = GetNormalRecord(result.SongId);
            if (result.Score > current.BestScore) PlayerPrefs.SetInt(Key(result.SongId, "Normal.Score"), result.Score);
            if (result.MaxCombo > current.BestCombo) PlayerPrefs.SetInt(Key(result.SongId, "Normal.Combo"), result.MaxCombo);
            if (result.WeightedAccuracy > current.BestAccuracy)
            {
                PlayerPrefs.SetFloat(Key(result.SongId, "Normal.Accuracy"), result.WeightedAccuracy);
                PlayerPrefs.SetString(Key(result.SongId, "Normal.Rank"), result.Rank);
            }
        }

        private static bool IsTemporaryEternalUnlockResult(SessionResult result)
        {
            return result.WeightedAccuracy >= TemporaryEternalUnlockAccuracyThreshold;
        }

        private static void SaveEternal(SessionResult result)
        {
            EternalSongRecord current = GetEternalRecord(result.SongId);
            if (result.Score > current.BestScore) PlayerPrefs.SetInt(Key(result.SongId, "Eternal.Score"), result.Score);
            if (result.MaxCombo > current.BestCombo) PlayerPrefs.SetInt(Key(result.SongId, "Eternal.Combo"), result.MaxCombo);
            if (result.WeightedAccuracy > current.BestAccuracy) PlayerPrefs.SetFloat(Key(result.SongId, "Eternal.Accuracy"), result.WeightedAccuracy);
            if (result.DurationSeconds > current.BestSurvivalSeconds)
            {
                PlayerPrefs.SetFloat(Key(result.SongId, "Eternal.Survival"), (float)result.DurationSeconds);
            }
        }

        private static string Key(string songId, string suffix)
        {
            return Prefix + songId + "." + suffix;
        }

        private static float QuantizePlaybackSpeed(float value)
        {
            float stepped = Mathf.Round(value / PlaybackSpeedStep) * PlaybackSpeedStep;
            return Mathf.Clamp(stepped, MinPlaybackSpeed, MaxPlaybackSpeed);
        }
    }
}
