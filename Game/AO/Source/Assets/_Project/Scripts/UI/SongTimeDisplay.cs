using AO.Core;
using AO.Rhythm;
using TMPro;
using UnityEngine;

namespace AO.UI
{
    [DisallowMultipleComponent]
    public class SongTimeDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text _timeText;
        [SerializeField] private RhythmEngine _rhythmEngine;
        [SerializeField] private bool _showRemaining = true;

        private void Awake()
        {
            if (_timeText == null) _timeText = GetComponent<TMP_Text>();
            FindEngineIfNeeded();
            RefreshText();
        }

        private void OnEnable()
        {
            EventBus.SongStarted += HandleSongStarted;
            EventBus.SongEnded += HandleSongEnded;
            FindEngineIfNeeded();
            RefreshText();
        }

        private void OnDisable()
        {
            EventBus.SongStarted -= HandleSongStarted;
            EventBus.SongEnded -= HandleSongEnded;
        }

        private void Update()
        {
            RefreshText();
        }

        public void Configure(TMP_Text timeText, RhythmEngine rhythmEngine = null)
        {
            _timeText = timeText;
            if (rhythmEngine != null) _rhythmEngine = rhythmEngine;
            RefreshText();
        }

        private void HandleSongStarted(double dspStartTime)
        {
            FindEngineIfNeeded();
            RefreshText();
        }

        private void HandleSongEnded(bool cleared)
        {
            RefreshText();
        }

        private void RefreshText()
        {
            if (_timeText == null) return;
            FindEngineIfNeeded();

            float seconds = 0f;
            if (_rhythmEngine != null)
            {
                seconds = _showRemaining
                    ? _rhythmEngine.RemainingSongTimeSeconds
                    : Mathf.Max(0f, (float)_rhythmEngine.SongTime);
            }

            _timeText.text = FormatTime(seconds);
        }

        private void FindEngineIfNeeded()
        {
            if (_rhythmEngine != null) return;
            _rhythmEngine = FindFirstObjectByType<RhythmEngine>(FindObjectsInactive.Include);
        }

        private static string FormatTime(float seconds)
        {
            int wholeSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
            int minutes = wholeSeconds / 60;
            int remainder = wholeSeconds % 60;
            return $"{minutes}:{remainder:00}";
        }
    }
}
