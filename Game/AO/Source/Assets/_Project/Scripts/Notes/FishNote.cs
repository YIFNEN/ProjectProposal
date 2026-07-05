using AO.Core;
using AO.Judgement;
using AO.State;
using UnityEngine;
using UnityEngine.UI;

namespace AO.Notes
{
    [RequireComponent(typeof(Collider))]
    public class FishNote : NoteObject
    {
        [System.Serializable]
        public class FishVisualCandidate
        {
            [Tooltip("Optional id used only for authoring/debugging. The prefab name is fine.")]
            public string Id = "";
            [Tooltip("Fish visual prefab to instantiate under VisualRoot. Pick from Assets/_Project/Prefabs/Environment/Imported/Fish.")]
            public GameObject Prefab;
            [Tooltip("Local offset from the FishNote root after this visual is spawned.")]
            public Vector3 LocalPosition = Vector3.zero;
            [Tooltip("Additional local rotation after the visual prefab's authored root rotation. Keep 0,0,0 when tuning direction in the visual prefab.")]
            public Vector3 LocalEulerAngles = Vector3.zero;
            [Tooltip("Additional scale multiplier after the visual prefab's authored local scale. Keep 1,1,1 to use the Note-folder prefab scale as-is.")]
            public Vector3 LocalScale = Vector3.one;
            [Tooltip("Fallback head/touch point in FishNote root local space. If the spawned visual prefab contains FishNoteHeadAnchor, HeadAnchor, FishHeadAnchor, or TouchAnchor, that visual marker is used instead.")]
            public Vector3 HeadLocalPosition = new Vector3(0f, 0f, 0.25f);
            [Tooltip("Optional per-visual overlap radius. 0 keeps the note collider based radius.")]
            [Min(0f)] public float StrokeOverlapRadius = 0f;
            [Tooltip("Extra padding added to this visual's stroke overlap radius.")]
            [Min(0f)] public float StrokeOverlapPadding = 0.14f;
        }

        [Header("Fish Visual Candidates")]
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private FishVisualCandidate[] _visualCandidates = System.Array.Empty<FishVisualCandidate>();
        [SerializeField] private bool _randomizeVisualOnSpawn = true;

        [Header("Visual Direction")]
        [SerializeField, Tooltip("When enabled, this note rotates so its local +Z/front axis follows the spawn-to-hit travel direction. Author fish visual prefabs with their head facing local +Z.")]
        private bool _alignForwardToTravelDirection = true;

        [Header("Trail / VFX")]
        [SerializeField] private GameObject _successVfxPrefab;
        [SerializeField, Range(0.05f, 1f)] private float _successVfxScale = 0.45f;
        [SerializeField] private Vector3 _successVfxLocalOffset = new Vector3(0f, 0.08f, 0f);
        [SerializeField] private bool _successVfxFollowHeadAnchor = true;
        [SerializeField, Min(0f)] private float _successVfxWorldUpOffset = 0.24f;
        [SerializeField, Min(0.1f)] private float _successVfxLifetime = 1.8f;

        [Header("Approach Hearts")]
        [SerializeField] private bool _emitApproachHearts = true;
        [SerializeField] private GameObject _approachHeartPrefab;
        [SerializeField, Range(0f, 1f)] private float _approachHeartStartT = 0.08f;
        [SerializeField, Range(0f, 1f)] private float _approachHeartEndT = 0.92f;
        [SerializeField, Min(0.05f)] private float _approachHeartInterval = 0.32f;
        [SerializeField, Range(0.02f, 0.5f)] private float _approachHeartMinScale = 0.08f;
        [SerializeField, Range(0.02f, 0.75f)] private float _approachHeartMaxScale = 0.18f;
        [SerializeField] private Vector3 _approachHeartLocalOffset = new Vector3(0f, 0.08f, -0.08f);
        [SerializeField, Min(0f)] private float _approachHeartWorldUpOffset = 0.1f;
        [SerializeField] private Vector3 _approachHeartRandomWorldJitter = new Vector3(0.06f, 0.05f, 0.04f);
        [SerializeField, Min(0.1f)] private float _approachHeartLifetime = 1.1f;

        [Header("Visual Animation")]
        [SerializeField] private bool _playVisualPrefabAnimations = true;

