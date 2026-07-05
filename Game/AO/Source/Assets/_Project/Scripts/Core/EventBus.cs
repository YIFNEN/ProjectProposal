using System;

namespace AO.Core
{
    public enum JudgementResult
    {
        Perfect,
        Good,
        Miss
    }

    public struct NoteJudgedEvent
    {
        public JudgementResult Result;
        public int Lane;
        public double TimingDelta;
        public bool IsFeverHit;
    }

    /// <summary>
    /// 시스템 간 느슨한 결합용 정적 이벤트 허브.
    /// 발행자(예: JudgementSystem)가 Raise* 호출 → 구독자(HUDController, OxygenSystem 등)가 이벤트 수신.
    /// 사용 시 Subscribe / Unsubscribe(보통 OnEnable / OnDisable)에서.
    /// </summary>
    public static class EventBus
    {
        // Rhythm
        public static event Action<double> SongStarted;
        public static event Action SongInteractionStarted; // First note has become visible/spawned.
        public static event Action<bool> SongEnded; // true = cleared, false = aborted
        public static event Action SongInteractionEnded; // Last note judgement/stroke window has passed.

        // Judgement
        public static event Action<NoteJudgedEvent> NoteJudged;
        public static event Action FishStrokeSucceeded;
        public static event Action FishStrokeFailed;

        // Oxygen
        public static event Action<float> OxygenChanged; // 0~100
        public static event Action OxygenCritical;       // 임계 진입
        public static event Action OxygenRecovered;      // 임계 탈출
        public static event Action GameOver;

        // Combo / Score
        public static event Action<int> ComboChanged;
        public static event Action<float> MultiplierChanged;
        public static event Action<int> ScoreChanged;

        // Fever
        public static event Action<float> FeverGaugeChanged; // 0~1 비율
        public static event Action FeverActivated;
        public static event Action FeverEnded;

        // Gameplay flow
        public static event Action EternalExitRequested;
        public static event Action<bool> GameplayPauseChanged;

        // ─── Raise helpers ────────────────────────────────────────────────
        public static void RaiseSongStarted(double dspStart) => SongStarted?.Invoke(dspStart);
        public static void RaiseSongInteractionStarted() => SongInteractionStarted?.Invoke();
        public static void RaiseSongEnded(bool cleared) => SongEnded?.Invoke(cleared);
        public static void RaiseSongInteractionEnded() => SongInteractionEnded?.Invoke();
        public static void RaiseNoteJudged(NoteJudgedEvent e) => NoteJudged?.Invoke(e);
        public static void RaiseFishStrokeSucceeded() => FishStrokeSucceeded?.Invoke();
        public static void RaiseFishStrokeFailed() => FishStrokeFailed?.Invoke();
        public static void RaiseOxygenChanged(float v) => OxygenChanged?.Invoke(v);
        public static void RaiseOxygenCritical() => OxygenCritical?.Invoke();
        public static void RaiseOxygenRecovered() => OxygenRecovered?.Invoke();
        public static void RaiseGameOver() => GameOver?.Invoke();
        public static void RaiseComboChanged(int c) => ComboChanged?.Invoke(c);
        public static void RaiseMultiplierChanged(float m) => MultiplierChanged?.Invoke(m);
        public static void RaiseScoreChanged(int s) => ScoreChanged?.Invoke(s);
        public static void RaiseFeverGaugeChanged(float r) => FeverGaugeChanged?.Invoke(r);
        public static void RaiseFeverActivated() => FeverActivated?.Invoke();
        public static void RaiseFeverEnded() => FeverEnded?.Invoke();
        public static void RaiseEternalExitRequested() => EternalExitRequested?.Invoke();
        public static void RaiseGameplayPauseChanged(bool paused) => GameplayPauseChanged?.Invoke(paused);

        /// <summary>
        /// 씬 전환 시 모든 이벤트 핸들러를 정리. 메모리 누수·잘못된 콜백 방지용.
        /// SceneTransition에서 호출.
        /// </summary>
        public static void ClearAllSubscribers()
        {
            SongStarted = null;
            SongInteractionStarted = null;
            SongEnded = null;
            SongInteractionEnded = null;
            NoteJudged = null;
            FishStrokeSucceeded = null;
            FishStrokeFailed = null;
            OxygenChanged = null;
            OxygenCritical = null;
            OxygenRecovered = null;
            GameOver = null;
            ComboChanged = null;
            MultiplierChanged = null;
            ScoreChanged = null;
            FeverGaugeChanged = null;
            FeverActivated = null;
            FeverEnded = null;
            EternalExitRequested = null;
            GameplayPauseChanged = null;
        }
    }
}
