using System;
using AO.Judgement;
using UnityEngine;

namespace AO.Core
{
    public class RunStatsTracker : MonoBehaviour
    {
        public static RunStatsTracker Instance { get; private set; }

        public int PerfectCount { get; private set; }
        public int GoodCount { get; private set; }
        public int MissCount { get; private set; }
        public int FishSuccessCount { get; private set; }
        public int FishFailCount { get; private set; }
        public int MaxCombo { get; private set; }
        public int Score { get; private set; }
        public int FeverActivations { get; private set; }
        public double StartDspTime { get; private set; }

        [Header("Hand Range Tracking")]
        [SerializeField] private Transform _leftHandRangeSource;
        [SerializeField] private Transform _rightHandRangeSource;
        [SerializeField] private Transform _handRangeReference;
        [SerializeField] private bool _autoBindHandRangeSources = true;
        [SerializeField] private bool _sampleHandRangeInReferenceSpace = true;

        private HandRangeStats _leftHandRange;
        private HandRangeStats _rightHandRange;
        private bool _isTrackingHandRange;

        public float WeightedAccuracy
        {
            get
            {
                int total = PerfectCount + GoodCount + MissCount + FishSuccessCount + FishFailCount;
                if (total <= 0) return 0f;
                float weighted = PerfectCount + GoodCount * 0.5f + FishSuccessCount;
                return Mathf.Clamp01(weighted / total);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            AutoBindHandRangeSources();
        }

        private void Update()
        {
            if (!_isTrackingHandRange) return;
            SampleHandRanges();
        }

        private void OnEnable()
        {
            EventBus.SongStarted += HandleSongStarted;
            EventBus.NoteJudged += HandleNoteJudged;
            EventBus.FishStrokeSucceeded += HandleFishSucceeded;
            EventBus.FishStrokeFailed += HandleFishFailed;
            EventBus.ComboChanged += HandleComboChanged;
            EventBus.ScoreChanged += HandleScoreChanged;
            EventBus.FeverActivated += HandleFeverActivated;
        }

        private void OnDisable()
        {
            EventBus.SongStarted -= HandleSongStarted;
            EventBus.NoteJudged -= HandleNoteJudged;
            EventBus.FishStrokeSucceeded -= HandleFishSucceeded;
            EventBus.FishStrokeFailed -= HandleFishFailed;
            EventBus.ComboChanged -= HandleComboChanged;
            EventBus.ScoreChanged -= HandleScoreChanged;
            EventBus.FeverActivated -= HandleFeverActivated;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public SessionResult BuildResult(SessionResultStatus status, GameSession session, string songId, string songName)
        {
            _isTrackingHandRange = false;
            float accuracy = WeightedAccuracy;
            return new SessionResult
            {
                Status = status,
                Mode = session != null ? session.Mode : PlayMode.Normal,
                SongId = songId,
                SongName = songName,
                Score = Score,
                MaxCombo = MaxCombo,
                WeightedAccuracy = accuracy,
                Rank = GetRank(accuracy),
                PerfectCount = PerfectCount,
                GoodCount = GoodCount,
                MissCount = MissCount,
                FishStrokeSuccessCount = FishSuccessCount,
                FishStrokeFailCount = FishFailCount,
                DurationSeconds = Mathf.Max(0f, (float)(AudioSettings.dspTime - StartDspTime)),
                FeverActivations = FeverActivations,
                PlaybackSpeed = session != null ? session.PlaybackSpeed : 1f,
                NoteSpeed = session != null ? session.NoteSpeed : 1f,
                LeftHandRange = _leftHandRange,
                RightHandRange = _rightHandRange,
                HandRangeIsLocal = _sampleHandRangeInReferenceSpace && _handRangeReference != null,
                HandRangeReferenceName = _sampleHandRangeInReferenceSpace && _handRangeReference != null
                    ? _handRangeReference.name
                    : "World"
            };
        }

        public static string GetRank(float weightedAccuracy)
        {
            if (weightedAccuracy >= 0.9f) return "S";
            if (weightedAccuracy >= 0.8f) return "A";
            if (weightedAccuracy >= 0.7f) return "B";
            if (weightedAccuracy >= 0.6f) return "C";
            if (weightedAccuracy >= 0.5f) return "D";
            return "F";
        }

        private void ResetStats(double dspStart)
        {
            PerfectCount = 0;
            GoodCount = 0;
            MissCount = 0;
            FishSuccessCount = 0;
            FishFailCount = 0;
            MaxCombo = 0;
            Score = 0;
            FeverActivations = 0;
            StartDspTime = dspStart;
            ResetHandRangeStats();
            AutoBindHandRangeSources();
            _isTrackingHandRange = true;
            SampleHandRanges();
        }

        private void HandleSongStarted(double dspStart) => ResetStats(dspStart);

        private void HandleNoteJudged(NoteJudgedEvent e)
        {
            switch (e.Result)
            {
                case JudgementResult.Perfect:
                    PerfectCount++;
                    break;
                case JudgementResult.Good:
                    GoodCount++;
                    break;
                case JudgementResult.Miss:
                    MissCount++;
                    break;
            }
        }

        private void HandleFishSucceeded() => FishSuccessCount++;
        private void HandleFishFailed() => FishFailCount++;
        private void HandleComboChanged(int combo) => MaxCombo = Mathf.Max(MaxCombo, combo);
        private void HandleScoreChanged(int score) => Score = score;
        private void HandleFeverActivated() => FeverActivations++;

        private void ResetHandRangeStats()
        {
            _leftHandRange = default;
            _rightHandRange = default;
        }

        private void SampleHandRanges()
        {
            SampleHandRange(_leftHandRangeSource, ref _leftHandRange);
            SampleHandRange(_rightHandRangeSource, ref _rightHandRange);
        }

        private void SampleHandRange(Transform source, ref HandRangeStats range)
        {
            if (source == null || !source.gameObject.activeInHierarchy) return;

            Vector3 position = _sampleHandRangeInReferenceSpace && _handRangeReference != null
                ? _handRangeReference.InverseTransformPoint(source.position)
                : source.position;

            if (!range.HasSamples)
            {
                range.HasSamples = true;
                range.Min = position;
                range.Max = position;
                return;
            }

            range.Min = Vector3.Min(range.Min, position);
            range.Max = Vector3.Max(range.Max, position);
        }

        private void AutoBindHandRangeSources()
        {
            if (!_autoBindHandRangeSources) return;
            if (_leftHandRangeSource != null && _rightHandRangeSource != null && _handRangeReference != null) return;

            JudgementHandTarget[] targets = FindObjectsByType<JudgementHandTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (JudgementHandTarget target in targets)
            {
                if (target == null) continue;
                string targetName = target.gameObject.name;

                if (_leftHandRangeSource == null && targetName.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _leftHandRangeSource = target.transform;
                }
                else if (_rightHandRangeSource == null && targetName.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _rightHandRangeSource = target.transform;
                }
            }

            if (_handRangeReference != null) return;
            Transform leftParent = _leftHandRangeSource != null ? _leftHandRangeSource.parent : null;
            Transform rightParent = _rightHandRangeSource != null ? _rightHandRangeSource.parent : null;
            if (leftParent != null && leftParent == rightParent)
            {
                _handRangeReference = leftParent;
            }
        }
    }
}