        [Header("Stroke Progress Heart")]
        [SerializeField] private Transform _heartProgressRoot;
        [SerializeField] private Image _heartProgressImage;
        [SerializeField] private SpriteRenderer _heartProgressSprite;
        [SerializeField] private Vector3 _heartProgressLocalOffset = new Vector3(0f, 0.16f, 0f);
        [SerializeField] private bool _heartProgressFollowHeadAnchor = true;
        [SerializeField, Min(0f)] private float _heartProgressWorldUpOffset = 0.32f;
        [SerializeField] private Color _heartProgressColor = new Color(1f, 0.08f, 0.12f, 0.9f);
        [SerializeField, Range(0.05f, 1f)] private float _heartProgressMinScale = 0.18f;
        [SerializeField, Range(0.1f, 1.5f)] private float _heartProgressMaxScale = 0.42f;
        [SerializeField] private bool _heartProgressBillboard = true;
        [SerializeField] private bool _resetProgressWhenContactLost = true;

        [Header("Motion")]
        [SerializeField] private float _passThroughDistance = 1.5f;
        [SerializeField, Range(0f, 1f)] private float _sustainSwimFraction = 0.4f;
        [SerializeField] private float _defaultSustainSeconds = 0.5f;
        [SerializeField, Min(0.1f)] private float _exitSeconds = 0.75f;
        [SerializeField, Min(0.05f)] private float _successExitSeconds = 0.32f;
        [SerializeField] private bool _exitAfterStrokeSuccess = true;
        [SerializeField, Range(0.05f, 0.5f)] private float _successHoldSeconds = 0.12f;

        [Header("Stroke Timing Anchor")]
        [SerializeField, Tooltip("Preferred prefab-authored head/touch anchor. If present, this point reaches HitTime.")]
        private Transform _headAnchor;
        [SerializeField, Tooltip("When enabled, the leading edge/head side reaches the hit position at HitTime instead of the prefab origin.")]
        private bool _alignLeadingEdgeToHitTime = true;
        [SerializeField, Range(0f, 1.2f), Tooltip("0 = prefab origin, 1 = collider leading edge along the swim direction.")]
        private float _leadingEdgeFactor = 1f;
        [SerializeField, Tooltip("Extra world-space offset from the collider leading edge. Positive values move the timing anchor farther toward the head.")]
        private float _leadingEdgePadding = 0f;
        [SerializeField, Min(0f), Tooltip("Small grace added when a beatmap fish duration is too short to satisfy the required stroke time comfortably.")]
        private float _minimumWindowGraceSeconds = 0.15f;

        [Header("Stroke Overlap Scaling")]
        [SerializeField, Tooltip("Scales per-visual StrokeOverlapRadius/Padding by the active visual prefab scale, so larger fish have a larger head stroke area.")]
        private bool _scaleStrokeOverlapWithVisualScale = true;
        [SerializeField, Min(0.01f), Tooltip("Keeps very small fish forgiving even if their imported prefab root is tiny.")]
        private float _minimumStrokeOverlapScale = 1f;

        [Header("Haptic Throttle")]
        [SerializeField, Range(0.05f, 0.5f)] private float _strokingHapticInterval = 0.15f;

        private static readonly Collider[] HandOverlapBuffer = new Collider[16];
        private static readonly string[] VisualHeadAnchorNames =
        {
            "FishNoteHeadAnchor",
            "HeadAnchor",
            "FishHeadAnchor",
            "TouchAnchor"
        };

        private Collider _noteCollider;
        private float _strokeAccumSeconds;
        private bool _strokeFailed;
        private bool _strokeSucceeded;
        private float _requiredStrokeSeconds;
        private float _strokeWindowSeconds;
        private IHandSource _activeHandSource;
        private float _nextStrokingHapticTime;
        private int _lastStrokeProcessFrame = -1;
        private Vector3 _travelDirection = Vector3.forward;
        private Vector3 _pathAnchorOffsetFromRoot;
        private Vector3 _heartProgressBaseScale = Vector3.one;
        private double _successSongTime = -1d;
        private FishVisualCandidate _activeVisualCandidate;
        private GameObject _activeVisualInstance;
        private Transform _runtimeHeadAnchor;
        private float _nextApproachHeartTime;

