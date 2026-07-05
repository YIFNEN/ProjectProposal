using AO.Core;
using UnityEngine;

namespace AO.State
{
    /// <summary>
    /// 피버 게이지와 7초 발동 상태를 관리한다.
    /// 판정 이벤트를 프레임 끝에서 처리해 ComboSystem의 콤보 갱신 순서에 덜 민감하게 동작한다.
    /// </summary>
    public class FeverSystem : MonoBehaviour
    {
        public static FeverSystem Instance { get; private set; }

        [SerializeField] private FeverConfig _config;
        [SerializeField, Tooltip("콤보 임계값 판정용. 같은 GO에 있으면 자동 인식")]
        private ComboSystem _comboSystem;

        public float Gauge { get; private set; }
        public float GaugeRatio => _config == null || _config.GaugeMax <= 0f ? 0f : Mathf.Clamp01(Gauge / _config.GaugeMax);
        public bool IsActive { get; private set; }
        public bool ShouldAutoPerfect => IsActive && _config != null && _config.AutoPerfectDuringFever;

        private bool _isSongPlaying;
        private float _activeTimer;
        private int _pendingGoodHits;
        private int _pendingMisses;
        private int _pendingFishSuccesses;
        private bool _isPaused;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[FeverSystem] Duplicate instance, destroying self.");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (_config == null)
            {
                Debug.LogError("[FeverSystem] FeverConfig not assigned in inspector.");
                enabled = false;
                return;
            }

            if (_comboSystem == null) _comboSystem = GetComponent<ComboSystem>();
        }

        private void OnEnable()
        {
            EventBus.NoteJudged += HandleNoteJudged;
            EventBus.FishStrokeSucceeded += HandleFishSucceeded;
            EventBus.SongStarted += HandleSongStarted;
            EventBus.SongEnded += HandleSongEnded;
            EventBus.GameOver += HandleGameOver;
            EventBus.GameplayPauseChanged += HandleGameplayPauseChanged;
        }

        private void OnDisable()
        {
            EventBus.NoteJudged -= HandleNoteJudged;
            EventBus.FishStrokeSucceeded -= HandleFishSucceeded;
            EventBus.SongStarted -= HandleSongStarted;
            EventBus.SongEnded -= HandleSongEnded;
            EventBus.GameOver -= HandleGameOver;
            EventBus.GameplayPauseChanged -= HandleGameplayPauseChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_isSongPlaying || _isPaused) return;

            if (IsActive)
            {
                _activeTimer -= Time.deltaTime;
                float duration = _config != null ? Mathf.Max(0.001f, _config.DurationSeconds) : 1f;
                EventBus.RaiseFeverGaugeChanged(Mathf.Clamp01(_activeTimer / duration));
                if (_activeTimer <= 0f) EndFever();
                return;
            }

            ApplyComboHoldGain(Time.deltaTime);
        }

        private void ApplyComboHoldGain(float deltaTime)
        {
            if (_config == null || _config.ComboHoldIncrementPerSecond <= 0f || deltaTime <= 0f) return;
            if (_comboSystem == null || _comboSystem.Combo <= 0) return;
            if (!CanGainFromCombo()) return;

            float delta = _config.ComboHoldIncrementPerSecond * deltaTime;
            if (delta > 0f)
            {
                ModifyGauge(delta);
            }
        }

        private void LateUpdate()
        {
            if (!_isSongPlaying || _isPaused || IsActive)
            {
                ClearPending();
                return;
            }

            if (_pendingMisses > 0)
            {
                ModifyGauge(-_config.MissDecrement * _pendingMisses);
            }

            if (_pendingFishSuccesses > 0)
            {
                ModifyGauge(_config.FishStrokeIncrement * _pendingFishSuccesses);
            }

            if (_pendingGoodHits > 0 && CanGainFromCombo())
            {
                ModifyGauge(_config.NoteIncrement * _pendingGoodHits);
            }

            ClearPending();
        }

        private void HandleNoteJudged(NoteJudgedEvent e)
        {
            if (e.Result == JudgementResult.Miss) _pendingMisses++;
            else _pendingGoodHits++;
        }

        private void HandleFishSucceeded() => _pendingFishSuccesses++;

        private void HandleSongStarted(double _)
        {
            _isSongPlaying = true;
            IsActive = false;
            _activeTimer = 0f;
            Gauge = 0f;
            ClearPending();
            EventBus.RaiseFeverGaugeChanged(GaugeRatio);
        }

        private void HandleSongEnded(bool _) => StopFeverState();
        private void HandleGameOver() => StopFeverState();
        private void HandleGameplayPauseChanged(bool paused) => _isPaused = paused;

        private bool CanGainFromCombo()
        {
            if (_comboSystem == null) return !_config.FreezeBelowThreshold;
            return _comboSystem.Combo >= _config.ComboThreshold || !_config.FreezeBelowThreshold;
        }

        private void ModifyGauge(float delta)
        {
            if (Mathf.Approximately(delta, 0f)) return;

            Gauge = Mathf.Clamp(Gauge + delta, 0f, _config.GaugeMax);
            EventBus.RaiseFeverGaugeChanged(GaugeRatio);

            if (Gauge >= _config.GaugeMax)
            {
                ActivateFever();
            }
        }

        private void ActivateFever()
        {
            if (IsActive) return;

            Gauge = 0f;
            IsActive = true;
            _activeTimer = _config.DurationSeconds;
            EventBus.RaiseFeverActivated();
            EventBus.RaiseFeverGaugeChanged(1f);
        }

        private void EndFever()
        {
            if (!IsActive) return;

            IsActive = false;
            _activeTimer = 0f;
            Gauge = 0f;
            EventBus.RaiseFeverGaugeChanged(GaugeRatio);
            EventBus.RaiseFeverEnded();
        }

        private void StopFeverState()
        {
            _isSongPlaying = false;
            bool wasActive = IsActive;
            IsActive = false;
            _activeTimer = 0f;
            Gauge = 0f;
            ClearPending();
            EventBus.RaiseFeverGaugeChanged(GaugeRatio);
            if (wasActive) EventBus.RaiseFeverEnded();
        }

        private void ClearPending()
        {
            _pendingGoodHits = 0;
            _pendingMisses = 0;
            _pendingFishSuccesses = 0;
        }
    }
}
