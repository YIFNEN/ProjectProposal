using System;
using UnityEngine;

namespace AO.Core
{
    public enum PlayMode
    {
        Normal,
        Eternal
    }

    public enum SessionResultStatus
    {
        None,
        Cleared,
        GameOver,
        EternalExited,
        ReturnedToLobby
    }

    [Serializable]
    public struct HandRangeStats
    {
        public bool HasSamples;
        public Vector3 Min;
        public Vector3 Max;
    }

    [Serializable]
    public struct SessionResult
    {
        public SessionResultStatus Status;
        public PlayMode Mode;
        public string SongId;
        public string SongName;
        public int Score;
        public int MaxCombo;
        public float WeightedAccuracy;
        public string Rank;
        public int PerfectCount;
        public int GoodCount;
        public int MissCount;
        public int FishStrokeSuccessCount;
        public int FishStrokeFailCount;
        public double DurationSeconds;
        public int FeverActivations;
        public float PlaybackSpeed;
        public float NoteSpeed;
        public HandRangeStats LeftHandRange;
        public HandRangeStats RightHandRange;
        public bool HandRangeIsLocal;
        public string HandRangeReferenceName;
    }

    [CreateAssetMenu(fileName = "GameSession", menuName = "AO/Game Session", order = 100)]
    public class GameSession : ScriptableObject
    {
        public PlayMode Mode = PlayMode.Normal;
        public string SelectedSongId = "twinkle";
        public string BeatmapPath = "Assets/_Project/Beatmaps/Twinkle_Normal.json";
        public string SongName = "Synthion - Twinkle";
        public float PlaybackSpeed = 1f;
        public float NoteSpeed = 1f;
        public float AudioOffsetSeconds = 0f;

        [Header("Last Result")]
        public SessionResult LastResult;

        public void SetMode(PlayMode mode) => Mode = mode;

        public void SelectSong(string songId, string songName)
        {
            SelectedSongId = songId;
            SongName = songName;
        }

        public void SetSpeed(float playbackSpeed, float noteSpeed)
        {
            PlaybackSpeed = playbackSpeed;
            NoteSpeed = noteSpeed;
        }

        public void SetAudioOffset(float seconds) => AudioOffsetSeconds = seconds;
        public void SetResult(SessionResult result) => LastResult = result;
    }
}
