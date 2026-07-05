using AO.Core;
using UnityEngine;

namespace AO.State
{
    /// <summary>
    /// 콤보 + 점수 배율 상태 머신.
    ///
    /// 동작:
    ///   - NoteJudged PERFECT/GOOD → 콤보 +1
    ///   - NoteJudged MISS         → 콤보 0
    ///   - FishStrokeSucceeded     → 콤보 +1
    ///   - FishStrokeFailed        → 콤보 0
    ///   - ComboConfig.GetMultiplier(콤보) 로 배율 계산
    ///   - 변화 시 EventBus.ComboChanged / MultiplierChanged 발행
    /// </summary>
    public class ComboSystem : MonoBehaviour
    {
        [SerializeField] private ComboConfig _config;

        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public float Multiplier { get; private set; } = 1f;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError("[ComboSystem] ComboConfig not assigned in inspector.");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            EventBus.NoteJudged += HandleNoteJudged;
            EventBus.FishStrokeSucceeded += HandleFishSucceeded;
            EventBus.FishStrokeFailed += HandleFishFailed;
            EventBus.SongStarted += HandleSongStarted;
        }

        private void OnDisable()
        {
            EventBus.NoteJudged -= HandleNoteJudged;
            EventBus.FishStrokeSucceeded -= HandleFishSucceeded;
            EventBus.FishStrokeFailed -= HandleFishFailed;
            EventBus.SongStarted -= HandleSongStarted;
        }

        private void HandleNoteJudged(NoteJudgedEvent e)
        {
            if (e.Result == JudgementResult.Miss) ResetCombo();
            else IncrementCombo();
        }

        private void HandleFishSucceeded() => IncrementCombo();
        private void HandleFishFailed() => ResetCombo();

        private void HandleSongStarted(double _)
        {
            Combo = 0;
            MaxCombo = 0;
            UpdateMultiplier();
            EventBus.RaiseComboChanged(Combo);
        }

        private void IncrementCombo()
        {
            Combo++;
            if (Combo > MaxCombo) MaxCombo = Combo;
            UpdateMultiplier();
            EventBus.RaiseComboChanged(Combo);
        }

        private void ResetCombo()
        {
            if (Combo == 0) return;
            Combo = 0;
            UpdateMultiplier();
            EventBus.RaiseComboChanged(Combo);
        }

        private void UpdateMultiplier()
        {
            float prev = Multiplier;
            Multiplier = _config.GetMultiplier(Combo);
            if (!Mathf.Approximately(prev, Multiplier))
            {
                EventBus.RaiseMultiplierChanged(Multiplier);
            }
        }
    }
}
