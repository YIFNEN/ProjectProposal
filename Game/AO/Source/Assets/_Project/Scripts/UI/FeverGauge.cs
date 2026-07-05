using AO.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AO.UI
{
    public class FeverGauge : MonoBehaviour
    {
        [SerializeField] private Image _fillImage;
        [SerializeField] private Image _glowImage;
        [SerializeField] private RectTransform _marker;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private RectTransform _leadingSpark;
        [SerializeField] private Image _framePathImage;

        [Header("Colors")]
        [SerializeField] private Color _chargingColor = new Color(1f, 0.82f, 0.48f, 1f);
        [SerializeField] private Color _activeColor = new Color(0.25f, 0.88f, 0.88f, 1f);
        [SerializeField] private bool _transparentOverlay = true;
        [SerializeField, Range(0f, 1f)] private float _overlayAlpha = 0.38f;
        [SerializeField] private bool _showLabel = false;
        [SerializeField, Min(0f)] private float _markerPadding = 10f;

        [Header("Oxygen Frame Outline Mode")]
        [SerializeField] private bool _useRadialOutline = false;
        [SerializeField, Range(0f, 1f)] private float _chargingFillAlpha = 0.52f;
        [SerializeField, Range(0f, 1f)] private float _chargingGlowAlpha = 0.16f;
        [SerializeField, Range(0f, 1f)] private float _activeFillAlpha = 0.95f;
        [SerializeField, Range(0f, 1f)] private float _activeGlowAlpha = 0.58f;
        [SerializeField, Range(0f, 8f)] private float _activePulseHz = 2.4f;
        [SerializeField, Range(0f, 0.2f)] private float _activePulseScale = 0.045f;

        [Header("Leading Spark")]
        [SerializeField] private bool _showLeadingSpark = true;
        [SerializeField, Range(-32f, 32f)] private float _leadingSparkRadiusPadding = 6f;
        [SerializeField, Range(0f, 1f)] private float _chargingSparkAlpha = 0.78f;
        [SerializeField, Range(0f, 1f)] private float _activeSparkAlpha = 1f;
        [SerializeField, Range(1f, 2f)] private float _activeSparkScale = 1.45f;

        [Header("Smoothing")]
        [SerializeField] private bool _smoothChanges = true;
        [SerializeField, Range(1f, 80f)] private float _chargeSmoothSpeed = 12f;
        [SerializeField, Range(1f, 80f)] private float _activeDrainSmoothSpeed = 18f;

        private bool _active;
        private float _targetRatio;
        private float _displayRatio;
        private Vector3 _baseFillScale = Vector3.one;
        private Vector3 _baseGlowScale = Vector3.one;

        private void Awake()
        {
            if (_label == null) _label = GetComponentInChildren<TMP_Text>(true);
            ResolveFramePathImage();
            RuntimeUiFactory.ApplyPreferredFont(_label);
            CacheBaseScales();
        }

        private void Reset()
        {
            _fillImage = GetComponent<Image>();
        }

        private void OnEnable()
        {
            EventBus.FeverGaugeChanged += HandleGaugeChanged;
            EventBus.FeverActivated += HandleActivated;
            EventBus.FeverEnded += HandleEnded;
            EventBus.SongStarted += HandleSongStarted;
            CacheBaseScales();
            SetGauge(0f, true);
        }

        private void OnDisable()
        {
            EventBus.FeverGaugeChanged -= HandleGaugeChanged;
            EventBus.FeverActivated -= HandleActivated;
            EventBus.FeverEnded -= HandleEnded;
            EventBus.SongStarted -= HandleSongStarted;
        }

        private void HandleGaugeChanged(float ratio) => SetGauge(ratio);

        private void HandleActivated()
        {
            _active = true;
            SetGauge(1f);
        }

        private void HandleEnded()
        {
            _active = false;
            SetGauge(0f);
        }

        private void HandleSongStarted(double _)
        {
            _active = false;
            SetGauge(0f, true);
        }

        private void Update()
        {
            bool changed = false;
            if (_smoothChanges && Application.isPlaying && !Mathf.Approximately(_displayRatio, _targetRatio))
            {
                float speed = _targetRatio < _displayRatio ? _activeDrainSmoothSpeed : _chargeSmoothSpeed;
                float t = 1f - Mathf.Exp(-speed * Time.deltaTime);
                _displayRatio = Mathf.Lerp(_displayRatio, _targetRatio, t);
                if (Mathf.Abs(_displayRatio - _targetRatio) < 0.001f) _displayRatio = _targetRatio;
                changed = true;
            }

            if (changed || (_useRadialOutline && _active))
            {
                ApplyGaugeVisuals();
            }
        }

        private void SetGauge(float ratio, bool immediate = false)
        {
            _targetRatio = Mathf.Clamp01(ratio);

            if (!immediate && _smoothChanges && Application.isPlaying) return;

            _displayRatio = _targetRatio;
            ApplyGaugeVisuals();
        }

        private void ApplyGaugeVisuals()
        {
            float ratio = _displayRatio;
            float pulse = _active
                ? (Mathf.Sin(Time.unscaledTime * _activePulseHz * Mathf.PI * 2f) + 1f) * 0.5f
                : 0f;

            if (_fillImage != null)
            {
                Color color = ColorWithAlpha(_active ? _activeColor : _chargingColor, FillAlpha(pulse));
                if (_useRadialOutline)
                {
                    ConfigureRadialOutline(_fillImage);
                    _fillImage.fillAmount = ratio;
                    _fillImage.transform.localScale = _baseFillScale * ActiveScale(pulse);
                }
                else
                {
                    _fillImage.fillAmount = _marker != null ? 0f : ratio;
                    if (_transparentOverlay) color.a = Mathf.Min(color.a, _overlayAlpha);
                }

                _fillImage.color = color;
            }

            if (_glowImage != null)
            {
                ConfigureRadialOutline(_glowImage);
                _glowImage.fillAmount = ratio;
                _glowImage.color = ColorWithAlpha(_active ? _activeColor : _chargingColor, GlowAlpha(pulse));
                _glowImage.transform.localScale = _baseGlowScale * ActiveScale(pulse);
            }

            if (_marker != null && _marker.gameObject.activeSelf == _useRadialOutline)
            {
                _marker.gameObject.SetActive(!_useRadialOutline);
            }

            if (!_useRadialOutline && _marker != null)
            {
                RectTransform rect = transform as RectTransform;
                float height = rect != null ? rect.rect.height : 300f;
                float usableHeight = Mathf.Max(0f, height - _markerPadding * 2f);
                float y = -height * 0.5f + _markerPadding + usableHeight * ratio;
                _marker.anchoredPosition = new Vector2(_marker.anchoredPosition.x, y);

                Image markerImage = _marker.GetComponent<Image>();
                if (markerImage != null)
                {
                    Color color = _active ? _activeColor : _chargingColor;
                    color.a = Mathf.Max(color.a, 0.85f);
                    markerImage.color = color;
                }
            }

            if (_label != null)
            {
                if (_label.gameObject.activeSelf != _showLabel) _label.gameObject.SetActive(_showLabel);
                _label.text = _active ? "FEVER" : $"{Mathf.RoundToInt(ratio * 100f)}%";
            }

            UpdateLeadingSpark(ratio, pulse);
        }

        private void UpdateLeadingSpark(float ratio, float pulse)
        {
            if (_leadingSpark == null) return;

            bool visible = _useRadialOutline && _showLeadingSpark && ratio > 0.001f;
            if (_leadingSpark.gameObject.activeSelf != visible) _leadingSpark.gameObject.SetActive(visible);
            if (!visible) return;

            float sparkRatio = GetLeadingSparkRatio(ratio);
            if (!TryGetRadialOutlineSparkPosition(sparkRatio, out Vector2 position))
            {
                _leadingSpark.gameObject.SetActive(false);
                return;
            }

            float angleRad = RadialFillAngleRadians(sparkRatio);
            _leadingSpark.anchoredPosition = position;
            _leadingSpark.localRotation = Quaternion.Euler(0f, 0f, angleRad * Mathf.Rad2Deg - 90f);

            float scale = _active ? Mathf.Lerp(1f, _activeSparkScale, pulse) : 1f;
            _leadingSpark.localScale = Vector3.one * scale;

            Image sparkImage = _leadingSpark.GetComponent<Image>();
            if (sparkImage != null)
            {
                Color color = _active ? _activeColor : _chargingColor;
                color.a = _active
                    ? Mathf.Lerp(_activeSparkAlpha * 0.7f, _activeSparkAlpha, pulse)
                    : _chargingSparkAlpha;
                sparkImage.color = color;
                sparkImage.raycastTarget = false;
                sparkImage.preserveAspect = true;
            }
        }

        private float GetLeadingSparkRatio(float ratio)
        {
            return Mathf.Clamp01(ratio);
        }

        private bool TryGetRadialOutlineSparkPosition(float ratio, out Vector2 position)
        {
            position = default;

            RectTransform frameRect = _fillImage != null ? _fillImage.rectTransform : _framePathImage != null ? _framePathImage.rectTransform : transform as RectTransform;
            RectTransform parentRect = _leadingSpark != null ? _leadingSpark.parent as RectTransform : null;
            if (frameRect == null || parentRect == null) return false;

            Rect rect = frameRect.rect;
            if (rect.width <= 0f || rect.height <= 0f) return false;

            float angleRad = RadialFillAngleRadians(ratio);
            Vector2 direction = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
            Vector2 framePoint = FindRectEdgePoint(rect, rect.center, direction, _leadingSparkRadiusPadding);
            Vector3 world = frameRect.TransformPoint(framePoint);
            Vector3 local = parentRect.InverseTransformPoint(world);
            position = new Vector2(local.x, local.y);
            return true;
        }

        private static Vector2 FindRectEdgePoint(Rect rect, Vector2 center, Vector2 direction, float padding)
        {
            if (direction.sqrMagnitude < 0.0001f) return center;

            direction.Normalize();

            float distanceX = float.PositiveInfinity;
            if (Mathf.Abs(direction.x) > 0.0001f)
            {
                distanceX = direction.x > 0f
                    ? (rect.xMax - center.x) / direction.x
                    : (rect.xMin - center.x) / direction.x;
            }

            float distanceY = float.PositiveInfinity;
            if (Mathf.Abs(direction.y) > 0.0001f)
            {
                distanceY = direction.y > 0f
                    ? (rect.yMax - center.y) / direction.y
                    : (rect.yMin - center.y) / direction.y;
            }

            float distance = Mathf.Min(distanceX, distanceY);
            if (float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0f) return center;

            return center + direction * Mathf.Max(0f, distance + padding);
        }

        private static float RadialFillAngleRadians(float ratio)
        {
            return (90f - 360f * Mathf.Clamp01(ratio)) * Mathf.Deg2Rad;
        }

        private void ResolveFramePathImage()
        {
            if (_framePathImage != null) return;

            Transform sparkParent = _leadingSpark != null ? _leadingSpark.parent : null;
            Transform frame = sparkParent != null ? sparkParent.Find("Frame_Image") : null;
            if (frame == null && transform.parent != null) frame = transform.parent.Find("OxygenBar/Frame_Image");
            if (frame != null) _framePathImage = frame.GetComponent<Image>();
        }

        private void CacheBaseScales()
        {
            if (_fillImage != null) _baseFillScale = _fillImage.transform.localScale;
            if (_glowImage != null) _baseGlowScale = _glowImage.transform.localScale;
        }

        private float FillAlpha(float pulse)
        {
            return _active
                ? Mathf.Lerp(_activeFillAlpha * 0.82f, _activeFillAlpha, pulse)
                : _chargingFillAlpha;
        }

        private float GlowAlpha(float pulse)
        {
            return _active
                ? Mathf.Lerp(_activeGlowAlpha * 0.65f, _activeGlowAlpha, pulse)
                : _chargingGlowAlpha;
        }

        private float ActiveScale(float pulse)
        {
            return _active ? 1f + pulse * _activePulseScale : 1f;
        }

        private static Color ColorWithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static void ConfigureRadialOutline(Image image)
        {
            if (image == null) return;

            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.raycastTarget = false;
            image.preserveAspect = false;
        }
    }
}
