using AO.Core;
using AO.Judgement;
using AO.State;
using AO.VFX;
using UnityEngine;

namespace AO.Notes
{
    [RequireComponent(typeof(Collider))]
    public class BubbleNote : NoteObject
    {
        [Header("VFX")]
        [SerializeField] private GameObject _perfectVfxPrefab;
        [SerializeField] private GameObject _goodVfxPrefab;
        [SerializeField] private GameObject _feverVfxPrefab;
        [SerializeField] private GameObject _hitVfxPrefab;
        [SerializeField] private GameObject _missVfxPrefab;

        [Header("Timing Anchor")]
        [SerializeField, Tooltip("When enabled, the bubble's leading edge along its travel direction reaches the lane hit point at HitTime instead of the sphere center.")]
        private bool _alignLeadingEdgeToHitTime = true;

        [Header("Rolling Visual")]
        [SerializeField] private bool _rollVisual = true;
        [SerializeField, Min(0f)] private float _rollDegreesPerSecond = 120f;

        private static readonly Collider[] HandOverlapBuffer = new Collider[16];

        private Collider _noteCollider;
        private Vector3 _pathAnchorOffsetFromRoot;
        private Vector3 _rollAxisWorld = Vector3.right;
        private float _rollAngle;

        protected override void OnSpawned()
        {
            _noteCollider = GetComponent<Collider>();
            if (_noteCollider != null) _noteCollider.enabled = true;

            Renderer rend = GetComponentInChildren<Renderer>();
            if (rend != null) rend.enabled = true;

            transform.rotation = Quaternion.identity;
            _pathAnchorOffsetFromRoot = ComputePathAnchorOffsetFromRoot();
            _rollAxisWorld = ComputeRollAxisWorld();
            _rollAngle = 0f;
            SetPathPosition(_spawnPos);
        }

        protected override void UpdateNote(double songTime, float t)
        {
            UpdateRollingVisual();

            if (FeverSystem.Instance != null && FeverSystem.Instance.ShouldAutoPerfect && songTime >= _hitTime)
            {
                Resolve(JudgementResult.Perfect, 0d);
                return;
            }

            TryResolveOverlappingHand();
            if (_resolved) return;

            float goodWindow = _judgementConfig != null ? _judgementConfig.GoodWindow : 0.1f;
            if (songTime > _hitTime + goodWindow)
            {
                Resolve(JudgementResult.Miss, songTime - _hitTime);
            }
        }

        protected override void SetPathPosition(Vector3 pathPosition)
        {
            transform.position = pathPosition - _pathAnchorOffsetFromRoot;
        }

        private Vector3 ComputePathAnchorOffsetFromRoot()
        {
            if (!_alignLeadingEdgeToHitTime) return Vector3.zero;
            if (_noteCollider == null) _noteCollider = GetComponent<Collider>();
            if (_noteCollider == null) return Vector3.zero;

            Vector3 travelDirection = TravelDirection();
            Vector3 centerOffset = _noteCollider.bounds.center - transform.position;

            if (_noteCollider is SphereCollider sphere)
            {
                float radius = sphere.radius * MaxAbs(transform.lossyScale);
                return centerOffset + travelDirection * radius;
            }

            Bounds bounds = _noteCollider.bounds;
            Vector3 absDirection = new Vector3(
                Mathf.Abs(travelDirection.x),
                Mathf.Abs(travelDirection.y),
                Mathf.Abs(travelDirection.z));
            float leadingExtent =
                bounds.extents.x * absDirection.x +
                bounds.extents.y * absDirection.y +
                bounds.extents.z * absDirection.z;

            return centerOffset + travelDirection * leadingExtent;
        }

        private Vector3 ComputeRollAxisWorld()
        {
            Vector3 axis = Vector3.Cross(Vector3.up, TravelDirection());
            if (axis.sqrMagnitude <= 0.0001f) axis = Vector3.right;
            return axis.normalized;
        }

        private Vector3 TravelDirection()
        {
            Vector3 travel = _hitPos - _spawnPos;
            return travel.sqrMagnitude > 0.0001f ? travel.normalized : Vector3.forward;
        }

        private static float MaxAbs(Vector3 value)
        {
            return Mathf.Max(Mathf.Abs(value.x), Mathf.Max(Mathf.Abs(value.y), Mathf.Abs(value.z)));
        }

        private void UpdateRollingVisual()
        {
            if (!_rollVisual || _rollDegreesPerSecond <= 0f) return;

            _rollAngle = Mathf.Repeat(_rollAngle + _rollDegreesPerSecond * Time.deltaTime, 360f);
            transform.rotation = Quaternion.AngleAxis(_rollAngle, _rollAxisWorld);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryResolveTouch(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryResolveTouch(other);
        }

        private void TryResolveOverlappingHand()
        {
            if (_resolved || GameplayRuntimeState.IsInputBlocked) return;

            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                GetOverlapRadius(),
                HandOverlapBuffer,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider hit = HandOverlapBuffer[i];
                if (!hit.CompareTag("Hand")) continue;
                TryResolveTouch(hit);
                if (_resolved) return;
            }
        }

