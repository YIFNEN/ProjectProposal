using AO.Core;
using UnityEngine;

namespace AO.Character
{
    [DisallowMultipleComponent]
    public class MantaRideMotion : MonoBehaviour
    {
        [SerializeField] private Transform _motionRoot;
        [SerializeField] private bool _playOnAwake = true;

        [Header("Normal Ride")]
        [SerializeField] private float _bobAmplitude = 0.035f;
        [SerializeField] private float _swayAmplitude = 0.018f;
        [SerializeField] private float _pitchAmplitude = 2.2f;
        [SerializeField] private float _rollAmplitude = 3.5f;
        [SerializeField] private float _cycleSpeed = 0.62f;

        [Header("Fever Ride")]
        [SerializeField] private float _feverAmplitudeMultiplier = 1.75f;
        [SerializeField] private float _feverSpeedMultiplier = 1.65f;
        [SerializeField, Range(1f, 30f)] private float _feverBlendSpeed = 8f;

        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;
        private bool _active;
        private bool _fever;
        private float _feverBlend;

        private void Reset()
        {
            _motionRoot = transform;
        }

        private void Awake()
        {
            if (_motionRoot == null) _motionRoot = transform;
            CaptureBasePose();
            _active = _playOnAwake;
        }

        private void OnEnable()
        {
            EventBus.FeverActivated += HandleFeverActivated;
            EventBus.FeverEnded += HandleFeverEnded;
            EventBus.SongStarted += HandleSongStarted;
            EventBus.GameOver += HandleStop;
        }

        private void OnDisable()
        {
            EventBus.FeverActivated -= HandleFeverActivated;
            EventBus.FeverEnded -= HandleFeverEnded;
            EventBus.SongStarted -= HandleSongStarted;
            EventBus.GameOver -= HandleStop;
        }

        private void Update()
        {
            if (!_active || _motionRoot == null) return;

            float targetBlend = _fever ? 1f : 0f;
            _feverBlend = Mathf.MoveTowards(_feverBlend, targetBlend, _feverBlendSpeed * Time.deltaTime);

            float amplitude = Mathf.Lerp(1f, _feverAmplitudeMultiplier, _feverBlend);
            float speed = Mathf.Lerp(1f, _feverSpeedMultiplier, _feverBlend);
            float phase = Time.time * Mathf.Max(0.01f, _cycleSpeed) * speed * Mathf.PI * 2f;

            Vector3 offset = new Vector3(
                Mathf.Sin(phase * 0.71f) * _swayAmplitude * amplitude,
                Mathf.Sin(phase) * _bobAmplitude * amplitude,
                0f);

            Quaternion rotation = Quaternion.Euler(
                Mathf.Sin(phase + 0.8f) * _pitchAmplitude * amplitude,
                0f,
                Mathf.Sin(phase * 0.83f) * _rollAmplitude * amplitude);

            _motionRoot.localPosition = _baseLocalPosition + offset;
            _motionRoot.localRotation = _baseLocalRotation * rotation;
        }

        [ContextMenu("Capture Base Pose")]
        public void CaptureBasePose()
        {
            if (_motionRoot == null) _motionRoot = transform;
            _baseLocalPosition = _motionRoot.localPosition;
            _baseLocalRotation = _motionRoot.localRotation;
        }

        private void HandleSongStarted(double _) => _active = true;
        private void HandleFeverActivated() => _fever = true;
        private void HandleFeverEnded() => _fever = false;

        private void HandleStop()
        {
            _fever = false;
            _active = _playOnAwake;
        }
    }
}
