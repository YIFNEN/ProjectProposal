using UnityEngine;

namespace AO.Cameras
{
    /// <summary>
    /// Optional third-person camera for desktop mirror/recording.
    /// Disabled by default so it does not affect Quest gameplay until intentionally enabled.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpectatorCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private UnityEngine.Camera _camera;
        [SerializeField] private bool _enableCamera;
        [SerializeField] private bool _allowRuntimeCameraCreation = false;
        [SerializeField] private Vector3 _followOffset = new Vector3(0.8f, 0.45f, -1.35f);
        [SerializeField] private Vector3 _lookAtOffset = new Vector3(0f, 0.08f, 0f);
        [SerializeField] private float _followSharpness = 7f;
        [SerializeField] private float _lookSharpness = 10f;

        private void Awake()
        {
            EnsureCamera();
            ResolveTarget();
            ApplyCameraEnabled();
        }

        private void LateUpdate()
        {
            ResolveTarget();
            ApplyCameraEnabled();
            if (_target == null) return;

            Quaternion yaw = Quaternion.Euler(0f, _target.eulerAngles.y, 0f);
            Vector3 desiredPosition = _target.position + yaw * _followOffset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, 1f - Mathf.Exp(-_followSharpness * Time.deltaTime));

            Vector3 lookPoint = _target.position + _lookAtOffset;
            Vector3 direction = lookPoint - transform.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-_lookSharpness * Time.deltaTime));
            }
        }

        private void EnsureCamera()
        {
            if (_camera != null) return;
            _camera = GetComponentInChildren<UnityEngine.Camera>(true);
            if (_camera != null) return;
            if (!_enableCamera) return;
            if (!_allowRuntimeCameraCreation)
            {
                Debug.LogError("[SpectatorCameraRig] Spectator camera is enabled, but no Camera child/reference exists. Runtime camera creation is disabled.", this);
                return;
            }

            GameObject cameraObject = new GameObject("SpectatorCamera");
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<UnityEngine.Camera>();
            _camera.depth = -20f;
            _camera.fieldOfView = 45f;
        }

        private void ResolveTarget()
        {
            if (_target != null) return;

            GameObject rider = GameObject.Find("MantaRoot");
            if (rider == null) rider = GameObject.Find("DVariantRiderRoot");
            if (rider != null)
            {
                _target = rider.transform;
                return;
            }

            GameObject hitAnchor = GameObject.Find("HitAnchor");
            if (hitAnchor != null) _target = hitAnchor.transform;
        }

        private void ApplyCameraEnabled()
        {
            if (_enableCamera) EnsureCamera();
            if (_camera != null) _camera.enabled = _enableCamera;
        }
    }
}
