using System.Text;
using AO.Core;
using UnityEngine;

namespace AO.Character
{
    [DisallowMultipleComponent]
    public class CharacterTransformProbe : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _mantaRoot;
        [SerializeField] private Transform _seatAnchor;
        [SerializeField] private Transform _characterRoot;
        [SerializeField] private Transform _visualRig;
        [SerializeField] private Transform _xrOrigin;
        [SerializeField] private Transform _cameraOffset;
        [SerializeField] private Camera _mainCamera;

        [Header("Logging")]
        [SerializeField] private bool _autoBind = true;
        [SerializeField] private bool _logOnAwake = true;
        [SerializeField] private bool _logOnStart = true;
        [SerializeField] private bool _logFirstLateUpdate = true;
        [SerializeField] private bool _logOnSongStarted = true;
        [SerializeField] private bool _logWhenChangedAfterAwake = true;
        [SerializeField, Min(1)] private int _watchFramesAfterAwake = 120;
        [SerializeField, Min(0f)] private float _positionChangeThreshold = 0.01f;
        [SerializeField, Min(0f)] private float _rotationChangeThresholdDegrees = 0.5f;
        [SerializeField] private bool _logCameraSnapshot = true;
        [SerializeField] private bool _logRendererDiagnostics = true;
        [SerializeField, Min(1)] private int _maxRendererLogs = 16;
        [SerializeField] private bool _warnWhenRendererOutsideFrustum = true;

        private PoseSnapshot _mantaAwake;
        private PoseSnapshot _seatAwake;
        private PoseSnapshot _characterAwake;
        private PoseSnapshot _visualAwake;
        private int _awakeFrame;
        private bool _loggedFirstLateUpdate;
        private bool _loggedChange;

        private void Reset()
        {
            AutoBind();
        }

        private void Awake()
        {
            if (_autoBind) AutoBind();
            _awakeFrame = Time.frameCount;
            CaptureAwakeSnapshots();
            if (_logOnAwake) LogSnapshot("Awake");
        }

        private void OnEnable()
        {
            EventBus.SongStarted += HandleSongStarted;
        }

        private void Start()
        {
            if (_autoBind) AutoBind();
            if (_logOnStart) LogSnapshot("Start");
        }

        private void LateUpdate()
        {
            if (_autoBind) AutoBind();

            if (_logFirstLateUpdate && !_loggedFirstLateUpdate)
            {
                _loggedFirstLateUpdate = true;
                LogSnapshot("First LateUpdate");
            }

            if (!_logWhenChangedAfterAwake || _loggedChange) return;
            if (Time.frameCount - _awakeFrame > _watchFramesAfterAwake) return;

            if (ChangedFromAwake())
            {
                _loggedChange = true;
                LogSnapshot("Changed After Awake");
            }
        }

        private void OnDisable()
        {
            EventBus.SongStarted -= HandleSongStarted;
        }

