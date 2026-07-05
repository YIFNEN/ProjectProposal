using AO.Core;
using TMPro;
using UnityEngine;

namespace AO.UI
{
    /// <summary>
    /// 판정 결과 텍스트 팝업. PERFECT/GOOD/MISS/FISH 발생 시 색·텍스트 갱신 후 페이드아웃.
    /// 단일 인스턴스가 매 노트마다 갱신됨 (스폰 풀링 X — VR에서 시야 한 곳에 집중 표시).
    /// </summary>
    public class JudgementPopup : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text _label;

        [Header("Font")]
        [SerializeField] private bool _applyPreferredFont;

        [Header("Timing")]
        [SerializeField] private float _showDuration = 0.45f;
        [SerializeField] private float _fadeDuration = 0.25f;

        [Header("Colors")]
        [SerializeField] private Color _perfectColor = new Color(0.25f, 0.88f, 0.88f);
        [SerializeField] private Color _goodColor    = new Color(0.66f, 0.69f, 1f);
        [SerializeField] private Color _missColor    = new Color(1f, 0.33f, 0.47f);
        [SerializeField] private Color _fishColor    = new Color(1f, 0.82f, 0.48f);

        [Header("Punch")]
        [SerializeField, Range(0f, 0.5f)] private float _punchScale = 0.15f;

        private Vector3 _originalScale;
        private float _timer;

        private void Awake()
        {
            if (_label == null) _label = GetComponentInChildren<TMP_Text>(true);
            if (_applyPreferredFont) RuntimeUiFactory.ApplyPreferredFont(_label);
            _originalScale = transform.localScale;
            SetAlpha(0f);
        }

        private void OnEnable()
        {
            EventBus.NoteJudged += HandleNoteJudged;
            EventBus.FishStrokeSucceeded += HandleFishSucceeded;
            EventBus.FishStrokeFailed += HandleFishFailed;
        }

        private void OnDisable()
        {
            EventBus.NoteJudged -= HandleNoteJudged;
            EventBus.FishStrokeSucceeded -= HandleFishSucceeded;
            EventBus.FishStrokeFailed -= HandleFishFailed;
        }

        private void Update()
        {
            if (_timer <= 0f) return;
            _timer -= Time.deltaTime;

            // 펀치 스케일: 시작 직후 가장 크고 시간에 따라 _originalScale로 수렴
            float totalLife = _showDuration + _fadeDuration;
            float lifeNorm = Mathf.Clamp01(_timer / totalLife);
            transform.localScale = _originalScale * (1f + _punchScale * lifeNorm);

            // 페이드: _timer가 _fadeDuration 이하일 때만
            if (_timer <= _fadeDuration)
            {
                float a = Mathf.Clamp01(_timer / _fadeDuration);
                SetAlpha(a);
            }

            if (_timer <= 0f)
            {
                SetAlpha(0f);
                transform.localScale = _originalScale;
            }
        }

        private void HandleNoteJudged(NoteJudgedEvent e)
        {
            switch (e.Result)
            {
                case JudgementResult.Perfect: Show("PERFECT", _perfectColor); break;
                case JudgementResult.Good:    Show("GOOD", _goodColor); break;
                case JudgementResult.Miss:    Show("MISS", _missColor); break;
            }
        }

        private void HandleFishSucceeded() => Show("FISH!", _fishColor);
        private void HandleFishFailed() => Show("MISS", _missColor);

        private void Show(string text, Color color)
        {
            if (_label == null) return;
            _label.text = text;
            _label.color = color;
            _timer = _showDuration + _fadeDuration;
            SetAlpha(1f);
            transform.localScale = _originalScale * (1f + _punchScale);
        }

        private void SetAlpha(float a)
        {
            if (_label == null) return;
            Color c = _label.color;
            c.a = a;
            _label.color = c;
        }
    }
}