        protected override void OnSpawned()
        {
            _strokeAccumSeconds = 0f;
            _strokeFailed = false;
            _strokeSucceeded = false;
            _activeHandSource = null;
            _nextStrokingHapticTime = 0f;
            _lastStrokeProcessFrame = -1;
            _strokeWindowSeconds = 0f;
            _successSongTime = -1d;
            _nextApproachHeartTime = 0f;
            CacheVisualReferences();
            _runtimeHeadAnchor = _headAnchor;
            _travelDirection = (_hitPos - _spawnPos).sqrMagnitude > 0.0001f
                ? (_hitPos - _spawnPos).normalized
                : transform.forward;
            AlignForwardToTravelDirection();
            ApplyRandomVisualCandidate();
            CacheHeartProgressReferences();
            HideHeartProgress();

            float duration = GetBeatmapStrokeDurationSeconds();
            float minRequired = _judgementConfig != null ? _judgementConfig.FishStrokeMinDuration : 0.8f;
            _requiredStrokeSeconds = Mathf.Max(duration * 0.7f, minRequired);
            _strokeWindowSeconds = Mathf.Max(0.1f, duration, _requiredStrokeSeconds + _minimumWindowGraceSeconds);

            _noteCollider = GetComponent<Collider>();
            if (_noteCollider != null) _noteCollider.enabled = true;
            _pathAnchorOffsetFromRoot = ComputePathAnchorOffsetFromRoot();
            SetPathPosition(_spawnPos);

            SetVisualInstanceRenderersEnabled(true);
        }

        private void AlignForwardToTravelDirection()
        {
            if (!_alignForwardToTravelDirection) return;
            if (_travelDirection.sqrMagnitude <= 0.0001f) return;

            Vector3 up = Mathf.Abs(Vector3.Dot(_travelDirection.normalized, Vector3.up)) > 0.98f
                ? Vector3.forward
                : Vector3.up;
            transform.rotation = Quaternion.LookRotation(_travelDirection, up);
        }

