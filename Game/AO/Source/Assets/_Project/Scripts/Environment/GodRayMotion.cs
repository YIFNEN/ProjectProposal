using UnityEngine;

namespace AO.Environment
{
    [DisallowMultipleComponent]
    public sealed class GodRayMotion : MonoBehaviour
    {
        [Header("Gentle underwater motion")]
        [SerializeField, Range(0f, 5f)] private float _swayDegrees = 1.2f;
        [SerializeField, Min(0f)] private float _swaySpeed = 0.18f;
        [SerializeField, Min(0f)] private float _positionSway = 0.04f;

        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;
        private float _phase;

        private void OnEnable()
        {
            _baseLocalPosition = transform.localPosition;
            _baseLocalRotation = transform.localRotation;
            _phase = Mathf.Abs(GetInstanceID() * 0.00137f) % (Mathf.PI * 2f);
        }

        private void Update()
        {
            float time = Time.time * _swaySpeed + _phase;
            float roll = Mathf.Sin(time) * _swayDegrees;
            float yaw = Mathf.Sin(time * 0.73f + 1.1f) * _swayDegrees * 0.35f;
            float horizontalOffset = Mathf.Sin(time * 0.61f) * _positionSway;

            transform.localRotation = _baseLocalRotation * Quaternion.Euler(0f, yaw, roll);
            transform.localPosition = _baseLocalPosition + Vector3.right * horizontalOffset;
        }
    }
}
