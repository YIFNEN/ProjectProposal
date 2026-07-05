using System;
using UnityEngine;

namespace AO.Core
{
    [CreateAssetMenu(fileName = "SongLibrary", menuName = "AO/Song Library", order = 102)]
    public class SongLibrary : ScriptableObject
    {
        public SongDefinition[] Songs = Array.Empty<SongDefinition>();

        public SongDefinition FirstPlayableSong()
        {
            for (int i = 0; i < Songs.Length; i++)
            {
                if (Songs[i] != null && Songs[i].IsPlayable) return Songs[i];
            }

            return null;
        }

        public SongDefinition FindById(string songId)
        {
            if (string.IsNullOrEmpty(songId)) return FirstPlayableSong();

            for (int i = 0; i < Songs.Length; i++)
            {
                SongDefinition song = Songs[i];
                if (song != null && song.SongId == songId) return song;
            }

            return FirstPlayableSong();
        }
    }
}