        protected override void UpdateNote(double songTime, float t)
        {
            UpdateApproachHearts(t);

            if (t <= 1f) return;

            double sustainElapsed = songTime - _hitTime;
            double sustainDuration = GetEffectiveStrokeWindowSeconds();
            Vector3 dir = (_hitPos - _spawnPos).normalized;

            if (sustainElapsed < sustainDuration)
            {
                if (FeverSystem.Instance != null && FeverSystem.Instance.ShouldAutoPerfect && !_strokeSucceeded)
                {
                    CompleteStroke();
                }

                float swim = (float)(sustainElapsed / sustainDuration);
                SetPathPosition(_hitPos + dir * _passThroughDistance * _sustainSwimFraction * swim);
                TryStrokeOverlappingHand();
                ResetProgressIfContactWasLost();
            }
            else
            {
                HideHeartProgress();
                double postSustain = sustainElapsed - sustainDuration;
                float exitDuration = _strokeSucceeded ? _successExitSeconds : _exitSeconds;
                float exitProgress = (float)(postSustain / Mathf.Max(0.05f, exitDuration));
                Vector3 start = _hitPos + dir * _passThroughDistance * _sustainSwimFraction;
                Vector3 end = _hitPos + dir * _passThroughDistance;
                SetPathPosition(Vector3.LerpUnclamped(start, end, exitProgress));

                if (exitProgress >= 1f) Despawn();
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (!IsStrokeWindowOpen()) return;
            ProcessStroke(other);
        }

        private void TryStrokeOverlappingHand()
        {
            if (_resolved || _strokeFailed || GameplayRuntimeState.IsInputBlocked) return;
            if (!IsStrokeWindowOpen()) return;

            Vector3 center = GetStrokeOverlapCenter();
            int count = Physics.OverlapSphereNonAlloc(
                center,
                GetOverlapRadius(),
                HandOverlapBuffer,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider hit = HandOverlapBuffer[i];
                if (!hit.CompareTag("Hand")) continue;
                ProcessStroke(hit);
                return;
            }
        }

        private void ProcessStroke(Collider other)
        {
            if (_resolved || _strokeFailed) return;
            if (GameplayRuntimeState.IsInputBlocked) return;
            if (!IsStrokeWindowOpen()) return;
            if (!other.CompareTag("Hand")) return;
            if (!IsHandWithinStrokeOverlap(other)) return;
            if (_lastStrokeProcessFrame == Time.frameCount) return;
            _lastStrokeProcessFrame = Time.frameCount;

            IHandSource source = FindHandSource(other);
            _activeHandSource = source;

            if (!_strokeSucceeded)
            {
                float speed = source != null ? source.Velocity.magnitude : EstimateHandSpeedFallback(other);
                float maxSpeed = _judgementConfig != null ? _judgementConfig.FishStrokeMaxSpeed : 1.5f;

                if (speed > maxSpeed)
                {
                    _strokeFailed = true;
                    _strokeAccumSeconds = 0f;
                    HideHeartProgress();
                    EventBus.RaiseFishStrokeFailed();
                    PlayHaptic(_judgementConfig?.HapticFishFailAmplitude ?? 0.85f,
                               _judgementConfig?.HapticFishFailDuration ?? 0.08f);
                    return;
                }

                _strokeAccumSeconds += Time.deltaTime;
                UpdateHeartProgress();
            }

            float strokingAmp = _judgementConfig?.HapticFishStrokingAmplitude ?? 0f;
            if (strokingAmp > 0f && Time.time >= _nextStrokingHapticTime)
            {
                PlayHaptic(strokingAmp, _strokingHapticInterval * 0.8f);
                _nextStrokingHapticTime = Time.time + _strokingHapticInterval;
            }

            if (!_strokeSucceeded && _strokeAccumSeconds >= _requiredStrokeSeconds)
            {
                CompleteStroke();
            }
        }

        private void CompleteStroke()
        {
            if (_strokeSucceeded || _strokeFailed) return;

            _strokeSucceeded = true;
            _successSongTime = _engine != null ? _engine.SongTime : _hitTime;
            HideHeartProgress();
            EventBus.RaiseFishStrokeSucceeded();
            PlayHaptic(_judgementConfig?.HapticFishSuccessAmplitude ?? 0.55f,
                       _judgementConfig?.HapticFishSuccessDuration ?? 0.35f);

            if (_successVfxPrefab != null)
            {
                Vector3 worldPosition = GetHeadRelativeWorldPosition(
                    _successVfxLocalOffset,
                    _successVfxWorldUpOffset,
                    _successVfxFollowHeadAnchor);
                GameObject vfx = Instantiate(_successVfxPrefab, worldPosition, Quaternion.identity);
                ScaleVfxInstance(vfx, _successVfxScale);
                PlayParticleSystems(vfx);
                Destroy(vfx, Mathf.Max(0.1f, _successVfxLifetime));
            }
        }

        private void UpdateApproachHearts(float t)
        {
            if (!_emitApproachHearts || _strokeFailed || _resolved) return;
            if (t < _approachHeartStartT || t > Mathf.Min(_approachHeartEndT, 1f)) return;
            if (Time.time < _nextApproachHeartTime) return;

            SpawnApproachHeart(t);
            _nextApproachHeartTime = Time.time + Mathf.Max(0.05f, _approachHeartInterval);
        }

        private void SpawnApproachHeart(float t)
        {
            GameObject prefab = _approachHeartPrefab != null ? _approachHeartPrefab : _successVfxPrefab;
            if (prefab == null) return;

            Vector3 worldPosition = GetHeadRelativeWorldPosition(
                _approachHeartLocalOffset,
                _approachHeartWorldUpOffset,
                useHeadAnchor: true);
            worldPosition += RandomWorldJitter(_approachHeartRandomWorldJitter);

            GameObject heart = Instantiate(prefab, worldPosition, Quaternion.identity);
            float scale = Mathf.Lerp(_approachHeartMinScale, _approachHeartMaxScale, Mathf.Clamp01(t));
            ScaleVfxInstance(heart, scale);
            PlayParticleSystems(heart);
            Destroy(heart, Mathf.Max(0.1f, _approachHeartLifetime));
        }

        private void ResetProgressIfContactWasLost()
        {
            if (!_resetProgressWhenContactLost) return;
            if (_strokeSucceeded || _strokeFailed || _strokeAccumSeconds <= 0f) return;
            if (_lastStrokeProcessFrame == Time.frameCount) return;

            _strokeAccumSeconds = 0f;
            HideHeartProgress();
        }

        private void CacheHeartProgressReferences()
        {
            if (_heartProgressRoot == null)
            {
                Transform child = transform.Find("StrokeHeartProgress");
                if (child != null) _heartProgressRoot = child;
            }

            if (_heartProgressRoot != null)
            {
                if (_heartProgressImage == null) _heartProgressImage = _heartProgressRoot.GetComponentInChildren<Image>(true);
                if (_heartProgressSprite == null) _heartProgressSprite = _heartProgressRoot.GetComponentInChildren<SpriteRenderer>(true);
                _heartProgressBaseScale = _heartProgressRoot.localScale.sqrMagnitude > 0.0001f
                    ? _heartProgressRoot.localScale
                    : Vector3.one;
            }
        }

        private void UpdateHeartProgress()
        {
            float ratio = _requiredStrokeSeconds > 0f ? Mathf.Clamp01(_strokeAccumSeconds / _requiredStrokeSeconds) : 0f;

            if (_heartProgressRoot != null)
            {
                if (!_heartProgressRoot.gameObject.activeSelf) _heartProgressRoot.gameObject.SetActive(true);
                _heartProgressRoot.localPosition = HeartProgressLocalPosition();
                FaceHeartProgressTowardCamera();
                float scale = Mathf.Lerp(_heartProgressMinScale, _heartProgressMaxScale, ratio);
                _heartProgressRoot.localScale = _heartProgressBaseScale * scale;
            }

            if (_heartProgressImage != null)
            {
                _heartProgressImage.type = Image.Type.Filled;
                _heartProgressImage.fillMethod = Image.FillMethod.Radial360;
                _heartProgressImage.fillOrigin = (int)Image.Origin360.Bottom;
                _heartProgressImage.fillClockwise = true;
                _heartProgressImage.fillAmount = ratio;
                _heartProgressImage.color = _heartProgressColor;
            }

            if (_heartProgressSprite != null)
            {
                Color color = _heartProgressColor;
                color.a *= Mathf.Lerp(0.35f, 1f, ratio);
                _heartProgressSprite.color = color;
            }
        }

        private void HideHeartProgress()
        {
            if (_heartProgressRoot != null) _heartProgressRoot.gameObject.SetActive(false);
            if (_heartProgressImage != null) _heartProgressImage.fillAmount = 0f;
            if (_heartProgressSprite != null)
            {
                Color color = _heartProgressSprite.color;
                color.a = 0f;
                _heartProgressSprite.color = color;
            }
        }

        private void PlayHaptic(float amplitude, float duration)
        {
            if (_activeHandSource == null || amplitude <= 0f || duration <= 0f) return;
            _activeHandSource.PlayHaptic(amplitude, duration);
        }

        private float GetOverlapRadius()
        {
            if (_noteCollider == null) _noteCollider = GetComponent<Collider>();
            if (_noteCollider == null) return 0.25f;

            Vector3 extents = _noteCollider.bounds.extents;
            float noteRadius = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
            if (_activeVisualCandidate != null && _activeVisualCandidate.StrokeOverlapRadius > 0f)
            {
                float scale = _scaleStrokeOverlapWithVisualScale ? ActiveVisualScaleMultiplier() : 1f;
                float radius = (_activeVisualCandidate.StrokeOverlapRadius + _activeVisualCandidate.StrokeOverlapPadding) * scale;
                return Mathf.Max(0.05f, radius);
            }

            return Mathf.Max(0.05f, noteRadius + 0.14f);
        }

        private float ActiveVisualScaleMultiplier()
        {
            if (_activeVisualInstance == null) return 1f;

            Vector3 scale = _activeVisualInstance.transform.lossyScale;
            float multiplier = MaxAbs(scale);
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier <= 0f) return 1f;
            return Mathf.Max(Mathf.Max(0.01f, _minimumStrokeOverlapScale), multiplier);
        }

