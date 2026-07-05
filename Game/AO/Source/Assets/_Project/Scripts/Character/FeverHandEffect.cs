using AO.Core;
using UnityEngine;

namespace AO.Character
{
    [DisallowMultipleComponent]
    public class FeverHandEffect : MonoBehaviour
    {
        [SerializeField] private DVariantRiderRig _riderRig;
        [SerializeField] private GameObject _effectPrefab;
        [SerializeField] private bool _spawnHandEffectOnFever;
        [SerializeField, Min(0.05f)] private float _spawnInterval = 0.22f;
        [SerializeField, Min(0.1f)] private float _effectLifetime = 1.8f;
        [SerializeField] private Vector3 _localOffset = Vector3.zero;

        [Header("Trail Follow")]
        [SerializeField] private bool _followVisualHandTargets = true;
        [SerializeField] private bool _usePersistentTrails = true;
        [SerializeField, Range(1f, 40f)] private float _followSmoothSpeed = 18f;
        [SerializeField] private bool _parentTrailToHand;

        private bool _active;
        private float _nextSpawnTime;
        private GameObject _leftTrail;
        private GameObject _rightTrail;

        private void Awake()
        {
            ResolveRiderRig();
        }

        private void OnEnable()
        {
            EventBus.FeverActivated += HandleFeverActivated;
            EventBus.FeverEnded += HandleFeverEnded;
            EventBus.SongStarted += HandleSongStarted;
            EventBus.GameOver += HandleGameOver;
        }

        private void OnDisable()
        {
            EventBus.FeverActivated -= HandleFeverActivated;
            EventBus.FeverEnded -= HandleFeverEnded;
            EventBus.SongStarted -= HandleSongStarted;
            EventBus.GameOver -= HandleGameOver;
            StopTrails();
        }

        private void Update()
        {
            if (!_spawnHandEffectOnFever || !_active || _effectPrefab == null) return;
            ResolveRiderRig();

            if (_usePersistentTrails)
            {
                UpdateTrail(ref _leftTrail, LeftHandTarget());
                UpdateTrail(ref _rightTrail, RightHandTarget());
                return;
            }

            if (Time.time < _nextSpawnTime) return;

            SpawnAt(LeftHandTarget());
            SpawnAt(RightHandTarget());
            _nextSpawnTime = Time.time + _spawnInterval;
        }

        private void HandleFeverActivated()
        {
            if (!_spawnHandEffectOnFever)
            {
                StopTrails();
                return;
            }

            _active = true;
            _nextSpawnTime = 0f;
        }

        private void HandleFeverEnded() => StopTrails();
        private void HandleSongStarted(double _) => StopTrails();
        private void HandleGameOver() => StopTrails();

        private void SpawnAt(Transform target)
        {
            if (target == null || _effectPrefab == null) return;

            Vector3 position = target.TransformPoint(_localOffset);
            GameObject fx = Instantiate(_effectPrefab, position, target.rotation);
            Destroy(fx, _effectLifetime);
        }

        private void UpdateTrail(ref GameObject trail, Transform target)
        {
            if (target == null || _effectPrefab == null) return;

            Vector3 targetPosition = target.TransformPoint(_localOffset);
            Quaternion targetRotation = target.rotation;

            if (trail == null)
            {
                trail = Instantiate(_effectPrefab, targetPosition, targetRotation);
                if (_parentTrailToHand)
                {
                    trail.transform.SetParent(target, false);
                    trail.transform.localPosition = _localOffset;
                    trail.transform.localRotation = Quaternion.identity;
                }
            }

            if (_parentTrailToHand) return;

            float t = 1f - Mathf.Exp(-_followSmoothSpeed * Time.unscaledDeltaTime);
            trail.transform.position = Vector3.Lerp(trail.transform.position, targetPosition, t);
            trail.transform.rotation = Quaternion.Slerp(trail.transform.rotation, targetRotation, t);
            RestartFinishedParticles(trail);
        }

        private void StopTrails()
        {
            _active = false;
            StopTrail(ref _leftTrail);
            StopTrail(ref _rightTrail);
        }

        private void StopTrail(ref GameObject trail)
        {
            if (trail == null) return;

            ParticleSystem[] systems = trail.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem system in systems)
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            Destroy(trail, _effectLifetime);
            trail = null;
        }

        private static void RestartFinishedParticles(GameObject trail)
        {
            if (trail == null) return;

            ParticleSystem[] systems = trail.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem system in systems)
            {
                if (!system.isPlaying) system.Play(true);
            }
        }

        private void ResolveRiderRig()
        {
            if (_riderRig == null) _riderRig = GetComponent<DVariantRiderRig>();
            if (_riderRig == null) _riderRig = FindFirstObjectByType<DVariantRiderRig>(FindObjectsInactive.Include);
        }

        private Transform LeftHandTarget()
        {
            ResolveRiderRig();
            if (_followVisualHandTargets && _riderRig != null && _riderRig.LeftVisualHandTarget != null) return _riderRig.LeftVisualHandTarget;
            if (_riderRig != null && _riderRig.LeftHandTarget != null) return _riderRig.LeftHandTarget;
            return null;
        }

        private Transform RightHandTarget()
        {
            ResolveRiderRig();
            if (_followVisualHandTargets && _riderRig != null && _riderRig.RightVisualHandTarget != null) return _riderRig.RightVisualHandTarget;
            if (_riderRig != null && _riderRig.RightHandTarget != null) return _riderRig.RightHandTarget;
            return null;
        }
    }
}
