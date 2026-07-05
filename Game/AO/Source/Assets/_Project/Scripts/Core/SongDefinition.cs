using UnityEngine;

namespace AO.Core
{
    [CreateAssetMenu(fileName = "SongDefinition", menuName = "AO/Song Definition", order = 101)]
    public class SongDefinition : ScriptableObject
    {
        public string SongId = "song";
        public string DisplayName = "Song";
        public string Title = "";
        public string Composer = "";
        public string SourceUrl = "";
        public Sprite Thumbnail;
        public AudioClip BgmClip;
        public AudioClip PreviewClip;
        public TextAsset NormalBeatmap;
        [Range(0.5f, 3f)] public float DefaultPlaybackSpeed = 1f;
        [Range(0.5f, 2f)] public float DefaultNoteSpeed = 1f;
        [Range(-0.3f, 0.3f)] public float DefaultAudioOffsetSeconds = 0f;

        public bool IsPlayable => BgmClip != null && NormalBeatmap != null;

        public string EffectiveTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Title)) return Title.Trim();
                SplitDisplayName(DisplayName, out _, out string title);
                return string.IsNullOrWhiteSpace(title) ? DisplayName : title;
            }
        }

        public string EffectiveComposer
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Composer)) return Composer.Trim();
                SplitDisplayName(DisplayName, out string composer, out _);
                return composer;
            }
        }

        private static void SplitDisplayName(string displayName, out string composer, out string title)
        {
            composer = "";
            title = displayName;
            if (string.IsNullOrWhiteSpace(displayName)) return;

            int separator = displayName.IndexOf(" - ", System.StringComparison.Ordinal);
            if (separator < 0) return;

            composer = displayName.Substring(0, separator).Trim();
            title = displayName.Substring(separator + 3).Trim();
        }
    }
}