        private Vector3 GetStrokeOverlapCenter()
        {
            Transform anchor = GetActiveHeadAnchor();
            return anchor != null ? anchor.position : transform.position;
        }

        private bool IsHandWithinStrokeOverlap(Collider other)
        {
            if (other == null) return false;

            Vector3 center = GetStrokeOverlapCenter();
            float radius = GetOverlapRadius();
            Vector3 closest = other.ClosestPoint(center);
            return (closest - center).sqrMagnitude <= radius * radius;
        }

        protected override void SetPathPosition(Vector3 pathPosition)
        {
            transform.position = pathPosition - _pathAnchorOffsetFromRoot;
        }

        private Vector3 ComputePathAnchorOffsetFromRoot()
        {
            if (!_alignLeadingEdgeToHitTime) return Vector3.zero;

            Transform anchor = GetActiveHeadAnchor();
            if (anchor != null)
            {
                return anchor.position - transform.position + _travelDirection * _leadingEdgePadding;
            }

            if (_noteCollider == null) return Vector3.zero;

            Bounds bounds = _noteCollider.bounds;
            Vector3 absDir = new Vector3(
                Mathf.Abs(_travelDirection.x),
                Mathf.Abs(_travelDirection.y),
                Mathf.Abs(_travelDirection.z));

            float leadingExtent =
                bounds.extents.x * absDir.x +
                bounds.extents.y * absDir.y +
                bounds.extents.z * absDir.z;

            Vector3 centerOffset = bounds.center - transform.position;
            float headDistance = Mathf.Max(0f, leadingExtent * _leadingEdgeFactor + _leadingEdgePadding);
            return centerOffset + _travelDirection * headDistance;
        }

