using UnityEngine;
using UnityEngine.XR;

namespace AO.Judgement
{
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class JudgementHandTarget : MonoBehaviour, IHandSource
    {
        [SerializeField] private HandTracker _hapticSource;
        [SerializeField] private bool _sendHapticsDirectlyToXRNode = true;
        [SerializeField] private bool _inferHapticNodeFromObjectName = true;
        [SerializeField] private XRNode _hapticNode = XRNode.LeftHand;
        [SerializeField, Min(0.005f)] private float _colliderRadius = 0.1125f;
        [SerializeField, Range(0f, 0.95f)] private float _velocitySmoothing = 0.15f;
        [SerializeField] private bool _configureAsHandCollider = true;

        public Vector3 Position => transform.position;
        public Vector3 Velocity { get; private set; }
        public bool IsTracked => _hapticSource == null || _hapticSource.IsTracked;

        private Vector3 _lastPosition;
        private bool _hasLastPosition;
        private InputDevice _hapticDevice;

        private void Reset()
        {
            ApplyColliderSettings();
            ResetVelocity();
        }

        private void Awake()
        {
            ApplyColliderSettings();
            ResetVelocity();
        }

        private void OnEnable()
        {
            ApplyColliderSettings();
            ResetVelocity();
        }

        private void OnValidate()
        {
            _colliderRadius = Mathf.Max(0.005f, _colliderRadius);
            ApplyColliderSettings();
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                ResetVelocity();
                return;
            }

            if (!_hasLastPosition)
            {
                ResetVelocity();
                return;
            }

            Vector3 current = transform.position;
            Vector3 instantVelocity = (current - _lastPosition) / dt;
            Velocity = Vector3.Lerp(instantVelocity, Velocity, _velocitySmoothing);
            _lastPosition = current;
        }

        public void Configure(HandTracker hapticSource, float colliderRadius)
        {
            _hapticSource = hapticSource;
            _colliderRadius = Mathf.Max(0.005f, colliderRadius);
            ApplyColliderSettings();
            ResetVelocity();
        }

        public void Configure(XRNode hapticNode, float colliderRadius)
        {
            _hapticSource = null;
            _sendHapticsDirectlyToXRNode = true;
            _inferHapticNodeFromObjectName = false;
            _hapticNode = hapticNode;
            _colliderRadius = Mathf.Max(0.005f, colliderRadius);
            ApplyColliderSettings();
            ResetVelocity();
        }

        public void PlayHaptic(float amplitude, float duration)
        {
            if (_sendHapticsDirectlyToXRNode && TrySendDirectHaptic(amplitude, duration)) return;
            if (_hapticSource == null) return;
            _hapticSource.PlayHaptic(amplitude, duration);
        }

        private bool TrySendDirectHaptic(float amplitude, float duration)
        {
            if (amplitude <= 0f || duration <= 0f) return true;

            XRNode node = EffectiveHapticNode();
            if (!_hapticDevice.isValid) _hapticDevice = InputDevices.GetDeviceAtXRNode(node);
            if (!_hapticDevice.isValid) return false;

            if (_hapticDevice.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
            {
                _hapticDevice.SendHapticImpulse(0, Mathf.Clamp01(amplitude), Mathf.Max(0f, duration));
                return true;
            }

            return false;
        }

        private XRNode EffectiveHapticNode()
        {
            if (!_inferHapticNodeFromObjectName) return _hapticNode;

            string objectName = gameObject.name;
            if (objectName.Contains("Right")) return XRNode.RightHand;
            if (objectName.Contains("Left")) return XRNode.LeftHand;
            return _hapticNode;
        }

        private void ResetVelocity()
        {
            _lastPosition = transform.position;
            Velocity = Vector3.zero;
            _hasLastPosition = true;
        }

        private void ApplyColliderSettings()
        {
            if (!_configureAsHandCollider) return;

            TrySetHandTag();

            SphereCollider sphere = GetComponent<SphereCollider>();
            if (sphere != null)
            {
                sphere.isTrigger = true;
                sphere.radius = Mathf.Max(0.005f, _colliderRadius);
                sphere.center = Vector3.zero;
            }

            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.useGravity = false;
                body.isKinematic = true;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
        }

        private void TrySetHandTag()
        {
            try
            {
                gameObject.tag = "Hand";
            }
            catch (UnityException)
            {
                // The editor setup creates the tag. Until then the object will work after the tag exists.
            }
        }
    }
}
