using UnityEngine;

namespace AO.Character
{
    [DisallowMultipleComponent]
    public class TemporaryDVariantAvatar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _hmd;
        [SerializeField] private Transform _leftController;
        [SerializeField] private Transform _rightController;
        [SerializeField] private Transform _hitAnchor;
        [SerializeField] private Transform _leftHandTarget;
        [SerializeField] private Transform _rightHandTarget;

        [Header("Placement")]
        [SerializeField] private float _distanceInFrontOfHmd = 1f;
        [SerializeField] private float _heightOffsetFromHmd = -0.28f;
        [SerializeField] private float _lazyFollowThreshold = 0.35f;
        [SerializeField] private float _lazyFollowSpeed = 3.5f;

        [Header("Hand Mapping")]
        [SerializeField] private float _horizontalScale = 1.35f;
        [SerializeField] private float _verticalScale = 1.1f;
        [SerializeField] private float _depthScale = 0.45f;
        [SerializeField] private bool _driveHandTargetsFromControllers = true;
        [SerializeField] private Vector3 _leftHandRestLocal = new Vector3(-0.24f, -0.05f, -0.05f);
        [SerializeField] private Vector3 _rightHandRestLocal = new Vector3(0.24f, -0.05f, -0.05f);

        [Header("Debug Visuals")]
        [SerializeField] private bool _createPrimitiveBody = true;
        [SerializeField] private bool _showHandTargets = false;
        [SerializeField] private bool _driveHitAnchorToChest = false;
        [SerializeField] private bool _allowRuntimeObjectCreation = false;

        private Transform _body;
        private Transform _head;

        public Transform LeftHandTarget => _leftHandTarget;
        public Transform RightHandTarget => _rightHandTarget;

        private void Awake()
        {
            if (_hmd == null && Camera.main != null) _hmd = Camera.main.transform;
            EnsureTargets();
            EnsureDebugBody();
            SetHandTargetVisibility(_showHandTargets);
            SnapToDesiredPose();
        }

        private void LateUpdate()
        {
            if (_hmd == null) return;

            Vector3 desired = DesiredAvatarPosition();
            float distance = Vector3.Distance(transform.position, desired);
            if (distance > _lazyFollowThreshold)
            {
                transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-_lazyFollowSpeed * Time.deltaTime));
            }

            Vector3 flatForward = FlatForward();
            if (flatForward.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(flatForward, Vector3.up);
            }

            if (_driveHandTargetsFromControllers)
            {
                UpdateHandTarget(_leftController, _leftHandTarget, _leftHandRestLocal);
                UpdateHandTarget(_rightController, _rightHandTarget, _rightHandRestLocal);
            }

            if (_driveHitAnchorToChest && _hitAnchor != null)
            {
                _hitAnchor.position = transform.TransformPoint(new Vector3(0f, 0.02f, -0.04f));
                _hitAnchor.rotation = transform.rotation;
            }
        }

        private void SnapToDesiredPose()
        {
            if (_hmd == null) return;
            transform.position = DesiredAvatarPosition();
            Vector3 flatForward = FlatForward();
            if (flatForward.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(flatForward, Vector3.up);
        }

        private Vector3 DesiredAvatarPosition()
        {
            Vector3 forward = FlatForward();
            return _hmd.position + forward * _distanceInFrontOfHmd + Vector3.up * _heightOffsetFromHmd;
        }

        private Vector3 FlatForward()
        {
            Vector3 forward = _hmd != null ? _hmd.forward : Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private void UpdateHandTarget(Transform controller, Transform target, Vector3 restLocal)
        {
            if (target == null) return;

            if (controller == null || _hmd == null)
            {
                target.localPosition = restLocal;
                return;
            }

            Vector3 controllerLocal = _hmd.InverseTransformPoint(controller.position);
            Vector3 mapped = new Vector3(
                controllerLocal.x * _horizontalScale,
                controllerLocal.y * _verticalScale - 0.15f,
                controllerLocal.z * _depthScale);

            target.position = transform.TransformPoint(mapped);
            target.rotation = controller.rotation;
        }

        private void EnsureTargets()
        {
            _leftHandTarget = FindOrCreateChild(_leftHandTarget, "LeftHandTarget", _leftHandRestLocal);
            _rightHandTarget = FindOrCreateChild(_rightHandTarget, "RightHandTarget", _rightHandRestLocal);
        }

        private Transform FindOrCreateChild(Transform current, string childName, Vector3 localPosition)
        {
            Transform child = current != null ? current : transform.Find(childName);
            bool created = false;
            if (child == null)
            {
                if (!_allowRuntimeObjectCreation)
                {
                    Debug.LogError($"[TemporaryDVariantAvatar] Required child '{childName}' is missing under '{name}'. Runtime object creation is disabled.", this);
                    return null;
                }

                GameObject go = new GameObject(childName);
                child = go.transform;
                child.SetParent(transform, false);
                created = true;
            }
            else if (child.parent != transform)
            {
                child.SetParent(transform, true);
            }

            if (created)
            {
                child.localPosition = localPosition;
                child.localRotation = Quaternion.identity;
            }

            return child;
        }

        private void EnsureDebugBody()
        {
            if (!_createPrimitiveBody) return;

            _body = EnsurePrimitive("DebugBody", PrimitiveType.Capsule, new Vector3(0f, -0.12f, 0f), new Vector3(0.28f, 0.5f, 0.18f), new Color(0.12f, 0.55f, 0.65f, 0.75f));
            _head = EnsurePrimitive("DebugHead", PrimitiveType.Sphere, new Vector3(0f, 0.32f, 0f), new Vector3(0.18f, 0.18f, 0.18f), new Color(0.55f, 0.9f, 0.95f, 0.85f));
            if (_showHandTargets)
            {
                EnsurePrimitive("DebugLeftHand", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.08f, 0.08f, 0.08f), new Color(0.35f, 0.9f, 1f, 0.9f), _leftHandTarget);
                EnsurePrimitive("DebugRightHand", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.08f, 0.08f, 0.08f), new Color(0.35f, 0.9f, 1f, 0.9f), _rightHandTarget);
            }
        }

        private void SetHandTargetVisibility(bool visible)
        {
            if (_leftHandTarget != null) _leftHandTarget.gameObject.SetActive(visible);
            if (_rightHandTarget != null) _rightHandTarget.gameObject.SetActive(visible);
        }

        private Transform EnsurePrimitive(string childName, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color color, Transform parentOverride = null)
        {
            Transform parent = parentOverride != null ? parentOverride : transform;
            Transform child = parent.Find(childName);
            if (child == null)
            {
                if (!_allowRuntimeObjectCreation)
                {
                    Debug.LogError($"[TemporaryDVariantAvatar] Required primitive visual '{childName}' is missing under '{parent.name}'. Runtime object creation is disabled.", this);
                    return null;
                }

                GameObject primitive = GameObject.CreatePrimitive(type);
                primitive.name = childName;
                child = primitive.transform;
                child.SetParent(parent, false);
                Collider collider = primitive.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
            }

            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            child.localScale = localScale;

            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }

            return child;
        }
    }
}
