using System.Collections.Generic;
using UnityEngine;

namespace AO.Rhythm
{
    public static class BeatmapLoader
    {
        private const double FishBubbleCrowdingBeforeSeconds = 0.12d;
        private const double FishBubbleCrowdingAfterSeconds = 0.50d;
        private const double FishBubbleMinInteractionSeconds = 0.80d;
        private const int MaxBubblesPerFishWindow = 1;

        public static BeatmapData FromJson(TextAsset jsonAsset)
        {
            if (jsonAsset == null)
            {
                Debug.LogError("[BeatmapLoader] TextAsset is null.");
                return null;
            }

            return FromJsonString(jsonAsset.text);
        }

        public static BeatmapData FromJsonString(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("[BeatmapLoader] JSON string is empty.");
                return null;
            }

            try
            {
                BeatmapData data = JsonUtility.FromJson<BeatmapData>(json);
                if (data == null || data.Notes == null)
                {
                    Debug.LogError("[BeatmapLoader] Parse failed or Notes is null.");
                    return null;
                }

                data.Notes.Sort((a, b) => a.HitTime.CompareTo(b.HitTime));
                ReduceFishBubbleCrowding(data);
                return data;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BeatmapLoader] JSON parse error: {e.Message}");
                return null;
            }
        }

        private static void ReduceFishBubbleCrowding(BeatmapData data)
        {
            if (data == null || data.Notes == null || data.Notes.Count == 0) return;

            List<FishWindow> fishWindows = new List<FishWindow>();
            for (int i = 0; i < data.Notes.Count; i++)
            {
                NoteData note = data.Notes[i];
                if (note == null || note.Type != NoteType.Fish) continue;

                double sustain = System.Math.Max(note.Duration, FishBubbleMinInteractionSeconds);
                fishWindows.Add(new FishWindow(
                    note.HitTime - FishBubbleCrowdingBeforeSeconds,
                    note.HitTime + sustain + FishBubbleCrowdingAfterSeconds));
            }

            if (fishWindows.Count == 0) return;

            int[] keptBubbleCounts = new int[fishWindows.Count];
            List<NoteData> filtered = new List<NoteData>(data.Notes.Count);
            int removed = 0;

            for (int i = 0; i < data.Notes.Count; i++)
            {
                NoteData note = data.Notes[i];
                if (note == null || note.Type != NoteType.Bubble)
                {
                    filtered.Add(note);
                    continue;
                }

                int fishWindowIndex = FindContainingFishWindow(fishWindows, note.HitTime);
                if (fishWindowIndex < 0)
                {
                    filtered.Add(note);
                    continue;
                }

                if (keptBubbleCounts[fishWindowIndex] >= MaxBubblesPerFishWindow)
                {
                    removed++;
                    continue;
                }

                keptBubbleCounts[fishWindowIndex]++;
                filtered.Add(note);
            }

            if (removed <= 0) return;

            data.Notes = filtered;
            Debug.Log($"[BeatmapLoader] Suppressed {removed} crowded Bubble notes near Fish sustain windows.");
        }

        private static int FindContainingFishWindow(List<FishWindow> fishWindows, double hitTime)
        {
            for (int i = 0; i < fishWindows.Count; i++)
            {
                if (fishWindows[i].Contains(hitTime)) return i;
            }

            return -1;
        }

        private readonly struct FishWindow
        {
            private readonly double _start;
            private readonly double _end;

            public FishWindow(double start, double end)
            {
                _start = start;
                _end = end;
            }

            public bool Contains(double time)
            {
                return time >= _start && time <= _end;
            }
        }
    }
}
