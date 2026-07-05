using UnityEngine;
using UnityEngine.XR;

namespace AO.Cameras
{
    /// <summary>
    /// Drives a raw HMD reference and a render camera from the same XR pose,
    /// then applies a render-only local rotation offset to the camera.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public sealed class RenderCameraTiltRig : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _renderCamera;
        [SerializeField] private Transform _rawHmdReference;

        [Header("Raw Pose")]
        [SerializeField] private XRNode _xrNode = XRNode.CenterEye;
        [SerializeField] private bool _applyPosition = true;
        [SerializeField] private bool _applyRotation = true;
        [SerializeField] private bool _driveRawHmdReference = true;
        [SerializeField] private bool _driveRenderCamera = true;

        [Header("Render-Only Rotation")]
        [SerializeField] private bool _enableRenderRotationOffset = true;
        [SerializeField] private Vector3 _renderRotationOffsetEuler = new Vector3(4.5f, 0f, 0f);

        [Header("Render-Only Position")]
        [SerializeField] private bool _enableRenderPositionOffset = true;
        [SerializeField] private Vector3 _renderPositionOffsetLocal = new Vector3(0f, 0.12f, 0f);

        [Header("Timing")]
        [SerializeField] private bool _applyBeforeRender = true;
        [SerializeField] private bool _disableTrackedPoseDriversOnRenderCamera = true;

        private InputDevice _device;
        private bool _hasCachedRawPose;
        private Vector3 _cachedRawLocalPosition;
        private Quaternion _cachedRawLocalRotation = Quaternion.identity;

        public Transform RawHmdReference => _rawHmdReference;
        public Vector3 RenderRotationOffsetEuler => _renderRotationOffsetEuler;
        public Vector3 RenderPositionOffsetLocal => _renderPositionOffsetLocal;

        private void Reset()
        {
            _renderCamera = transform;
            ResolveRawHmdReference();
        }

        private void OnEnable()
        {
            ResolveReferences();
            DisableTrackedPoseDrivers();
            _device = default;

            if (_applyBeforeRender)
            {
                Application.onBeforeRender -= ApplyPose;
                Application.onBeforeRender += ApplyPose;
            }
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= ApplyPose;
        }

        private void LateUpdate()
        {
            ApplyPose();
        }

        [ContextMenu("Apply Pose Now")]
        public void ApplyPose()
        {
            ResolveReferences();
            if (!TryReadRawPose(out Vector3 rawLocalPosition, out Quaternion rawLocalRotation)) return;

            if (_driveRawHmdReference && _rawHmdReference != null)
            {
                ApplyLocalPose(_rawHmdReference, rawLocalPosition, rawLocalRotation);
            }

            if (_driveRenderCamera && _renderCamera != null)
            {
                Vector3 renderPosition = rawLocalPosition + RenderPositionOffset();
                Quaternion renderRotation = rawLocalRotation * RenderRotationOffset();
                ApplyLocalPose(_renderCamera, renderPosition, renderRotation);
            }
        }

        private void ResolveReferences()
        {
            if (_renderCamera == null) _renderCamera = transform;
            ResolveRawHmdReference();
        }

        private void ResolveRawHmdReference()
        {
            if (_rawHmdReference != null) return;
            Transform cameraTransform = _renderCamera != null ? _renderCamera : transform;
            Transform parent = cameraTransform != null ? cameraTransform.parent : null;
            if (parent == null) return;

            Transform raw = parent.Find("RawHmdReference");
            if (raw != null) _rawHmdReference = raw;
        }

        private bool TryReadRawPose(out Vector3 localPosition, out Quaternion localRotation)
        {
            localPosition = CurrentRawLocalPosition();
            localRotation = CurrentRawLocalRotation();

            if (!_device.isValid) _device = InputDevices.GetDeviceAtXRNode(_xrNode);
            if (_device.isValid)
            {
                bool hasPosition = !_applyPosition || _device.TryGetFeatureValue(CommonUsages.devicePosition, out localPosition);
                bool hasRotation = !_applyRotation || _device.TryGetFeatureValue(CommonUsages.deviceRotation, out localRotation);

                if (hasPosition && hasRotation)
                {
                    CacheRawPose(localPosition, localRotation);
                    return true;
                }
            }

            if (_hasCachedRawPose)
            {
                localPosition = _cachedRawLocalPosition;
                localRotation = _cachedRawLocalRotation;
                return true;
            }

            if (_rawHmdReference != null)
            {
                localPosition = _rawHmdReference.localPosition;
                localRotation = _rawHmdReference.localRotation;
                CacheRawPose(localPosition, localRotation);
                return true;
            }

            if (_renderCamera != null)
            {
                localPosition = _renderCamera.localPosition - RenderPositionOffset();
                localRotation = _renderCamera.localRotation * Quaternion.Inverse(RenderRotationOffset());
                CacheRawPose(localPosition, localRotation);
                return true;
            }

            localPosition = default;
            localRotation = Quaternion.identity;
            return false;
        }

        private void ApplyLocalPose(Transform target, Vector3 localPosition, Quaternion localRotation)
        {
            if (_applyPosition) target.localPosition = localPosition;
            if (_applyRotation) target.localRotation = localRotation;
        }

        private Vector3 CurrentRawLocalPosition()
        {
            if (_rawHmdReference != null) return _rawHmdReference.localPosition;
            if (_renderCamera != null) return _renderCamera.localPosition - RenderPositionOffset();
            return _cachedRawLocalPosition;
        }

        private Quaternion CurrentRawLocalRotation()
        {
            if (_rawHmdReference != null) return _rawHmdReference.localRotation;
            if (_renderCamera != null) return _renderCamera.localRotation * Quaternion.Inverse(RenderRotationOffset());
            return _cachedRawLocalRotation;
        }

        private void CacheRawPose(Vector3 localPosition, Quaternion localRotation)
        {
            _cachedRawLocalPosition = localPosition;
            _cachedRawLocalRotation = localRotation;
            _hasCachedRawPose = true;
        }

        private Quaternion RenderRotationOffset()
        {
            return _enableRenderRotationOffset
                ? Quaternion.Euler(_renderRotationOffsetEuler)
                : Quaternion.identity;
        }

        private Vector3 RenderPositionOffset()
        {
            return _enableRenderPositionOffset
                ? _renderPositionOffsetLocal
                : Vector3.zero;
        }

        private void DisableTrackedPoseDrivers()
        {
            if (!_disableTrackedPoseDriversOnRenderCamera || _renderCamera == null) return;

            foreach (Behaviour behaviour in _renderCamera.GetComponents<Behaviour>())
            {
                if (behaviour == null || behaviour == this) continue;
                string typeName = behaviour.GetType().FullName;
                if (typeName == "UnityEngine.InputSystem.XR.TrackedPoseDriver" ||
                    typeName == "UnityEngine.SpatialTracking.TrackedPoseDriver")
                {
                    behaviour.enabled = false;
                }
            }
        }
    }
}
