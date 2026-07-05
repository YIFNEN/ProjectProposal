using AO.Core;
using UnityEditor;
using UnityEngine;

namespace AO.Editor
{
    public static class SongLibraryEditorTools
    {
        private const string SongFolder = "Assets/_Project/Settings/Songs";
        private const string SongLibraryPath = "Assets/_Project/Settings/SongLibrary.asset";

        [MenuItem("AO/Songs/Create Blank Song Definition")]
        public static void CreateBlankSongDefinition()
        {
            EnsureFolder("Assets/_Project/Settings");
            EnsureFolder(SongFolder);

            string path = AssetDatabase.GenerateUniqueAssetPath($"{SongFolder}/Song_New.asset");
            SongDefinition song = ScriptableObject.CreateInstance<SongDefinition>();
            song.SongId = MakeSongIdFromAssetPath(path);
            song.DisplayName = "New Song";
            song.DefaultPlaybackSpeed = 1f;
            song.DefaultNoteSpeed = 1f;
            song.DefaultAudioOffsetSeconds = 0f;

            AssetDatabase.CreateAsset(song, path);
            AppendToLibrary(song);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = song;
            EditorGUIUtility.PingObject(song);
            Debug.Log($"[AO] Created song definition and added it to SongLibrary: {path}");
        }

        [MenuItem("AO/Songs/Add Selected Song To Library")]
        public static void AddSelectedSongToLibrary()
        {
            SongDefinition song = Selection.activeObject as SongDefinition;
            if (song == null)
            {
                Debug.LogWarning("[AO] Select a SongDefinition asset first.");
                return;
            }

            AppendToLibrary(song);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AO] Added song to SongLibrary: {song.DisplayName}");
        }

        [MenuItem("AO/Songs/Add Selected Song To Library", true)]
        public static bool ValidateAddSelectedSongToLibrary()
        {
            return Selection.activeObject is SongDefinition;
        }

        private static void AppendToLibrary(SongDefinition song)
        {
            SongLibrary library = AssetDatabase.LoadAssetAtPath<SongLibrary>(SongLibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SongLibrary>();
                AssetDatabase.CreateAsset(library, SongLibraryPath);
            }

            SongDefinition[] songs = library.Songs ?? new SongDefinition[0];
            for (int i = 0; i < songs.Length; i++)
            {
                if (songs[i] == song) return;
                if (songs[i] != null && !string.IsNullOrEmpty(song.SongId) && songs[i].SongId == song.SongId) return;
            }

            SongDefinition[] expanded = new SongDefinition[songs.Length + 1];
            for (int i = 0; i < songs.Length; i++) expanded[i] = songs[i];
            expanded[expanded.Length - 1] = song;
            library.Songs = expanded;
            EditorUtility.SetDirty(library);
        }

        private static string MakeSongIdFromAssetPath(string path)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            return fileName.ToLowerInvariant().Replace("song_", "").Replace(" ", "_");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
