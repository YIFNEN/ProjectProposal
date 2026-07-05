using UnityEngine;
using UnityEngine.XR;

namespace AO.Judgement
{
    /// <summary>
    /// XRNode의 디바이스 포즈를 Transform에 반영하는 가벼운 손 포즈 팔로워.
    /// XR Origin에 컨트롤러 프리팹이 아직 없을 때도 손 콜라이더 실기 테스트를 진행할 수 있게 한다.
    /// </summary>
    public class XRHandPoseFollower : MonoBehaviour
    {
        [SerializeField] private XRNode _xrNode = XRNode.LeftHand;
        [SerializeField] private bool _applyPosition = true;
        [SerializeField] private bool _applyRotation = true;
        [SerializeField, Range(0.25f, 5f)] private float _positionSensitivity = 1f;
        [SerializeField] private bool _useInitialPoseAsNeutral = true;
        [SerializeField] private Vector3 _positionOffset = Vector3.zero;
        [SerializeField] private bool _logPoseState = true;

        private InputDevice _device;
        private bool _hasNeutralPose;
        private Vector3 _neutralDevicePosition;
        private Vector3 _neutralLocalPosition;
        private bool _loggedPoseAcquired;
        private bool _loggedMissingDevice;
        private float _missingDeviceTimer;

        private void OnEnable()
        {
            _device = default;
            _hasNeutralPose = false;
            _neutralLocalPosition = transform.localPosition;
            _loggedPoseAcquired = false;
            _loggedMissingDevice = false;
            _missingDeviceTimer = 0f;
        }

        private void Update()
        {
            if (!_device.isValid) _device = InputDevices.GetDeviceAtXRNode(_xrNode);
            if (!_device.isValid)
            {
                if (_logPoseState && !_loggedMissingDevice)
                {
                    _missingDeviceTimer += Time.unscaledDeltaTime;
                    if (_missingDeviceTimer >= 2f)
                    {
                        Debug.LogWarning($"[XRHandPoseFollower] {_xrNode} device is not valid yet for {name}. Bubble touch will not follow this controller.");
                        _loggedMissingDevice = true;
                    }
                }
                return;
            }

            if (_applyPosition && _device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
            {
                if (_logPoseState && !_loggedPoseAcquired)
                {
                    Debug.Log($"[XRHandPoseFollower] {_xrNode} pose acquired for {name}. Neutral local={_neutralLocalPosition}, sensitivity={_positionSensitivity:0.##}.");
                    _loggedPoseAcquired = true;
                }

                Vector3 targetPosition = position;
                float sensitivity = Mathf.Max(0.01f, _positionSensitivity);

                if (_useInitialPoseAsNeutral)
                {
                    if (!_hasNeutralPose)
                    {
                        _neutralDevicePosition = position;
                        _neutralLocalPosition = transform.localPosition;
                        _hasNeutralPose = true;
                    }

                    targetPosition = _neutralLocalPosition + (position - _neutralDevicePosition) * sensitivity;
                }
                else if (!Mathf.Approximately(sensitivity, 1f))
                {
                    targetPosition = position * sensitivity;
                }

                transform.localPosition = targetPosition + _positionOffset;
            }

            if (_applyRotation && _device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            {
                transform.localRotation = rotation;
            }
        }
    }
}
