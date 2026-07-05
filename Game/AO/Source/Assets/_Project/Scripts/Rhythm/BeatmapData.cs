using System;
using System.Collections.Generic;

namespace AO.Rhythm
{
    public enum NoteType
    {
        Bubble = 0,
        Fish = 1
    }

    public enum Lane
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3,
        Center = 4,
        None = 5
    }

    [Serializable]
    public class NoteData
    {
        public NoteType Type;
        public Lane Lane;

        /// <summary>
        /// The song time, in seconds, when the note should reach the hit position.
        /// </summary>
        public double HitTime;

        /// <summary>
        /// Sustain duration in seconds for Fish notes. Bubble notes use 0.
        /// </summary>
        public double Duration;

        /// <summary>
        /// Optional prefab/style variant id. Existing beatmaps can omit this.
        /// </summary>
        public string Variant;
    }

    [Serializable]
    public class BeatmapData
    {
        public string SongName = "";
        public float Bpm = 120f;
        public float StartOffsetSeconds = 0f;
        public float SongLengthSeconds = 0f;
        public List<NoteData> Notes = new List<NoteData>();
    }
}
