using UnityEngine;
using UnityEngine.XR;

namespace AO.Judgement
{
    /// <summary>
    /// Copies an XRNode device pose to this transform.
    /// Used by the lightweight UI rig in non-gameplay scenes where a full XR Origin prefab is not required yet.
    /// </summary>
    public class XRNodePoseFollower : MonoBehaviour
    {
        [SerializeField] private XRNode _xrNode = XRNode.CenterEye;
        [SerializeField] private bool _applyPosition = true;
        [SerializeField] private bool _applyRotation = true;
        [SerializeField] private bool _localPose = true;

        private InputDevice _device;

        private void OnEnable()
        {
            _device = default;
        }

        private void Update()
        {
            if (!_device.isValid) _device = InputDevices.GetDeviceAtXRNode(_xrNode);
            if (!_device.isValid) return;

            if (_applyPosition && _device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
            {
                if (_localPose) transform.localPosition = position;
                else transform.position = position;
            }

            if (_applyRotation && _device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            {
                if (_localPose) transform.localRotation = rotation;
                else transform.rotation = rotation;
            }
        }
    }
}
