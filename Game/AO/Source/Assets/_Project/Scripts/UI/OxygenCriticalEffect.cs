using System;
using AO.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AO.UI
{
    [DisallowMultipleComponent]
    public class OxygenCriticalEffect : MonoBehaviour
    {
        [SerializeField] private RectTransform _targetRoot;
        [SerializeField] private Image _pulseImage;
        [SerializeField] private Color _criticalColor = new Color(1f, 0.08f, 0.12f, 0.18f);
        [SerializeField, Range(0.5f, 6f)] private float _pulseHz = 1.6f;
        [SerializeField, Range(0f, 1f)] private float _maxAlpha = 0.72f;
        [SerializeField, Range(0f, 1f)] private float _minAlpha = 0.16f;
        [SerializeField] private bool _preferSceneRect = true;
        [SerializeField] private bool _preferSceneImageSettings = true;
        [SerializeField] private bool _setAsLastSibling = true;
        [SerializeField] private bool _allowRuntimePulseCreation = false;

        [Header("View Coverage")]
        [SerializeField] private bool _forceFullTargetRect = true;
        [SerializeField, Range(1f, 4f)] private float _viewWidthOverscan = 1.6f;
        [SerializeField, Range(1f, 4f)] private float _viewHeightOverscan = 2.25f;
        [SerializeField] private bool _forceStretchedImage = true;

        [Header("Camera Follow")]
        [SerializeField] private bool _followCamera = true;
        [SerializeField] private Transform _cameraTarget;
        [SerializeField, Min(0.1f)] private float _cameraDistance = 1.15f;
        [SerializeField] private Vector2 _cameraOffset = Vector2.zero;

        [Header("Sprite Animation")]
        [SerializeField] private Sprite[] _pulseFrames = Array.Empty<Sprite>();
        [SerializeField, Range(1f, 30f)] private float _frameRate = 18f;
        [SerializeField] private bool _useSpriteNativeColor = true;

        private bool _critical;
        private float _animationStartTime;

        private void Awake()
        {
            EnsurePulseImage();
            EnsureCameraTarget();
            SetVisible(false);
        }

        private void OnEnable()
        {
            EventBus.OxygenCritical += HandleCritical;
            EventBus.OxygenRecovered += HandleRecovered;
            EventBus.SongStarted += HandleSongStarted;
            EnsurePulseImage();
            EnsureCameraTarget();
            SetVisible(_critical);
        }

        private void OnDisable()
        {
            EventBus.OxygenCritical -= HandleCritical;
            EventBus.OxygenRecovered -= HandleRecovered;
            EventBus.SongStarted -= HandleSongStarted;
        }

        private void Update()
        {
            if (!_critical || _pulseImage == null) return;

            UpdateSpriteFrame();

            float pulse = (Mathf.Sin(Time.unscaledTime * _pulseHz * Mathf.PI * 2f) + 1f) * 0.5f;
            Color color = UseSpriteFrames && _useSpriteNativeColor ? Color.white : _criticalColor;
            color.a = Mathf.Lerp(_minAlpha, _maxAlpha, pulse);
            _pulseImage.color = color;
        }

        private void LateUpdate()
        {
            if (_followCamera)
            {
                FollowCamera();
            }
        }

        private void HandleCritical()
        {
            _critical = true;
            _animationStartTime = Time.unscaledTime;
            SetVisible(true);
            UpdateSpriteFrame();
        }

        private void HandleRecovered()
        {
            _critical = false;
            SetVisible(false);
        }

        private void HandleSongStarted(double _)
        {
            _critical = false;
            _animationStartTime = Time.unscaledTime;
            SetVisible(false);
        }

        private void EnsurePulseImage()
        {
            if (_targetRoot == null && _pulseImage != null && _pulseImage.transform.parent is RectTransform pulseParent)
            {
                _targetRoot = pulseParent;
            }

            if (_targetRoot == null) _targetRoot = transform as RectTransform;
            if (_targetRoot == null) return;

            bool existingAuthored = false;
            bool created = false;
            if (_pulseImage == null)
            {
                Transform existing = _targetRoot.Find("OxygenCriticalPulse");
                if (existing != null)
                {
                    _pulseImage = existing.GetComponent<Image>();
                    existingAuthored = _pulseImage != null;
                }
            }
            else
            {
                existingAuthored = true;
            }

            if (_pulseImage == null)
            {
                if (!_allowRuntimePulseCreation)
                {
                    Debug.LogError("[OxygenCriticalEffect] Required child 'OxygenCriticalPulse' with Image is missing. Runtime pulse creation is disabled.", this);
                    return;
                }

                GameObject go = new GameObject("OxygenCriticalPulse", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_targetRoot, false);
                _pulseImage = go.GetComponent<Image>();
                created = true;
            }

            RectTransform rect = _pulseImage.rectTransform;
            if (!_preferSceneRect || !existingAuthored || created)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
            }

            ApplyViewCoverage(rect);

            _pulseImage.raycastTarget = false;
            if (_forceStretchedImage || !_preferSceneImageSettings || !existingAuthored || created)
            {
                _pulseImage.preserveAspect = false;
                _pulseImage.type = Image.Type.Simple;
            }

            _pulseImage.color = UseSpriteFrames && _useSpriteNativeColor ? Color.white : _criticalColor;
            if (UseSpriteFrames && _pulseImage.sprite == null) _pulseImage.sprite = _pulseFrames[0];
            if (_setAsLastSibling) _pulseImage.transform.SetAsLastSibling();
        }

        private void ApplyViewCoverage(RectTransform rect)
        {
            if (!_forceFullTargetRect || rect == null) return;

            RectTransform root = _targetRoot != null ? _targetRoot : rect.parent as RectTransform;
            Vector2 rootSize = root != null ? root.rect.size : rect.rect.size;
            if (rootSize.x <= 0f || rootSize.y <= 0f)
            {
                rootSize = root != null ? root.sizeDelta : rect.sizeDelta;
            }

            float widthScale = Mathf.Max(1f, _viewWidthOverscan);
            float heightScale = Mathf.Max(1f, _viewHeightOverscan);
            float extraX = Mathf.Max(0f, rootSize.x * (widthScale - 1f) * 0.5f);
            float extraY = Mathf.Max(0f, rootSize.y * (heightScale - 1f) * 0.5f);

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-extraX, -extraY);
            rect.offsetMax = new Vector2(extraX, extraY);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private void EnsureCameraTarget()
        {
            if (_cameraTarget == null && Camera.main != null)
            {
                _cameraTarget = Camera.main.transform;
            }
        }

        private void FollowCamera()
        {
            if (_pulseImage == null) return;
            EnsureCameraTarget();
            if (_cameraTarget == null) return;

            Transform pulseTransform = _pulseImage.transform;
            Vector3 position =
                _cameraTarget.position +
                _cameraTarget.forward * _cameraDistance +
                _cameraTarget.right * _cameraOffset.x +
                _cameraTarget.up * _cameraOffset.y;

            pulseTransform.position = position;
            pulseTransform.rotation = Quaternion.LookRotation(_cameraTarget.forward, _cameraTarget.up);
        }

        private void UpdateSpriteFrame()
        {
            if (!UseSpriteFrames || _pulseImage == null) return;

            int frame = Mathf.FloorToInt((Time.unscaledTime - _animationStartTime) * _frameRate);
            frame = Mathf.Abs(frame) % _pulseFrames.Length;
            _pulseImage.sprite = _pulseFrames[frame];
            if (!_preferSceneImageSettings)
            {
                _pulseImage.type = Image.Type.Simple;
                _pulseImage.preserveAspect = false;
            }
        }

        private void SetVisible(bool visible)
        {
            if (_pulseImage == null) return;
            _pulseImage.gameObject.SetActive(visible);
        }

        private bool UseSpriteFrames => _pulseFrames != null && _pulseFrames.Length > 0;
    }
}