        private void CacheVisualReferences()
        {
            if (_visualRoot == null)
            {
                Transform visual = transform.Find("VisualRoot");
                if (visual != null) _visualRoot = visual;
            }

            if (_headAnchor == null)
            {
                Transform head = transform.Find("HeadAnchor");
                if (head == null) head = transform.Find("FishHeadAnchor");
                if (head != null) _headAnchor = head;
            }

            if (_runtimeHeadAnchor == null) _runtimeHeadAnchor = _headAnchor;
        }

        private void ApplyRandomVisualCandidate()
        {
            _activeVisualCandidate = SelectVisualCandidate();
            if (_activeVisualCandidate == null || _activeVisualCandidate.Prefab == null) return;

            Transform parent = _visualRoot != null ? _visualRoot : transform;
            if (_activeVisualInstance != null)
            {
                Destroy(_activeVisualInstance);
                _activeVisualInstance = null;
            }

            _activeVisualInstance = Instantiate(_activeVisualCandidate.Prefab, parent, false);
            _activeVisualInstance.name = string.IsNullOrWhiteSpace(_activeVisualCandidate.Id)
                ? _activeVisualCandidate.Prefab.name
                : _activeVisualCandidate.Id;
            Quaternion authoredLocalRotation = _activeVisualInstance.transform.localRotation;
            _activeVisualInstance.transform.localPosition = _activeVisualCandidate.LocalPosition;
            _activeVisualInstance.transform.localRotation = authoredLocalRotation * Quaternion.Euler(_activeVisualCandidate.LocalEulerAngles);
            Vector3 authoredLocalScale = _activeVisualInstance.transform.localScale;
            _activeVisualInstance.transform.localScale = Vector3.Scale(authoredLocalScale, _activeVisualCandidate.LocalScale);

            Transform visualHeadAnchor = FindVisualHeadAnchor(_activeVisualInstance.transform);
            if (visualHeadAnchor != null)
            {
                _runtimeHeadAnchor = visualHeadAnchor;
                if (_headAnchor != null)
                {
                    _headAnchor.position = visualHeadAnchor.position;
                }
            }
            else if (_headAnchor != null)
            {
                _headAnchor.localPosition = _activeVisualCandidate.HeadLocalPosition;
                _runtimeHeadAnchor = _headAnchor;
            }

            PlayActiveVisualAnimations();
        }

        private void SetVisualInstanceRenderersEnabled(bool enabled)
        {
            if (_activeVisualInstance == null) return;

            Renderer[] renderers = _activeVisualInstance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = enabled;
            }
        }

