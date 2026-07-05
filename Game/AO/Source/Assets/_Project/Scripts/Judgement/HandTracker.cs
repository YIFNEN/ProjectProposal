using UnityEngine;
using UnityEngine.XR;

namespace AO.Judgement
{
    public class HandTracker : MonoBehaviour, IHandSource
    {
        [Header("Hand Identification")]
        [SerializeField] private XRNode _xrNode = XRNode.LeftHand;

        [Header("Smoothing")]
        [SerializeField, Range(0f, 0.95f)] private float _smoothing = 0.3f;

        [Header("Tracking Validity")]
        [SerializeField] private float _stallTimeout = 1f;

        [Header("Debug Visual")]
        [SerializeField] private bool _showTouchVolume = true;
        [SerializeField] private Color _touchVolumeColor = new Color(0.25f, 0.9f, 1f, 0.42f);
        [SerializeField] private bool _allowRuntimeTouchVolumeCreation = false;

        public Vector3 Position => transform.position;
        public Vector3 Velocity { get; private set; }
        public bool IsTracked { get; private set; } = true;

        private Vector3 _lastPosition;
        private float _stallTimer;
        private InputDevice _device;
        private bool _deviceCached;
        private Transform _touchVolumeVisual;

        private void Awake()
        {
            _lastPosition = transform.position;
            EnsureTouchVolumeVisual();
        }

        private void OnEnable()
        {
            _lastPosition = transform.position;
            Velocity = Vector3.zero;
            _stallTimer = 0f;
            IsTracked = true;
            _deviceCached = false;
            EnsureTouchVolumeVisual();
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 currentPos = transform.position;
            Vector3 instantVel = (currentPos - _lastPosition) / dt;
            Velocity = Vector3.Lerp(instantVel, Velocity, _smoothing);
            _lastPosition = currentPos;

            if (instantVel.sqrMagnitude < 1e-6f)
            {
                _stallTimer += dt;
                if (_stallTimer >= _stallTimeout) IsTracked = false;
            }
            else
            {
                _stallTimer = 0f;
                IsTracked = true;
            }

            UpdateTouchVolumeVisual();
        }

        public void PlayHaptic(float amplitude, float duration)
        {
            if (!isActiveAndEnabled) return;
            if (!TryEnsureDevice()) return;

            if (_device.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
            {
                _device.SendHapticImpulse(0, Mathf.Clamp01(amplitude), Mathf.Max(0f, duration));
            }
        }

        private bool TryEnsureDevice()
        {
            if (_deviceCached && _device.isValid) return true;
            _device = InputDevices.GetDeviceAtXRNode(_xrNode);
            _deviceCached = _device.isValid;
            return _deviceCached;
        }

        private void EnsureTouchVolumeVisual()
        {
            if (!_showTouchVolume)
            {
                if (_touchVolumeVisual != null) _touchVolumeVisual.gameObject.SetActive(false);
                return;
            }

            if (_touchVolumeVisual == null)
            {
                Transform existing = transform.Find("TouchVolumeVisual");
                if (existing != null)
                {
                    _touchVolumeVisual = existing;
                }
                else
                {
                    if (!_allowRuntimeTouchVolumeCreation)
                    {
                        Debug.LogError("[HandTracker] Required child 'TouchVolumeVisual' is missing. Runtime debug visual creation is disabled.", this);
                        return;
                    }

                    GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    visual.name = "TouchVolumeVisual";
                    visual.transform.SetParent(transform, false);
                    Collider visualCollider = visual.GetComponent<Collider>();
                    if (visualCollider != null) Destroy(visualCollider);
                    _touchVolumeVisual = visual.transform;
                }
            }

            _touchVolumeVisual.gameObject.SetActive(true);
            UpdateTouchVolumeVisual();
        }

        private void UpdateTouchVolumeVisual()
        {
            if (_touchVolumeVisual == null || !_showTouchVolume) return;

            _touchVolumeVisual.localPosition = Vector3.zero;
            _touchVolumeVisual.localRotation = Quaternion.identity;

            float radius = 0.1125f;
            SphereCollider sphere = GetComponent<SphereCollider>();
            if (sphere != null) radius = Mathf.Max(0.005f, sphere.radius);
            _touchVolumeVisual.localScale = Vector3.one * radius * 2f;

            Renderer renderer = _touchVolumeVisual.GetComponent<Renderer>();
            if (renderer == null) return;

            if (renderer.sharedMaterial == null || renderer.sharedMaterial.name != "HandTouchVolume_Debug")
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Standard");
                renderer.sharedMaterial = new Material(shader) { name = "HandTouchVolume_Debug" };
            }

            Material material = renderer.sharedMaterial;
            material.color = _touchVolumeColor;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", _touchVolumeColor);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        }
    }
}
