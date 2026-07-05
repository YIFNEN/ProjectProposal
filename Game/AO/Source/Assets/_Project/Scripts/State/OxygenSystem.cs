using AO.Core;
using UnityEngine;

namespace AO.State
{
    /// <summary>
    /// 산소 게이지 상태 머신. OxygenConfig 수치 기반.
    ///
    /// 동작:
    ///   - 곡 시작 → Oxygen = InitialOxygen, IsGameOver=false
    ///   - 매 프레임 NaturalDrainPerSecond 만큼 감소 (피버 중 PauseDuringFever=true이면 정지)
    ///   - NoteJudged: PERFECT/GOOD/MISS 에 따라 회복·패널티
    ///   - FishStrokeSucceeded: +FishStrokeRecovery
    ///   - CriticalThreshold 진입 → EventBus.OxygenCritical, 탈출 → OxygenRecovered
    ///   - 0% 도달 → IsGameOver=true, EventBus.GameOver
    /// </summary>
    public class OxygenSystem : MonoBehaviour
    {
        [SerializeField] private OxygenConfig _config;
        [SerializeField, Tooltip("마지막 노트 판정/쓰다듬 가능 시간이 지나면 곡 종료 전이라도 산소 자연 감소를 멈춘다.")]
        private bool _pauseNaturalDrainAfterLastInteraction = true;
        [SerializeField, Tooltip("Keep natural oxygen drain paused until the first beatmap note is actually spawned/visible.")]
        private bool _pauseNaturalDrainBeforeFirstInteraction = true;

        public float Oxygen { get; private set; }
        public bool IsCritical { get; private set; }
        public bool IsGameOver { get; private set; }

        private bool _isFeverActive;
        private bool _isSongPlaying;
        private bool _rulesEnabled = true;
        private bool _isPaused;
        private bool _naturalDrainPausedForSongHead;
        private bool _naturalDrainPausedForSongTail;
        private double _songDspStartTime;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError("[OxygenSystem] OxygenConfig not assigned in inspector.");
                enabled = false;
                return;
            }
            Oxygen = _config.InitialOxygen;
        }

        private void OnEnable()
        {
            EventBus.NoteJudged += HandleNoteJudged;
            EventBus.FishStrokeSucceeded += HandleFishSucceeded;
            EventBus.FeverActivated += HandleFeverActivated;
            EventBus.FeverEnded += HandleFeverEnded;
            EventBus.SongStarted += HandleSongStarted;
            EventBus.SongInteractionStarted += HandleSongInteractionStarted;
            EventBus.SongEnded += HandleSongEnded;
            EventBus.SongInteractionEnded += HandleSongInteractionEnded;
            EventBus.GameplayPauseChanged += HandleGameplayPauseChanged;
        }

        private void OnDisable()
        {
            EventBus.NoteJudged -= HandleNoteJudged;
            EventBus.FishStrokeSucceeded -= HandleFishSucceeded;
            EventBus.FeverActivated -= HandleFeverActivated;
            EventBus.FeverEnded -= HandleFeverEnded;
            EventBus.SongStarted -= HandleSongStarted;
            EventBus.SongInteractionStarted -= HandleSongInteractionStarted;
            EventBus.SongEnded -= HandleSongEnded;
            EventBus.SongInteractionEnded -= HandleSongInteractionEnded;
            EventBus.GameplayPauseChanged -= HandleGameplayPauseChanged;
        }

        private void Update()
        {
            if (!_isSongPlaying || IsGameOver) return;
            if (!_rulesEnabled || _isPaused) return;
            if (_naturalDrainPausedForSongHead) return;
            if (_naturalDrainPausedForSongTail) return;
            if (AudioSettings.dspTime < _songDspStartTime) return;
            if (_isFeverActive && _config.PauseDuringFever) return;

            float prev = Oxygen;
            Oxygen = Mathf.Max(0f, Oxygen - _config.NaturalDrainPerSecond * Time.deltaTime);
            if (!Mathf.Approximately(Oxygen, prev)) EventBus.RaiseOxygenChanged(Oxygen);

            CheckThresholds();
        }

        private void HandleNoteJudged(NoteJudgedEvent e)
        {
            if (IsGameOver) return;
            if (!_rulesEnabled) return;
            switch (e.Result)
            {
                case JudgementResult.Perfect: Modify(+_config.PerfectRecovery); break;
                case JudgementResult.Good:    Modify(+_config.GoodRecovery); break;
                case JudgementResult.Miss:    Modify(-_config.MissPenalty); break;
            }
        }

        private void HandleFishSucceeded()
        {
            if (IsGameOver) return;
            if (!_rulesEnabled) return;
            Modify(+_config.FishStrokeRecovery);
        }

        private void HandleFeverActivated() => _isFeverActive = true;
        private void HandleFeverEnded() => _isFeverActive = false;

        private void HandleSongStarted(double dspStartTime)
        {
            Oxygen = _config.InitialOxygen;
            IsCritical = false;
            IsGameOver = false;
            _isSongPlaying = true;
            _naturalDrainPausedForSongHead = _pauseNaturalDrainBeforeFirstInteraction;
            _naturalDrainPausedForSongTail = false;
            _songDspStartTime = dspStartTime;
            EventBus.RaiseOxygenChanged(Oxygen);
        }

        private void HandleSongInteractionStarted() => _naturalDrainPausedForSongHead = false;
        private void HandleSongEnded(bool _)
        {
            _isSongPlaying = false;
            _naturalDrainPausedForSongHead = false;
        }
        private void HandleSongInteractionEnded()
        {
            if (_pauseNaturalDrainAfterLastInteraction)
            {
                _naturalDrainPausedForSongTail = true;
            }
        }
        private void HandleGameplayPauseChanged(bool paused) => _isPaused = paused;

        public void SetRulesEnabled(bool enabled)
        {
            _rulesEnabled = enabled;
        }

        public void RefillToInitial()
        {
            bool wasCritical = IsCritical;
            Oxygen = _config.InitialOxygen;
            IsCritical = false;
            IsGameOver = false;
            EventBus.RaiseOxygenChanged(Oxygen);
            if (wasCritical) EventBus.RaiseOxygenRecovered();
        }

        private void Modify(float delta)
        {
            float prev = Oxygen;
            Oxygen = Mathf.Clamp(Oxygen + delta, 0f, _config.MaxOxygen);
            if (!Mathf.Approximately(Oxygen, prev)) EventBus.RaiseOxygenChanged(Oxygen);
            CheckThresholds();
        }

        private void CheckThresholds()
        {
            if (!IsCritical && Oxygen <= _config.CriticalThreshold)
            {
                IsCritical = true;
                EventBus.RaiseOxygenCritical();
            }
            else if (IsCritical && Oxygen > _config.CriticalThreshold)
            {
                IsCritical = false;
                EventBus.RaiseOxygenRecovered();
            }

            if (!IsGameOver && Oxygen <= 0f)
            {
                IsGameOver = true;
                _isSongPlaying = false;
                EventBus.RaiseGameOver();
            }
        }
    }
}
