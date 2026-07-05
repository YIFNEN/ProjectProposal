using UnityEngine;

namespace AO.Core
{
    /// <summary>
    /// W1~W2 디버깅용 — EventBus의 모든 이벤트를 Console에 로그.
    /// W3 후 비활성화 또는 enableSelf=false로 끄기.
    ///
    /// Inspector에서 카테고리별 토글 가능. Oxygen은 임계 이상 변화일 때만 로그(스팸 방지).
    /// </summary>
    public class EventLogger : MonoBehaviour
    {
        [Header("Logger")]
        [SerializeField] private bool _enableLogging = false;

        [Header("Log Categories")]
        [SerializeField] private bool _logRhythm = true;
        [SerializeField] private bool _logJudgement = true;
        [SerializeField] private bool _logOxygen = true;
        [SerializeField] private bool _logCombo = true;
        [SerializeField] private bool _logScore = true;
        [SerializeField] private bool _logFever = true;

        [Header("Spam Control")]
        [SerializeField, Tooltip("Oxygen 변화가 이 값 이상일 때만 로그 (단위: %)")]
        [Range(0.1f, 20f)] private float _oxygenLogThreshold = 5f;
        [SerializeField, Tooltip("Score 변화가 이 값 이상일 때만 로그")]
        private int _scoreLogThreshold = 100;

        private float _lastLoggedOxygen = -1000f;
        private int _lastLoggedScore = int.MinValue;

        private void OnEnable()
        {
            if (!_enableLogging) return;

            if (_logRhythm)
            {
                EventBus.SongStarted += LogSongStarted;
                EventBus.SongInteractionStarted += LogSongInteractionStarted;
                EventBus.SongEnded += LogSongEnded;
            }
            if (_logJudgement)
            {
                EventBus.NoteJudged += LogJudgement;
                EventBus.FishStrokeSucceeded += LogFishSucceeded;
                EventBus.FishStrokeFailed += LogFishFailed;
            }
            if (_logOxygen)
            {
                EventBus.OxygenChanged += LogOxygen;
                EventBus.OxygenCritical += LogCritical;
                EventBus.OxygenRecovered += LogRecovered;
                EventBus.GameOver += LogGameOver;
            }
            if (_logCombo)
            {
                EventBus.ComboChanged += LogCombo;
                EventBus.MultiplierChanged += LogMultiplier;
            }
            if (_logScore)
            {
                EventBus.ScoreChanged += LogScore;
            }
            if (_logFever)
            {
                EventBus.FeverGaugeChanged += LogFeverGauge;
                EventBus.FeverActivated += LogFeverActivated;
                EventBus.FeverEnded += LogFeverEnded;
            }
        }

        private void OnDisable()
        {
            EventBus.SongStarted -= LogSongStarted;
            EventBus.SongInteractionStarted -= LogSongInteractionStarted;
            EventBus.SongEnded -= LogSongEnded;
            EventBus.NoteJudged -= LogJudgement;
            EventBus.FishStrokeSucceeded -= LogFishSucceeded;
            EventBus.FishStrokeFailed -= LogFishFailed;
            EventBus.OxygenChanged -= LogOxygen;
            EventBus.OxygenCritical -= LogCritical;
            EventBus.OxygenRecovered -= LogRecovered;
            EventBus.GameOver -= LogGameOver;
            EventBus.ComboChanged -= LogCombo;
            EventBus.MultiplierChanged -= LogMultiplier;
            EventBus.ScoreChanged -= LogScore;
            EventBus.FeverGaugeChanged -= LogFeverGauge;
            EventBus.FeverActivated -= LogFeverActivated;
            EventBus.FeverEnded -= LogFeverEnded;
        }

        private void LogSongStarted(double dsp) => Debug.Log($"[Event] SongStarted dspStart={dsp:F2}");
        private void LogSongInteractionStarted() => Debug.Log("[Event] SongInteractionStarted");
        private void LogSongEnded(bool cleared) => Debug.Log($"[Event] SongEnded cleared={cleared}");
        private void LogJudgement(NoteJudgedEvent e) =>
            Debug.Log($"[Event] {(e.IsFeverHit ? "FeverHit" : e.Result.ToString())} lane={e.Lane} delta={e.TimingDelta * 1000:+0;-0}ms");
        private void LogFishSucceeded() => Debug.Log("[Event] FishStrokeSucceeded");
        private void LogFishFailed() => Debug.Log("[Event] FishStrokeFailed");
        private void LogOxygen(float o)
        {
            if (Mathf.Abs(o - _lastLoggedOxygen) < _oxygenLogThreshold) return;
            _lastLoggedOxygen = o;
            Debug.Log($"[Event] Oxygen={o:F1}%");
        }
        private void LogCritical() => Debug.LogWarning("[Event] Oxygen CRITICAL");
        private void LogRecovered() => Debug.Log("[Event] Oxygen recovered");
        private void LogGameOver() => Debug.LogWarning("[Event] GAME OVER");
        private void LogCombo(int c) => Debug.Log($"[Event] Combo={c}");
        private void LogMultiplier(float m) => Debug.Log($"[Event] Multiplier x{m:F1}");
        private void LogScore(int s)
        {
            if (_lastLoggedScore != int.MinValue &&
                Mathf.Abs((long)s - _lastLoggedScore) < _scoreLogThreshold)
            {
                return;
            }

            _lastLoggedScore = s;
            Debug.Log($"[Event] Score={s}");
        }
        private void LogFeverGauge(float g) => Debug.Log($"[Event] FeverGauge={g * 100:F0}%");
        private void LogFeverActivated() => Debug.Log("[Event] FEVER!");
        private void LogFeverEnded() => Debug.Log("[Event] Fever ended");
    }
}
