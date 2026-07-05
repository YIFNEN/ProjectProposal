using AO.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AO.UI
{
    /// <summary>
    /// Visual oxygen gauge. The fill image is resized from bottom to top so
    /// oxygen changes are easy to read in the world-space HUD.
    /// </summary>
    public class OxygenBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image _fillImage;
        [SerializeField] private bool _resizeFillRectToRatio = false;

        [Header("Colors")]
        [SerializeField] private Color _normalColor = new Color(0.25f, 0.88f, 0.88f, 1f);
        [SerializeField] private Color _criticalColor = new Color(1f, 0.33f, 0.47f, 1f);

        [Header("Critical Pulse")]
        [SerializeField, Range(0.5f, 6f)] private float _criticalPulseHz = 2f;
        [SerializeField, Range(0f, 1f)] private float _criticalPulseAmount = 0.4f;

        [Header("Range")]
        [SerializeField] private float _maxOxygen = 100f;

        [Header("Smoothing")]
        [SerializeField] private bool _smoothChanges = true;
        [SerializeField, Range(1f, 40f)] private float _smoothSpeed = 12f;

        private bool _isCritical;
        private RectTransform _fillRect;
        private float _currentRatio = 1f;
        private float _targetRatio = 1f;

        public float CurrentRatio => _currentRatio;

        private void Reset()
        {
            _fillImage = GetComponent<Image>();
        }

        private void Awake()
        {
            CacheFillRect();
        }

        private void OnEnable()
        {
            CacheFillRect();
            EventBus.OxygenChanged += HandleOxygenChanged;
            EventBus.OxygenCritical += HandleCritical;
            EventBus.OxygenRecovered += HandleRecovered;
            EventBus.SongStarted += HandleSongStarted;

            if (_fillImage != null) _fillImage.color = _normalColor;
            SetOxygenRatio(1f, true);
        }

        private void OnDisable()
        {
            EventBus.OxygenChanged -= HandleOxygenChanged;
            EventBus.OxygenCritical -= HandleCritical;
            EventBus.OxygenRecovered -= HandleRecovered;
            EventBus.SongStarted -= HandleSongStarted;
        }

        private void Update()
        {
            if (_smoothChanges && !Mathf.Approximately(_currentRatio, _targetRatio))
            {
                float t = 1f - Mathf.Exp(-_smoothSpeed * Time.unscaledDeltaTime);
                _currentRatio = Mathf.Lerp(_currentRatio, _targetRatio, t);
                if (Mathf.Abs(_currentRatio - _targetRatio) < 0.001f) _currentRatio = _targetRatio;
                ApplyOxygenRatio(_currentRatio);
            }

            if (_isCritical && _fillImage != null)
            {
                float pulse = (Mathf.Sin(Time.unscaledTime * _criticalPulseHz * Mathf.PI * 2f) + 1f) * 0.5f;
                _fillImage.color = Color.Lerp(_criticalColor, _normalColor, pulse * _criticalPulseAmount);
            }
        }

        private void HandleOxygenChanged(float oxygen)
        {
            SetOxygenRatio(oxygen / _maxOxygen);
        }

        private void HandleCritical() => _isCritical = true;

        private void HandleRecovered()
        {
            _isCritical = false;
            if (_fillImage != null) _fillImage.color = _normalColor;
        }

        private void HandleSongStarted(double _)
        {
            SetOxygenRatio(1f, true);
        }

        private void CacheFillRect()
        {
            if (_fillImage != null) _fillRect = _fillImage.rectTransform;
        }

        private void SetOxygenRatio(float ratio, bool immediate = false)
        {
            ratio = Mathf.Clamp01(ratio);
            _targetRatio = ratio;

            if (!immediate && _smoothChanges && Application.isPlaying) return;

            _currentRatio = ratio;
            ApplyOxygenRatio(ratio);
        }

        private void ApplyOxygenRatio(float ratio)
        {
            if (_fillImage == null) return;

            if (_fillImage.type != Image.Type.Filled) _fillImage.type = Image.Type.Filled;
            _fillImage.fillMethod = Image.FillMethod.Vertical;
            _fillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
            _fillImage.fillAmount = ratio;
            if (_fillRect == null) CacheFillRect();
            if (_fillRect == null) return;
            if (!_resizeFillRectToRatio) return;

            // Filled images should stay at the authored frame size. Resizing the rect
            // itself makes the tall oxygen sprite look squashed as oxygen changes.
            if (_fillImage.type == Image.Type.Filled) return;

            _fillRect.anchorMin = Vector2.zero;
            _fillRect.anchorMax = new Vector2(1f, ratio);
            _fillRect.offsetMin = Vector2.zero;
            _fillRect.offsetMax = Vector2.zero;
        }
    }
}
