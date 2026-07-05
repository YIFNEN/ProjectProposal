using AO.Rhythm;
using UnityEngine;

namespace AO.Judgement
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class DebugLaneHitPad : MonoBehaviour, IHandSource
    {
        [Header("Lane")]
        [SerializeField] private Lane _lane = Lane.Center;
        [SerializeField] private Vector3 _laneOffset = Vector3.zero;

        [Header("Debug Tuning")]
        [Tooltip("Extra local Z offset from the lane hit position. Use this to inspect judgement timing.")]
        [SerializeField] private float _debugZOffset;
        [Tooltip("Trigger radius used as the fake hand hit area.")]
        [SerializeField, Min(0.005f)] private float _colliderRadius = 0.1125f;
        [SerializeField] private bool _applyLocalPosition = true;

#if UNITY_EDITOR
        private bool _editorApplyQueued;
#endif

        public Lane Lane => _lane;
        public float DebugZOffset => _debugZOffset;
        public Vector3 Position => transform.position;
        public Vector3 Velocity => Vector3.zero;
        public bool IsTracked => isActiveAndEnabled;

        private void Reset()
        {
            ApplySettings(allowTagChange: true);
        }

        private void Awake()
        {
            ApplySettings(allowTagChange: true);
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            QueueEditorApply();
#else
            ApplySettings(allowTagChange: true);
#endif
        }

        public void Configure(Lane lane, Vector3 laneOffset, float debugZOffset, float colliderRadius)
        {
            _lane = lane;
            _laneOffset = laneOffset;
            _debugZOffset = debugZOffset;
            _colliderRadius = Mathf.Max(0.005f, colliderRadius);
            ApplySettings(allowTagChange: true);
        }

        public void SetDebugZOffset(float debugZOffset)
        {
            _debugZOffset = debugZOffset;
            ApplySettings(allowTagChange: true);
        }

        public void PlayHaptic(float amplitude, float duration)
        {
            // Editor/debug fake hand: no hardware haptics.
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= DelayedEditorApply;
#endif
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isActiveAndEnabled ? Color.yellow : new Color(0.25f, 0.9f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.005f, _colliderRadius));
        }

        private void ApplySettings(bool allowTagChange)
        {
            if (allowTagChange)
            {
                TrySetHandTag();
            }

            var sphere = GetComponent<SphereCollider>();
            if (sphere != null)
            {
                sphere.isTrigger = true;
                sphere.radius = Mathf.Max(0.005f, _colliderRadius);
                sphere.center = Vector3.zero;
            }

            var body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.useGravity = false;
                body.isKinematic = true;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }

            if (_applyLocalPosition)
            {
                transform.localPosition = _laneOffset + new Vector3(0f, 0f, _debugZOffset);
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
                // The editor setup creates the tag. If the component is added manually first,
                // the object will still work after the tag is added.
            }
        }

#if UNITY_EDITOR
        private void QueueEditorApply()
        {
            if (_editorApplyQueued) return;

            _editorApplyQueued = true;
            UnityEditor.EditorApplication.delayCall += DelayedEditorApply;
        }

        private void DelayedEditorApply()
        {
            UnityEditor.EditorApplication.delayCall -= DelayedEditorApply;
            _editorApplyQueued = false;

            if (this == null) return;
            ApplySettings(allowTagChange: true);
        }
#endif
    }
}