        [ContextMenu("Auto Bind")]
        public void AutoBind()
        {
            if (_mantaRoot == null)
            {
                DVariantRiderRig riderRig = GetComponent<DVariantRiderRig>() ?? FindFirstObjectByType<DVariantRiderRig>(FindObjectsInactive.Include);
                if (riderRig != null) _mantaRoot = riderRig.transform;
            }

            if (_mantaRoot == null)
            {
                GameObject manta = GameObject.Find("MantaRoot");
                if (manta != null) _mantaRoot = manta.transform;
            }

            if (_seatAnchor == null && _mantaRoot != null) _seatAnchor = _mantaRoot.Find("SeatAnchor");
            if (_characterRoot == null && _seatAnchor != null) _characterRoot = _seatAnchor.Find("CharacterRoot");
            if (_visualRig == null && _characterRoot != null) _visualRig = _characterRoot.Find("VisualRig");

            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_cameraOffset == null && _mainCamera != null) _cameraOffset = _mainCamera.transform.parent;
            if (_xrOrigin == null && _cameraOffset != null) _xrOrigin = _cameraOffset.parent;
        }

        [ContextMenu("Log Current Snapshot")]
        public void LogCurrentSnapshot()
        {
            if (_autoBind) AutoBind();
            LogSnapshot("Manual");
        }

        [ContextMenu("Log Renderer Diagnostics")]
        public void LogRendererDiagnostics()
        {
            if (_autoBind) AutoBind();
            StringBuilder builder = new StringBuilder(2048);
            builder.Append("[CharacterTransformProbe] Renderer Diagnostics\n");
            AppendCameraInfo(builder);
            AppendRendererDiagnostics(builder);
            Debug.Log(builder.ToString(), this);
        }

        [ContextMenu("Apply Play Mode Skinned Renderer Culling Test")]
        public void ApplyPlayModeSkinnedRendererCullingTest()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[CharacterTransformProbe] This culling test only runs in Play Mode so scene/prefab renderer settings are not saved accidentally.", this);
                return;
            }

            if (_autoBind) AutoBind();
            if (_visualRig == null)
            {
                Debug.LogWarning("[CharacterTransformProbe] VisualRig is missing. Cannot apply SkinnedMeshRenderer culling test.", this);
                return;
            }

            int skinnedCount = 0;
            foreach (SkinnedMeshRenderer renderer in _visualRig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skinnedCount++;
                renderer.updateWhenOffscreen = true;
                renderer.allowOcclusionWhenDynamic = false;

                Bounds localBounds = renderer.localBounds;
                Vector3 size = localBounds.size;
                size.x = Mathf.Max(size.x, 3f);
                size.y = Mathf.Max(size.y, 3f);
                size.z = Mathf.Max(size.z, 3f);
                Vector3 center = localBounds.center;
                center.y = Mathf.Max(center.y, 1f);
                renderer.localBounds = new Bounds(center, size);
            }

            foreach (Renderer renderer in _visualRig.GetComponentsInChildren<Renderer>(true))
            {
                renderer.allowOcclusionWhenDynamic = false;
            }

            Debug.Log($"[CharacterTransformProbe] Applied Play Mode skinned renderer culling test to {skinnedCount} SkinnedMeshRenderer(s). This is runtime-only and reverts when Play Mode stops.", this);
            LogRendererDiagnostics();
        }

        private void HandleSongStarted(double dspStartTime)
        {
            if (!_logOnSongStarted) return;
            LogSnapshot($"SongStarted dsp={dspStartTime:0.000}");
        }

        private void CaptureAwakeSnapshots()
        {
            _mantaAwake = PoseSnapshot.Capture(_mantaRoot);
            _seatAwake = PoseSnapshot.Capture(_seatAnchor);
            _characterAwake = PoseSnapshot.Capture(_characterRoot);
            _visualAwake = PoseSnapshot.Capture(_visualRig);
        }

        private bool ChangedFromAwake()
        {
            return PoseSnapshot.Changed(_mantaRoot, _mantaAwake, _positionChangeThreshold, _rotationChangeThresholdDegrees)
                || PoseSnapshot.Changed(_seatAnchor, _seatAwake, _positionChangeThreshold, _rotationChangeThresholdDegrees)
                || PoseSnapshot.Changed(_characterRoot, _characterAwake, _positionChangeThreshold, _rotationChangeThresholdDegrees)
                || PoseSnapshot.Changed(_visualRig, _visualAwake, _positionChangeThreshold, _rotationChangeThresholdDegrees);
        }

        private void LogSnapshot(string label)
        {
            StringBuilder builder = new StringBuilder(512);
            builder.Append("[CharacterTransformProbe] ").Append(label).Append('\n');
            AppendTransform(builder, "MantaRoot", _mantaRoot);
            AppendTransform(builder, "SeatAnchor", _seatAnchor);
            AppendTransform(builder, "CharacterRoot", _characterRoot);
            AppendTransform(builder, "VisualRig", _visualRig);
            if (_logCameraSnapshot)
            {
                AppendTransform(builder, "XROrigin", _xrOrigin);
                AppendTransform(builder, "CameraOffset", _cameraOffset);
                AppendTransform(builder, "MainCamera", _mainCamera != null ? _mainCamera.transform : null);
                AppendCameraInfo(builder);
            }

            if (_logRendererDiagnostics) AppendRendererDiagnostics(builder);
            Debug.Log(builder.ToString(), this);
        }

        private void AppendCameraInfo(StringBuilder builder)
        {
            if (_mainCamera == null)
            {
                builder.Append("Camera: <missing>").Append('\n');
                return;
            }

            builder
                .Append("Camera: near=").Append(_mainCamera.nearClipPlane.ToString("0.###"))
                .Append(", far=").Append(_mainCamera.farClipPlane.ToString("0.###"))
                .Append(", fov=").Append(_mainCamera.fieldOfView.ToString("0.###"))
                .Append(", occlusionCulling=").Append(_mainCamera.useOcclusionCulling)
                .Append(", cullingMask=0x").Append(_mainCamera.cullingMask.ToString("X"))
                .Append('\n');

            AppendCameraRelation(builder, "CharacterRoot", _characterRoot);
            AppendCameraRelation(builder, "VisualRig", _visualRig);
        }

        private void AppendCameraRelation(StringBuilder builder, string label, Transform target)
        {
            if (_mainCamera == null || target == null) return;

            Vector3 cameraLocal = _mainCamera.transform.InverseTransformPoint(target.position);
            float distance = Vector3.Distance(_mainCamera.transform.position, target.position);
            builder
                .Append(label)
                .Append(" vs Camera: cameraLocal=").Append(Format(cameraLocal))
                .Append(", distance=").Append(distance.ToString("0.###"))
                .Append(", signedDepth=").Append(cameraLocal.z.ToString("0.###"))
                .Append(", near=").Append(_mainCamera.nearClipPlane.ToString("0.###"))
                .Append('\n');
        }

        private void AppendRendererDiagnostics(StringBuilder builder)
        {
            if (_visualRig == null)
            {
                builder.Append("Renderers: VisualRig <missing>").Append('\n');
                return;
            }

            Renderer[] renderers = _visualRig.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                builder.Append("Renderers: none under VisualRig").Append('\n');
                return;
            }

            Plane[] frustumPlanes = _mainCamera != null ? GeometryUtility.CalculateFrustumPlanes(_mainCamera) : null;
            int skinnedCount = 0;
            int visibleCount = 0;
            int frustumCount = 0;
            int loggedCount = 0;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                if (renderer is SkinnedMeshRenderer) skinnedCount++;
                if (renderer.isVisible) visibleCount++;
                bool inFrustum = frustumPlanes == null || GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds);
                if (inFrustum) frustumCount++;
            }

            builder
                .Append("Renderers: total=").Append(renderers.Length)
                .Append(", skinned=").Append(skinnedCount)
                .Append(", isVisible=").Append(visibleCount)
                .Append(", inMainCameraFrustum=").Append(frustumPlanes != null ? frustumCount.ToString() : "unknown")
                .Append('\n');

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                if (loggedCount >= _maxRendererLogs)
                {
                    builder.Append("  ... ").Append(renderers.Length - loggedCount).Append(" more renderer(s) omitted").Append('\n');
                    break;
                }

                loggedCount++;
                bool inFrustum = frustumPlanes == null || GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds);
                Vector3 boundsCameraLocal = _mainCamera != null
                    ? _mainCamera.transform.InverseTransformPoint(renderer.bounds.center)
                    : Vector3.zero;

                builder
                    .Append("  [").Append(loggedCount).Append("] ")
                    .Append(GetPath(renderer.transform))
                    .Append(" type=").Append(renderer.GetType().Name)
                    .Append(", enabled=").Append(renderer.enabled)
                    .Append(", active=").Append(renderer.gameObject.activeInHierarchy)
                    .Append(", isVisible=").Append(renderer.isVisible)
                    .Append(", inFrustum=").Append(inFrustum)
                    .Append(", dynamicOccludee=").Append(renderer.allowOcclusionWhenDynamic)
                    .Append(", boundsCenter=").Append(Format(renderer.bounds.center))
                    .Append(", boundsSize=").Append(Format(renderer.bounds.size));

                if (_mainCamera != null)
                {
                    builder
                        .Append(", boundsCameraLocal=").Append(Format(boundsCameraLocal))
                        .Append(", boundsDepth=").Append(boundsCameraLocal.z.ToString("0.###"));
                }

                builder.Append('\n');

                if (renderer is SkinnedMeshRenderer skinned)
                {
                    builder
                        .Append("      skinned: updateWhenOffscreen=").Append(skinned.updateWhenOffscreen)
                        .Append(", localBoundsCenter=").Append(Format(skinned.localBounds.center))
                        .Append(", localBoundsSize=").Append(Format(skinned.localBounds.size))
                        .Append(", rootBone=").Append(skinned.rootBone != null ? GetPath(skinned.rootBone) : "<missing>");

                    if (skinned.rootBone != null)
                    {
                        Vector3 rootBoneCameraLocal = _mainCamera != null
                            ? _mainCamera.transform.InverseTransformPoint(skinned.rootBone.position)
                            : Vector3.zero;
                        builder
                            .Append(", rootBoneWorld=").Append(Format(skinned.rootBone.position));
                        if (_mainCamera != null) builder.Append(", rootBoneCameraLocal=").Append(Format(rootBoneCameraLocal));
                    }

                    builder.Append('\n');
                }

                if (_warnWhenRendererOutsideFrustum && frustumPlanes != null && !inFrustum)
                {
                    builder.Append("      WARNING: renderer bounds are outside Main Camera frustum. If this is the disappearing mesh, bounds/culling is a likely cause.").Append('\n');
                }
            }
        }

        private static void AppendTransform(StringBuilder builder, string label, Transform target)
        {
            if (target == null)
            {
                builder.Append(label).Append(": <missing>").Append('\n');
                return;
            }

            builder
                .Append(label)
                .Append(": localPos=").Append(Format(target.localPosition))
                .Append(", localRot=").Append(Format(target.localEulerAngles))
                .Append(", localScale=").Append(Format(target.localScale))
                .Append(", worldPos=").Append(Format(target.position))
                .Append(", worldRot=").Append(Format(target.eulerAngles))
                .Append(", lossyScale=").Append(Format(target.lossyScale))
                .Append('\n');
        }

        private static string Format(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }

        private static string GetPath(Transform target)
        {
            if (target == null) return "<missing>";

            StringBuilder builder = new StringBuilder(target.name);
            Transform current = target.parent;
            while (current != null)
            {
                builder.Insert(0, current.name + "/");
                current = current.parent;
            }

            return builder.ToString();
        }

        private readonly struct PoseSnapshot
        {
            private readonly bool _valid;
            private readonly Vector3 _localPosition;
            private readonly Quaternion _localRotation;
            private readonly Vector3 _worldPosition;
            private readonly Quaternion _worldRotation;

            private PoseSnapshot(Transform target)
            {
                _valid = target != null;
                _localPosition = target != null ? target.localPosition : default;
                _localRotation = target != null ? target.localRotation : Quaternion.identity;
                _worldPosition = target != null ? target.position : default;
                _worldRotation = target != null ? target.rotation : Quaternion.identity;
            }

            public static PoseSnapshot Capture(Transform target)
            {
                return new PoseSnapshot(target);
            }

            public static bool Changed(Transform target, PoseSnapshot snapshot, float positionThreshold, float rotationThresholdDegrees)
            {
                if (!snapshot._valid || target == null) return false;

                return Vector3.Distance(target.localPosition, snapshot._localPosition) > positionThreshold
                    || Vector3.Distance(target.position, snapshot._worldPosition) > positionThreshold
                    || Quaternion.Angle(target.localRotation, snapshot._localRotation) > rotationThresholdDegrees
                    || Quaternion.Angle(target.rotation, snapshot._worldRotation) > rotationThresholdDegrees;
            }
        }
    }
}