        private static Transform FindVisualHeadAnchor(Transform visualRoot)
        {
            if (visualRoot == null) return null;

            Transform[] transforms = visualRoot.GetComponentsInChildren<Transform>(true);
            for (int nameIndex = 0; nameIndex < VisualHeadAnchorNames.Length; nameIndex++)
            {
                string anchorName = VisualHeadAnchorNames[nameIndex];
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform candidate = transforms[i];
                    if (candidate == visualRoot) continue;
                    if (string.Equals(candidate.name, anchorName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private void PlayActiveVisualAnimations()
        {
            if (!_playVisualPrefabAnimations || _activeVisualInstance == null) return;

            Animator[] animators = _activeVisualInstance.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null || animator.runtimeAnimatorController == null) continue;

                animator.enabled = true;
                if (Mathf.Approximately(animator.speed, 0f)) animator.speed = 1f;
                animator.Rebind();
                animator.Update(0f);
            }

            Animation[] legacyAnimations = _activeVisualInstance.GetComponentsInChildren<Animation>(true);
            for (int i = 0; i < legacyAnimations.Length; i++)
            {
                PlayLegacyAnimation(legacyAnimations[i]);
            }

            PlayParticleSystems(_activeVisualInstance);
        }

        private static void PlayLegacyAnimation(Animation legacyAnimation)
        {
            if (legacyAnimation == null) return;

            legacyAnimation.enabled = true;
            string clipName = legacyAnimation.clip != null ? legacyAnimation.clip.name : null;

            if (string.IsNullOrEmpty(clipName))
            {
                foreach (AnimationState state in legacyAnimation)
                {
                    if (state == null) continue;
                    state.wrapMode = WrapMode.Loop;
                    clipName = state.name;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(clipName))
            {
                AnimationState state = legacyAnimation[clipName];
                if (state != null) state.wrapMode = WrapMode.Loop;
                legacyAnimation.Play(clipName);
            }
        }

        private static void PlayParticleSystems(GameObject root)
        {
            if (root == null) return;

            ParticleSystem[] particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem particle = particles[i];
                if (particle == null) continue;
                particle.Play(true);
            }
        }

        private FishVisualCandidate SelectVisualCandidate()
        {
            if (_visualCandidates == null || _visualCandidates.Length == 0) return null;

            if (TrySelectPreferredVisualCandidate(out FishVisualCandidate preferred))
            {
                return preferred;
            }

            int start = _randomizeVisualOnSpawn && _visualCandidates.Length > 1
                ? Random.Range(0, _visualCandidates.Length)
                : 0;

            for (int i = 0; i < _visualCandidates.Length; i++)
            {
                FishVisualCandidate candidate = _visualCandidates[(start + i) % _visualCandidates.Length];
                if (candidate != null && candidate.Prefab != null) return candidate;
            }

            return null;
        }

        private bool TrySelectPreferredVisualCandidate(out FishVisualCandidate candidate)
        {
            candidate = null;

            string key = PreferredVisualKey(_data != null ? _data.Variant : null);
            if (string.IsNullOrEmpty(key))
            {
                key = PreferredVisualKey(gameObject.name);
            }

            if (string.IsNullOrEmpty(key)) return false;

            for (int i = 0; i < _visualCandidates.Length; i++)
            {
                FishVisualCandidate item = _visualCandidates[i];
                if (item == null || item.Prefab == null) continue;

                if (MatchesVisualKey(item.Id, key) || MatchesVisualKey(item.Prefab.name, key))
                {
                    candidate = item;
                    return true;
                }
            }

            return false;
        }

        private static string PreferredVisualKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";

            string lower = value.ToLowerInvariant();
            if (lower.Contains("pink")) return "pink";
            if (lower.Contains("cyan")) return "cyan";
            if (lower.Contains("blue")) return "blue";
            if (lower.Contains("red")) return "red";
            if (lower.Contains("violet")) return "violet";
            if (lower.Contains("black") || lower.Contains("white")) return "black_white";
            if (lower.Contains("dolphin")) return "dolphin";
            if (lower.Contains("whale")) return "whale";

            return "";
        }

        private static bool MatchesVisualKey(string value, string key)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrEmpty(key)) return false;

            string lower = value.ToLowerInvariant();
            if (key == "black_white")
            {
                return lower.Contains("black_white") || (lower.Contains("black") && lower.Contains("white"));
            }

