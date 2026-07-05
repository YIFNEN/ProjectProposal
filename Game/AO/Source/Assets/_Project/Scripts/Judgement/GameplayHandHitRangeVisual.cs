using UnityEngine;

namespace AO.Judgement
{
    [DisallowMultipleComponent]
    public sealed class GameplayHandHitRangeVisual : MonoBehaviour
    {
        private const string VisualInstanceName = "GameplayHandHitRangeVisual";

        [Header("Visual Source")]
        [SerializeField] private GameObject _visualPrefab;
        [SerializeField] private bool _visible = true;

        [Header("Sizing")]
        [SerializeField, Min(0.1f)] private float _radiusMultiplier = 1f;
        [SerializeField, Min(0.005f)] private float _fallbackRadius = 0.1125f;

        [Header("Rolling Visual")]
        [SerializeField] private bool _rollVisual = true;
        [SerializeField, Min(0f)] private float _rollDegreesPerSecond = 90f;
        [SerializeField] private Vector3 _rollAxisLocal = Vector3.right;

        [Header("Tint")]
        [SerializeField] private Color _baseColor = new Color(0.18f, 0.95f, 1f, 0.28f);
        [SerializeField] private Color _innerColor = new Color(0.45f, 0.85f, 1f, 1f);
        [SerializeField] private Color _rimColor = new Color(0.8f, 1f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float _baseAlpha = 0.28f;

        private Transform _visualInstance;
        private Renderer[] _renderers;
        private SphereCollider _sphereCollider;
        private MaterialPropertyBlock _propertyBlock;
        private float _rollAngle;

        private void OnEnable()
        {
            EnsureVisual();
            UpdateVisual();
        }

        private void LateUpdate()
        {
            EnsureVisual();
            UpdateVisual();
        }

        private void OnDisable()
        {
            if (_visualInstance != null) _visualInstance.gameObject.SetActive(false);
        }

        private void EnsureVisual()
        {
            if (_visualPrefab == null) return;

            if (_visualInstance == null)
            {
                Transform existing = transform.Find(VisualInstanceName);
                if (existing != null)
                {
                    _visualInstance = existing;
                }
                else
                {
                    GameObject instance = Instantiate(_visualPrefab, transform);
                    instance.name = VisualInstanceName;
                    _visualInstance = instance.transform;
                }

                DisablePhysics(_visualInstance.gameObject);
                _renderers = _visualInstance.GetComponentsInChildren<Renderer>(true);
            }

            _sphereCollider ??= GetComponent<SphereCollider>();
            _propertyBlock ??= new MaterialPropertyBlock();
        }

        private void UpdateVisual()
        {
            if (_visualInstance == null) return;

            bool active = _visible && _visualPrefab != null;
            if (_visualInstance.gameObject.activeSelf != active) _visualInstance.gameObject.SetActive(active);
            if (!active) return;

            _visualInstance.localPosition = Vector3.zero;
            _visualInstance.localRotation = CurrentVisualRotation();
            _visualInstance.localScale = LocalScaleForWorldRadius(CurrentWorldRadius());

            ApplyTint();
        }

        private Quaternion CurrentVisualRotation()
        {
            if (!_rollVisual || _rollDegreesPerSecond <= 0f) return Quaternion.identity;

            _rollAngle = Mathf.Repeat(_rollAngle + _rollDegreesPerSecond * Time.deltaTime, 360f);
            Vector3 axis = _rollAxisLocal.sqrMagnitude > 0.0001f ? _rollAxisLocal.normalized : Vector3.right;
            return Quaternion.AngleAxis(_rollAngle, axis);
        }

        private float CurrentWorldRadius()
        {
            float localRadius = _sphereCollider != null ? _sphereCollider.radius : _fallbackRadius;
            float worldRadius = localRadius * MaxAbs(transform.lossyScale);
            return Mathf.Max(0.005f, worldRadius * _radiusMultiplier);
        }

        private Vector3 LocalScaleForWorldRadius(float worldRadius)
        {
            Vector3 scale = transform.lossyScale;
            float diameter = worldRadius * 2f;
            return new Vector3(
                diameter / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                diameter / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                diameter / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
        }

        private void ApplyTint()
        {
            if (_renderers == null) return;

            Color baseColor = _baseColor;
            baseColor.a = _baseAlpha;

            foreach (Renderer item in _renderers)
            {
                if (item == null) continue;

                item.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                item.receiveShadows = false;
                item.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_Color", baseColor);
                _propertyBlock.SetColor("_BaseColor", baseColor);
                _propertyBlock.SetColor("_InnerColor", _innerColor);
                _propertyBlock.SetColor("_RimColor", _rimColor);
                _propertyBlock.SetFloat("_BaseAlpha", _baseAlpha);
                item.SetPropertyBlock(_propertyBlock);
            }
        }

        private static void DisablePhysics(GameObject root)
        {
            foreach (Collider item in root.GetComponentsInChildren<Collider>(true))
            {
                item.enabled = false;
            }

            foreach (Rigidbody item in root.GetComponentsInChildren<Rigidbody>(true))
            {
                item.isKinematic = true;
                item.useGravity = false;
            }
        }

        private static float MaxAbs(Vector3 value)
        {
            return Mathf.Max(Mathf.Abs(value.x), Mathf.Max(Mathf.Abs(value.y), Mathf.Abs(value.z)));
        }
    }
}
