using AO.Core;
using UnityEngine;

namespace AO.State
{
    /// <summary>
    /// 점수 누적 시스템.
    ///
    /// 계산식:
    ///   score += baseScore × comboMultiplier × feverMultiplier
    ///   - baseScore: JudgementConfig.PerfectBaseScore / GoodBaseScore / FishStrokeBaseScore
    ///   - comboMultiplier: ComboSystem.Multiplier
    ///   - feverMultiplier: 피버 활성 시 _feverMultiplier (기본 1.2), 아니면 1.0
    ///   - Fish 성공은 추가로 FishStrokeBonusMultiplier 적용
    ///
    /// 변화 시 EventBus.ScoreChanged 발행.
    /// </summary>
    public class ScoreSystem : MonoBehaviour
    {
        [SerializeField] private JudgementConfig _judgementConfig;
        [SerializeField, Tooltip("ComboSystem 참조 — 같은 GO에 있으면 자동 인식")]
        private ComboSystem _comboSystem;
        [SerializeField, Tooltip("피버 중 점수 배율"), Range(1f, 2f)]
        private float _feverMultiplier = 1.2f;

        public int Score { get; private set; }

        private bool _isFeverActive;
        private int _pendingPerfectNotes;
        private int _pendingGoodNotes;
        private int _pendingFishSuccesses;

        private void Awake()
        {
            if (_judgementConfig == null)
            {
                Debug.LogError("[ScoreSystem] JudgementConfig not assigned in inspector.");
                enabled = false;
                return;
            }
            if (_comboSystem == null) _comboSystem = GetComponent<ComboSystem>();
        }

        private void OnEnable()
        {
            EventBus.NoteJudged += HandleNoteJudged;
            EventBus.FishStrokeSucceeded += HandleFishSucceeded;
            EventBus.FeverActivated += HandleFeverActivated;
            EventBus.FeverEnded += HandleFeverEnded;
            EventBus.SongStarted += HandleSongStarted;
        }

        private void OnDisable()
        {
            EventBus.NoteJudged -= HandleNoteJudged;
            EventBus.FishStrokeSucceeded -= HandleFishSucceeded;
            EventBus.FeverActivated -= HandleFeverActivated;
            EventBus.FeverEnded -= HandleFeverEnded;
            EventBus.SongStarted -= HandleSongStarted;
        }

        private void LateUpdate()
        {
            ApplyPendingScores();
        }

        private void HandleNoteJudged(NoteJudgedEvent e)
        {
            if (e.Result == JudgementResult.Miss) return;
            if (e.Result == JudgementResult.Perfect) _pendingPerfectNotes++;
            else _pendingGoodNotes++;
        }

        private void HandleFishSucceeded()
        {
            _pendingFishSuccesses++;
        }

        private void HandleFeverActivated() => _isFeverActive = true;
        private void HandleFeverEnded() => _isFeverActive = false;

        private void HandleSongStarted(double _)
        {
            Score = 0;
            _pendingPerfectNotes = 0;
            _pendingGoodNotes = 0;
            _pendingFishSuccesses = 0;
            EventBus.RaiseScoreChanged(Score);
        }

        private void ApplyPendingScores()
        {
            if (_pendingPerfectNotes == 0 && _pendingGoodNotes == 0 && _pendingFishSuccesses == 0)
            {
                return;
            }

            float comboMult = _comboSystem != null ? _comboSystem.Multiplier : 1f;
            float feverMult = _isFeverActive ? _feverMultiplier : 1f;
            int delta = 0;

            delta += Mathf.RoundToInt(_pendingPerfectNotes * _judgementConfig.PerfectBaseScore * comboMult * feverMult);
            delta += Mathf.RoundToInt(_pendingGoodNotes * _judgementConfig.GoodBaseScore * comboMult * feverMult);
            delta += Mathf.RoundToInt(
                _pendingFishSuccesses
                * _judgementConfig.FishStrokeBaseScore
                * comboMult
                * feverMult
                * _judgementConfig.FishStrokeBonusMultiplier);

            _pendingPerfectNotes = 0;
            _pendingGoodNotes = 0;
            _pendingFishSuccesses = 0;

            if (delta == 0) return;
            Score += delta;
            EventBus.RaiseScoreChanged(Score);
        }
    }
}
