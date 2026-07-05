using AO.Rhythm;
using UnityEngine;

namespace AO.Core
{
    /// <summary>
    /// W1 임시 부트스트랩. Start()에서 비트맵 JSON + AudioClip을 RhythmEngine에 넘겨 자동 재생.
    /// W2에 GameStateManager가 들어오면 이 클래스는 삭제.
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RhythmEngine _rhythmEngine;
        [SerializeField] private TextAsset _beatmapJson;
        [SerializeField] private AudioClip _bgmClip;

        [Header("Options")]
        [SerializeField] private bool _autoStartOnPlay = true;
        [Tooltip("Play Mode 진입 후 곡 시작까지 추가 지연(초). RhythmEngine 자체 leadIn 외에.")]
        [SerializeField, Range(0f, 5f)] private float _extraDelay = 0.5f;

        private void Start()
        {
            if (!_autoStartOnPlay) return;

            if (_rhythmEngine == null || _beatmapJson == null || _bgmClip == null)
            {
                Debug.LogError("[Bootstrap] References missing. Assign RhythmEngine, BeatmapJson, BgmClip in inspector.");
                return;
            }

            Invoke(nameof(StartSongNow), _extraDelay);
        }

        private void StartSongNow()
        {
            BeatmapData data = BeatmapLoader.FromJson(_beatmapJson);
            if (data == null)
            {
                Debug.LogError("[Bootstrap] Beatmap parse failed.");
                return;
            }
            Debug.Log($"[Bootstrap] Loaded beatmap: {data.SongName}, BPM {data.Bpm:F1}, " +
                      $"Notes {data.Notes.Count}, Duration {data.SongLengthSeconds:F1}s");
            _rhythmEngine.StartSong(data, _bgmClip);
        }
    }
}
