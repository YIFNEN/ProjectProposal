using System.IO;
using AO.Rhythm;
using UnityEditor;
using UnityEngine;

namespace AO.Editor
{
    public class BeatmapEditor : EditorWindow
    {
        private const float IndexWidth = 34f;
        private const float TypeWidth = 90f;
        private const float LaneWidth = 90f;
        private const float HitTimeWidth = 95f;
        private const float DurationWidth = 95f;
        private const float VariantWidth = 150f;
        private const float RowButtonWidth = 24f;

        private TextAsset _jsonAsset;
        private BeatmapData _beatmap;
        private Vector2 _scroll;

        [MenuItem("AO/Beatmap Editor")]
        public static void Open()
        {
            GetWindow<BeatmapEditor>("AO Beatmap");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Beatmap", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _jsonAsset = (TextAsset)EditorGUILayout.ObjectField(_jsonAsset, typeof(TextAsset), false);
                if (GUILayout.Button("Load", GUILayout.Width(80f))) LoadSelectedAsset();
                if (GUILayout.Button("Save", GUILayout.Width(80f))) SaveSelectedAsset();
            }

            if (_beatmap == null)
            {
                EditorGUILayout.HelpBox("Select Twinkle_Normal.json and press Load.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(8f);
            _beatmap.SongName = EditorGUILayout.TextField("Song Name", _beatmap.SongName);
            _beatmap.Bpm = EditorGUILayout.FloatField("BPM", _beatmap.Bpm);
            _beatmap.StartOffsetSeconds = EditorGUILayout.FloatField("Start Offset", _beatmap.StartOffsetSeconds);
            _beatmap.SongLengthSeconds = EditorGUILayout.FloatField("Song Length", _beatmap.SongLengthSeconds);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Notes: {_beatmap.Notes.Count}", EditorStyles.boldLabel);
                if (GUILayout.Button("Sort By HitTime", GUILayout.Width(130f)))
                {
                    _beatmap.Notes.Sort((a, b) => a.HitTime.CompareTo(b.HitTime));
                }
            }

            EditorGUILayout.HelpBox(
                "Variant is optional. Known Fish variants: fish_cyan, fish_gold, fish_pink. Leave blank for NotePool fallback.",
                MessageType.None);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawNotesHeader();
            for (int i = 0; i < _beatmap.Notes.Count; i++)
            {
                NoteData note = _beatmap.Notes[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(i.ToString(), GUILayout.Width(IndexWidth));
                    note.Type = (NoteType)EditorGUILayout.EnumPopup(note.Type, GUILayout.Width(TypeWidth));
                    note.Lane = (Lane)EditorGUILayout.EnumPopup(note.Lane, GUILayout.Width(LaneWidth));
                    note.HitTime = EditorGUILayout.DoubleField(note.HitTime, GUILayout.Width(HitTimeWidth));
                    note.Duration = EditorGUILayout.DoubleField(note.Duration, GUILayout.Width(DurationWidth));
                    note.Variant = NormalizeVariant(
                        EditorGUILayout.TextField(note.Variant ?? string.Empty, GUILayout.Width(VariantWidth)));

                    if (GUILayout.Button("+", GUILayout.Width(RowButtonWidth)))
                    {
                        _beatmap.Notes.Insert(i + 1, new NoteData
                        {
                            Type = note.Type,
                            Lane = note.Lane,
                            HitTime = note.HitTime + 0.25d,
                            Duration = note.Duration,
                            Variant = note.Variant,
                        });
                    }

                    if (GUILayout.Button("-", GUILayout.Width(RowButtonWidth)))
                    {
                        _beatmap.Notes.RemoveAt(i);
                        i--;
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static void DrawNotesHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("#", EditorStyles.miniBoldLabel, GUILayout.Width(IndexWidth));
                EditorGUILayout.LabelField("Type", EditorStyles.miniBoldLabel, GUILayout.Width(TypeWidth));
                EditorGUILayout.LabelField("Lane", EditorStyles.miniBoldLabel, GUILayout.Width(LaneWidth));
                EditorGUILayout.LabelField("HitTime", EditorStyles.miniBoldLabel, GUILayout.Width(HitTimeWidth));
                EditorGUILayout.LabelField("Duration", EditorStyles.miniBoldLabel, GUILayout.Width(DurationWidth));
                EditorGUILayout.LabelField("Variant", EditorStyles.miniBoldLabel, GUILayout.Width(VariantWidth));
                GUILayout.Space((RowButtonWidth * 2f) + 8f);
            }
        }

        private static string NormalizeVariant(string variant)
        {
            return string.IsNullOrWhiteSpace(variant) ? string.Empty : variant.Trim();
        }

        private void LoadSelectedAsset()
        {
            _beatmap = BeatmapLoader.FromJson(_jsonAsset);
        }

        private void SaveSelectedAsset()
        {
            if (_jsonAsset == null || _beatmap == null)
            {
                EditorUtility.DisplayDialog("AO Beatmap", "Load a beatmap asset before saving.", "OK");
                return;
            }

            _beatmap.Notes.Sort((a, b) => a.HitTime.CompareTo(b.HitTime));
            string path = AssetDatabase.GetAssetPath(_jsonAsset);
            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("AO Beatmap", "Could not resolve the selected asset path.", "OK");
                return;
            }

            File.WriteAllText(path, JsonUtility.ToJson(_beatmap, true));
            AssetDatabase.ImportAsset(path);
            AssetDatabase.Refresh();
        }
    }
}
