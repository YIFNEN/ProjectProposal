using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AO.Rhythm
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class LaneTuningHandle : MonoBehaviour
    {
        [SerializeField] private string _label = "Lane";
        [SerializeField] private Color _color = new Color(0.25f, 0.88f, 0.88f, 0.8f);
        [SerializeField, Min(0.01f)] private float _radius = 0.055f;

        public void Configure(string label, Color color, float radius)
        {
            _label = label;
            _color = color;
            _radius = Mathf.Max(0.01f, radius);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = _color;
            Gizmos.DrawSphere(transform.position, _radius);
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, _radius * 1.18f);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Handles.color = Color.white;
                Handles.Label(transform.position + Vector3.up * (_radius * 1.8f), _label);
            }
#endif
        }
    }
}
