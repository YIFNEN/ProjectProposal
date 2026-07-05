using UnityEngine;
using UnityEngine.UI;

namespace AO.UI
{
    public class OxygenGaugeVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image _waterBodyImage;
        [SerializeField] private RectTransform _fillMask;
        [SerializeField] private RectTransform _waveHighlight;
        [SerializeField] private ParticleSystem _bubbleParticles;

        [Header("Wave Motion")]
        [SerializeField, Range(0f, 12f)] private float _waveXAmplitude = 3f;
        [SerializeField, Range(0f, 5f)] private float _waveRotationAmplitude = 1f;
        [SerializeField, Range(0f, 0.08f)] private float _waveScaleAmplitude = 0.02f;
        [SerializeField, Range(0.1f, 4f)] private float _waveMoveHz = 1.2f;
        [SerializeField, Range(0.1f, 4f)] private float _waveRotateHz = 1.6f;
        [SerializeField, Range(0f, 24f)] private float _waveVerticalInset = 2f;

        [Header("Bubble Motion")]
        [SerializeField, Range(0f, 1f)] private float _minimumBubbleFill = 0.04f;

        private Vector2 _waveBasePosition;
        private bool _cachedWaveBase;

        private void OnEnable()
        {
            CacheWaveBase();
            ConfigureBubbleParticles();
            UpdateVisuals();
        }

        private void LateUpdate()
        {
            UpdateVisuals();
        }

        private void CacheWaveBase()
        {
            if (_waveHighlight == null || _cachedWaveBase) return;
            _waveBasePosition = _waveHighlight.anchoredPosition;
            _cachedWaveBase = true;
        }

        private void UpdateVisuals()
        {
            float ratio = _waterBodyImage != null ? Mathf.Clamp01(_waterBodyImage.fillAmount) : 1f;
            UpdateWave(ratio);
            UpdateBubbles(ratio);
        }

        private void UpdateWave(float ratio)
        {
            if (_waveHighlight == null || _fillMask == null) return;

            CacheWaveBase();
            Rect rect = _fillMask.rect;
            float bottom = -rect.height * 0.5f;
            float top = rect.height * 0.5f;
            float y = Mathf.Lerp(bottom, top, ratio);
            y = Mathf.Clamp(y, bottom + _waveVerticalInset, top - _waveVerticalInset);

            float time = Time.unscaledTime;
            float x = _waveBasePosition.x + Mathf.Sin(time * _waveMoveHz * Mathf.PI * 2f) * _waveXAmplitude;
            _waveHighlight.anchoredPosition = new Vector2(x, y);
            _waveHighlight.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * _waveRotateHz * Mathf.PI * 2f) * _waveRotationAmplitude);

            float scaleX = 1f + (Mathf.Sin(time * _waveMoveHz * Mathf.PI * 2f + 0.7f) + 1f) * 0.5f * _waveScaleAmplitude;
            _waveHighlight.localScale = new Vector3(scaleX, 1f, 1f);
            _waveHighlight.gameObject.SetActive(ratio > 0.01f);
        }

        private void UpdateBubbles(float ratio)
        {
            if (_bubbleParticles == null) return;

            bool shouldEmit = ratio > _minimumBubbleFill;
            ParticleSystem.EmissionModule emission = _bubbleParticles.emission;
            emission.enabled = shouldEmit;

            if (!shouldEmit)
            {
                _bubbleParticles.Clear();
                return;
            }

            if (!_bubbleParticles.isPlaying) _bubbleParticles.Play();

            if (_fillMask != null)
            {
                Rect rect = _fillMask.rect;
                ParticleSystem.ShapeModule shape = _bubbleParticles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(Mathf.Max(8f, rect.width * 0.42f), Mathf.Max(8f, rect.height * ratio * 0.58f), 0.01f);

                float bottom = -rect.height * 0.5f;
                float centerY = bottom + rect.height * ratio * 0.42f;
                shape.position = new Vector3(0f, centerY, 0f);
                _bubbleParticles.transform.localPosition = Vector3.zero;
            }
        }

        private void ConfigureBubbleParticles()
        {
            if (_bubbleParticles == null) return;

            ParticleSystem.MainModule main = _bubbleParticles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.prewarm = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0f);
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 12f);
            main.maxParticles = 24;

            ParticleSystem.EmissionModule emission = _bubbleParticles.emission;
            emission.SetBursts(new ParticleSystem.Burst[0]);

            ParticleSystem.VelocityOverLifetimeModule velocity = _bubbleParticles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-2f, 2f);
            velocity.y = new ParticleSystem.MinMaxCurve(10f, 20f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }
    }
}
