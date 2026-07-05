using AO.Core;
using TMPro;
using UnityEngine;

namespace AO.UI
{
    /// <summary>
    /// 점수 표시. 점수 증가 시 카운트업 애니메이션 (즉시가 아닌 시청각적으로 누적되는 느낌).
    /// </summary>
    public class ScoreDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text _scoreText;

        [Header("Font")]
        [SerializeField] private bool _applyPreferredFont;

        [Header("Count-Up Animation")]
        [SerializeField, Tooltip("초당 카운트업 점수 (이 속도로 _displayedScore가 _targetScore 따라감)")]
        private float _countUpRate = 5000f;

        private int _targetScore;
        private float _displayedScore;

        private void Awake()
        {
            if (_scoreText == null) _scoreText = GetComponentInChildren<TMP_Text>(true);
            if (_applyPreferredFont) RuntimeUiFactory.ApplyPreferredFont(_scoreText);
        }

        private void OnEnable()
        {
            EventBus.ScoreChanged += HandleScoreChanged;
            EventBus.SongStarted += HandleSongStarted;
            UpdateText(0);
        }

        private void OnDisable()
        {
            EventBus.ScoreChanged -= HandleScoreChanged;
            EventBus.SongStarted -= HandleSongStarted;
        }

        private void Update()
        {
            if (Mathf.Approximately(_displayedScore, _targetScore)) return;

            float dt = Time.deltaTime;
            float diff = _targetScore - _displayedScore;
            float step = Mathf.Sign(diff) * Mathf.Min(_countUpRate * dt, Mathf.Abs(diff));
            _displayedScore += step;

            if (Mathf.Abs(_displayedScore - _targetScore) < 0.5f)
            {
                _displayedScore = _targetScore;
            }
            UpdateText(Mathf.RoundToInt(_displayedScore));
        }

        private void HandleScoreChanged(int score) => _targetScore = score;

        private void HandleSongStarted(double _)
        {
            _targetScore = 0;
            _displayedScore = 0;
            UpdateText(0);
        }

        private void UpdateText(int n)
        {
            if (_scoreText != null) _scoreText.text = n.ToString("N0");
        }
    }
}