        private void TryResolveTouch(Collider other)
        {
            if (_resolved) return;
            if (GameplayRuntimeState.IsInputBlocked) return;
            if (!other.CompareTag("Hand")) return;

            IHandSource source = FindHandSource(other);

            double songTime = _engine != null ? _engine.SongTime : 0;
            double delta = songTime - _hitTime;
            float absDelta = Mathf.Abs((float)delta);
            float perfectW = _judgementConfig != null ? _judgementConfig.PerfectWindow : 0.05f;
            float goodW = _judgementConfig != null ? _judgementConfig.GoodWindow : 0.1f;

            JudgementResult result;
            bool autoPerfect = FeverSystem.Instance != null && FeverSystem.Instance.ShouldAutoPerfect && songTime >= _hitTime;
            if (autoPerfect) result = JudgementResult.Perfect;
            else if (delta < -goodW) return;
            else if (absDelta <= perfectW) result = JudgementResult.Perfect;
            else if (absDelta <= goodW) result = JudgementResult.Good;
            else result = JudgementResult.Miss;

            if (source != null && result != JudgementResult.Miss)
            {
                float amp = result == JudgementResult.Perfect
                    ? _judgementConfig.HapticPerfectAmplitude
                    : _judgementConfig.HapticGoodAmplitude;
                float dur = result == JudgementResult.Perfect
                    ? _judgementConfig.HapticPerfectDuration
                    : _judgementConfig.HapticGoodDuration;
                if (amp > 0f && dur > 0f) source.PlayHaptic(amp, dur);
            }

            Resolve(result, delta);
        }

        private float GetOverlapRadius()
        {
            if (_noteCollider == null) _noteCollider = GetComponent<Collider>();
            if (_noteCollider == null) return 0.25f;

            Vector3 extents = _noteCollider.bounds.extents;
            float noteRadius = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
            return Mathf.Max(0.05f, noteRadius + 0.14f);
        }

        private void Resolve(JudgementResult result, double delta)
        {
            _resolved = true;
            bool isFeverHit = IsFeverHit(result);

            EventBus.RaiseNoteJudged(new NoteJudgedEvent
            {
                Result = result,
                Lane = (int)_data.Lane,
                TimingDelta = delta,
                IsFeverHit = isFeverHit,
            });

            GameObject vfxPrefab = SelectVfxPrefab(result, isFeverHit);
            if (vfxPrefab != null)
            {
                SpawnVfx(vfxPrefab);
            }

            Despawn();
        }

        private static IHandSource FindHandSource(Collider other)
        {
            return other.GetComponent<IHandSource>() ?? other.GetComponentInParent<IHandSource>();
        }

        private static bool IsFeverHit(JudgementResult result)
        {
            return result != JudgementResult.Miss && FeverSystem.Instance != null && FeverSystem.Instance.IsActive;
        }

        private GameObject SelectVfxPrefab(JudgementResult result, bool isFeverHit)
        {
            if (isFeverHit)
            {
                return _feverVfxPrefab != null ? _feverVfxPrefab : _perfectVfxPrefab;
            }

            switch (result)
            {
                case JudgementResult.Perfect:
                    return _perfectVfxPrefab != null ? _perfectVfxPrefab : _hitVfxPrefab;
                case JudgementResult.Good:
                    return _goodVfxPrefab != null ? _goodVfxPrefab : _hitVfxPrefab;
                case JudgementResult.Miss:
                    return null;
                default:
                    return null;
            }
        }

        private void SpawnVfx(GameObject vfxPrefab)
        {
            GameObject instance = Instantiate(vfxPrefab, transform.position, Quaternion.identity);
            BubblePopOneShotVfx oneShot = instance.GetComponent<BubblePopOneShotVfx>();
            if (oneShot != null)
            {
                oneShot.PlayAt(transform.position, Quaternion.identity);
                Destroy(instance, oneShot.AutoReleaseAfter + 0.15f);
                return;
            }

            float releaseAfter = 1.4f;
            ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem particle = particles[i];
                ParticleSystem.MainModule main = particle.main;
                releaseAfter = Mathf.Max(
                    releaseAfter,
                    main.duration + main.startDelay.constantMax + main.startLifetime.constantMax);
                particle.Clear(true);
                particle.Play(true);
            }

            Destroy(instance, releaseAfter + 0.15f);
        }
    }
}