            return lower.Contains(key);
        }

        private Vector3 HeartProgressLocalPosition()
        {
            Vector3 worldPosition = GetHeadRelativeWorldPosition(
                _heartProgressLocalOffset,
                _heartProgressWorldUpOffset,
                _heartProgressFollowHeadAnchor);
            return transform.InverseTransformPoint(worldPosition);
        }

        private Vector3 GetHeadRelativeWorldPosition(Vector3 localOffset, float worldUpOffset, bool useHeadAnchor)
        {
            Transform anchor = useHeadAnchor ? GetActiveHeadAnchor() : null;
            Vector3 worldPosition = anchor != null ? anchor.position : transform.TransformPoint(localOffset);

            if (anchor != null)
            {
                worldPosition += transform.TransformDirection(localOffset);
            }

            if (worldUpOffset > 0f)
            {
                worldPosition += Vector3.up * worldUpOffset;
            }

            return worldPosition;
        }

        private static Vector3 RandomWorldJitter(Vector3 maxAbs)
        {
            return new Vector3(
                Random.Range(-Mathf.Abs(maxAbs.x), Mathf.Abs(maxAbs.x)),
                Random.Range(-Mathf.Abs(maxAbs.y), Mathf.Abs(maxAbs.y)),
                Random.Range(-Mathf.Abs(maxAbs.z), Mathf.Abs(maxAbs.z)));
        }

        private static void ScaleVfxInstance(GameObject instance, float scale)
        {
            if (instance == null) return;
            instance.transform.localScale = Vector3.Scale(instance.transform.localScale, Vector3.one * Mathf.Max(0.01f, scale));
        }

        private static float MaxAbs(Vector3 value)
        {
            return Mathf.Max(Mathf.Abs(value.x), Mathf.Max(Mathf.Abs(value.y), Mathf.Abs(value.z)));
        }

        private Transform GetActiveHeadAnchor()
        {
            return _runtimeHeadAnchor != null ? _runtimeHeadAnchor : _headAnchor;
        }

        private float GetStrokeWindowSeconds()
        {
            return _strokeWindowSeconds > 0f
                ? _strokeWindowSeconds
                : Mathf.Max(0.1f, GetBeatmapStrokeDurationSeconds());
        }

        private double GetEffectiveStrokeWindowSeconds()
        {
            float window = GetStrokeWindowSeconds();
            if (!_exitAfterStrokeSuccess || !_strokeSucceeded || _successSongTime < 0d) return window;

            double successElapsed = Mathf.Max(0f, (float)(_successSongTime - _hitTime));
            return System.Math.Min(window, successElapsed + _successHoldSeconds);
        }

        private float GetBeatmapStrokeDurationSeconds()
        {
            float duration = _data != null && _data.Duration > 0
                ? (float)_data.Duration
                : _defaultSustainSeconds;
            float minRequired = _judgementConfig != null ? _judgementConfig.FishStrokeMinDuration : 0.8f;
            return Mathf.Max(0.1f, duration, minRequired);
        }

        private bool IsStrokeWindowOpen()
        {
            if (_engine == null || _data == null) return false;

            double songTime = _engine.SongTime;
            double windowEnd = _hitTime + GetStrokeWindowSeconds();
            return songTime >= _hitTime && songTime <= windowEnd;
        }

        private void FaceHeartProgressTowardCamera()
        {
            if (!_heartProgressBillboard || _heartProgressRoot == null) return;

            Camera camera = Camera.main;
            if (camera == null) return;

            _heartProgressRoot.rotation = Quaternion.LookRotation(camera.transform.forward, camera.transform.up);
        }

        private static float EstimateHandSpeedFallback(Collider hand)
        {
            Rigidbody rb = hand.attachedRigidbody;
            if (rb != null)
            {
#if UNITY_2023_3_OR_NEWER
                return rb.linearVelocity.magnitude;
#else
                return rb.velocity.magnitude;
#endif
            }

            return 0f;
        }

        private static IHandSource FindHandSource(Collider other)
        {
            return other.GetComponent<IHandSource>() ?? other.GetComponentInParent<IHandSource>();
        }
    }
}
