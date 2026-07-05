using UnityEngine;
using UnityEngine.XR;

namespace AO.Character
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public class OculusUpperBodyDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _xrTrackingRoot;
        [SerializeField] private Transform _hmd;
        [SerializeField] private Transform _leftController;
        [SerializeField] private Transform _rightController;
        [SerializeField] private Transform _leftHandTarget;
        [SerializeField] private Transform _rightHandTarget;
        [SerializeField] private DVariantRiderRig _riderRig;

        [Header("Auto Bind")]
        [SerializeField] private bool _autoBindMissingSceneTargets = true;
        [SerializeField] private bool _readMissingReferencesFromXRNodes = true;

        [Header("Upper Body")]
        [SerializeField] private bool _driveHead = true;
        [SerializeField] private bool _driveChest = true;
        [SerializeField, Range(0f, 1f)] private float _chestYawWeight = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _chestPitchWeight = 0.12f;
        [SerializeField, Range(0f, 1f)] private float _neckWeight = 0.24f;
        [SerializeField, Range(0f, 1f)] private float _headWeight = 0.45f;
        [SerializeField, Range(1f, 30f)] private float _poseSmoothSpeed = 12f;
        [SerializeField] private bool _useCalibratedHmdReference = true;
        [SerializeField] private bool _driveLeanFromHmdPosition = true;
        [SerializeField, Range(0f, 45f)] private float _leanPitchDegreesPerMeter = 10f;
        [SerializeField, Range(0f, 45f)] private float _leanRollDegreesPerMeter = 8f;
        [SerializeField, Range(0f, 30f)] private float _maxLeanDegrees = 8f;

        [Header("Hands")]
        [SerializeField] private bool _driveHandsWithAnimatorIk;
        [SerializeField, Range(0f, 1f)] private float _handIkPositionWeight = 0.85f;
        [SerializeField, Range(0f, 1f)] private float _handIkRotationWeight = 0.55f;

        private Transform _chest;
        private Transform _upperChest;
        private Transform _neck;
        private Transform _head;
        private Quaternion _chestBaseLocalRotation = Quaternion.identity;
        private Quaternion _upperChestBaseLocalRotation = Quaternion.identity;
        private Quaternion _neckBaseLocalRotation = Quaternion.identity;
        private Quaternion _headBaseLocalRotation = Quaternion.identity;
        private Quaternion _smoothedChestAdditive = Quaternion.identity;
        private Quaternion _smoothedNeckAdditive = Quaternion.identity;
        private Quaternion _smoothedHeadAdditive = Quaternion.identity;
        private bool _hasHmdReference;
        private Vector3 _hmdReferenceWorldPosition;
        private Quaternion _hmdReferenceWorldRotation = Quaternion.identity;
        private InputDevice _hmdDevice;
        private InputDevice _leftDevice;
        private InputDevice _rightDevice;

        private void Reset()
        {
            _animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            AutoBindMissingReferences();
            CacheHumanBones();
        }

        private void LateUpdate()
        {
            if (_animator == null || !_animator.isHuman) return;
            if (_autoBindMissingSceneTargets) AutoBindMissingReferences();
            DriveUpperBody();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!_driveHandsWithAnimatorIk || _animator == null || !_animator.isHuman) return;

            ApplyHandIk(AvatarIKGoal.LeftHand, _leftHandTarget != null ? _leftHandTarget : _leftController, XRNode.LeftHand, ref _leftDevice);
            ApplyHandIk(AvatarIKGoal.RightHand, _rightHandTarget != null ? _rightHandTarget : _rightController, XRNode.RightHand, ref _rightDevice);
        }

        [ContextMenu("Auto Bind Missing Scene Targets")]
        private void AutoBindMissingReferences()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (!_autoBindMissingSceneTargets) return;

            if (_hmd == null && Camera.main != null) _hmd = Camera.main.transform;

            if (_riderRig == null) _riderRig = FindSceneRiderRig();
            if (_riderRig != null)
            {
                if (_leftHandTarget == null) _leftHandTarget = _riderRig.LeftVisualHandTarget;
                if (_rightHandTarget == null) _rightHandTarget = _riderRig.RightVisualHandTarget;
            }

            if (_leftController == null)
            {
                GameObject left = GameObject.Find("LeftHand");
                if (left != null) _leftController = left.transform;
            }

            if (_rightController == null)
            {
                GameObject right = GameObject.Find("RightHand");
                if (right != null) _rightController = right.transform;
            }
        }

        [ContextMenu("Recapture Current Bone Pose As Base")]
        private void RecaptureCurrentBonePoseAsBase()
        {
            CacheHumanBones();
        }

        [ContextMenu("Capture Current HMD As Upper Body Reference")]
        public void CaptureCurrentHmdAsUpperBodyReference()
        {
            if (!TryGetPose(_hmd, XRNode.CenterEye, ref _hmdDevice, out Vector3 position, out Quaternion rotation)) return;
            ApplyUpperBodyCalibration(position, rotation);
        }

        public void ApplyUpperBodyCalibration(Vector3 hmdWorldPosition, Quaternion hmdWorldRotation)
        {
            _hmdReferenceWorldPosition = hmdWorldPosition;
            _hmdReferenceWorldRotation = hmdWorldRotation;
            _hasHmdReference = true;
            _smoothedChestAdditive = Quaternion.identity;
            _smoothedNeckAdditive = Quaternion.identity;
            _smoothedHeadAdditive = Quaternion.identity;
        }

        private void CacheHumanBones()
        {
            if (_animator == null || !_animator.isHuman) return;

            _chest = _animator.GetBoneTransform(HumanBodyBones.Chest);
            _upperChest = _animator.GetBoneTransform(HumanBodyBones.UpperChest);
            _neck = _animator.GetBoneTransform(HumanBodyBones.Neck);
            _head = _animator.GetBoneTransform(HumanBodyBones.Head);

            if (_chest != null) _chestBaseLocalRotation = _chest.localRotation;
            if (_upperChest != null) _upperChestBaseLocalRotation = _upperChest.localRotation;
            if (_neck != null) _neckBaseLocalRotation = _neck.localRotation;
            if (_head != null) _headBaseLocalRotation = _head.localRotation;
        }

        private static DVariantRiderRig FindSceneRiderRig()
        {
            return FindFirstObjectByType<DVariantRiderRig>(FindObjectsInactive.Include);
        }

        public void BindSceneTargets(
            DVariantRiderRig riderRig,
            Transform hmd,
            Transform leftController,
            Transform rightController,
            Transform xrTrackingRoot = null)
        {
            _riderRig = riderRig;
            _hmd = hmd;
            _leftController = leftController;
            _rightController = rightController;
            _xrTrackingRoot = xrTrackingRoot;

            if (_riderRig != null)
            {
                _leftHandTarget = _riderRig.LeftVisualHandTarget;
                _rightHandTarget = _riderRig.RightVisualHandTarget;
            }

            CacheHumanBones();
        }

        private void DriveUpperBody()
        {
            if (!TryGetPose(_hmd, XRNode.CenterEye, ref _hmdDevice, out Vector3 hmdPosition, out Quaternion hmdRotation)) return;

            Vector3 localForward = (_useCalibratedHmdReference && _hasHmdReference)
                ? Quaternion.Inverse(_hmdReferenceWorldRotation) * (hmdRotation * Vector3.forward)
                : transform.InverseTransformDirection(hmdRotation * Vector3.forward);
            if (localForward.sqrMagnitude < 0.0001f) return;

            float yaw = Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(localForward.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            float leanPitch = 0f;
            float leanRoll = 0f;

            if (_driveLeanFromHmdPosition && _hasHmdReference)
            {
                Vector3 currentLocal = transform.InverseTransformPoint(hmdPosition);
                Vector3 referenceLocal = transform.InverseTransformPoint(_hmdReferenceWorldPosition);
                Vector3 delta = currentLocal - referenceLocal;
                leanPitch = Mathf.Clamp(delta.z * _leanPitchDegreesPerMeter, -_maxLeanDegrees, _maxLeanDegrees);
                leanRoll = Mathf.Clamp(-delta.x * _leanRollDegreesPerMeter, -_maxLeanDegrees, _maxLeanDegrees);
            }

            float t = 1f - Mathf.Exp(-_poseSmoothSpeed * Time.deltaTime);

            Quaternion chestAdditive = Quaternion.Euler(pitch * _chestPitchWeight + leanPitch, yaw * _chestYawWeight, leanRoll);
            Quaternion neckAdditive = Quaternion.Euler(pitch * _neckWeight, yaw * _neckWeight, 0f);
            Quaternion headAdditive = Quaternion.Euler(pitch * _headWeight, yaw * _headWeight, 0f);

            _smoothedChestAdditive = Quaternion.Slerp(_smoothedChestAdditive, chestAdditive, t);
            _smoothedNeckAdditive = Quaternion.Slerp(_smoothedNeckAdditive, neckAdditive, t);
            _smoothedHeadAdditive = Quaternion.Slerp(_smoothedHeadAdditive, headAdditive, t);

            if (_driveChest)
            {
                if (_chest != null) _chest.localRotation = _chestBaseLocalRotation * _smoothedChestAdditive;
                if (_upperChest != null) _upperChest.localRotation = _upperChestBaseLocalRotation * _smoothedChestAdditive;
            }

            if (_driveHead)
            {
                if (_neck != null) _neck.localRotation = _neckBaseLocalRotation * _smoothedNeckAdditive;
                if (_head != null) _head.localRotation = _headBaseLocalRotation * _smoothedHeadAdditive;
            }
        }

        private void ApplyHandIk(AvatarIKGoal goal, Transform target, XRNode node, ref InputDevice device)
        {
            if (!TryGetPose(target, node, ref device, out Vector3 position, out Quaternion rotation))
            {
                _animator.SetIKPositionWeight(goal, 0f);
                _animator.SetIKRotationWeight(goal, 0f);
                return;
            }

            _animator.SetIKPositionWeight(goal, _handIkPositionWeight);
            _animator.SetIKRotationWeight(goal, _handIkRotationWeight);
            _animator.SetIKPosition(goal, position);
            _animator.SetIKRotation(goal, rotation);
        }

        private bool TryGetPose(Transform source, XRNode node, ref InputDevice device, out Vector3 position, out Quaternion rotation)
        {
            if (source != null)
            {
                position = source.position;
                rotation = source.rotation;
                return true;
            }

            if (!_readMissingReferencesFromXRNodes)
            {
                position = default;
                rotation = default;
                return false;
            }

            if (!device.isValid) device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid)
            {
                position = default;
                rotation = default;
                return false;
            }

            bool hasPosition = device.TryGetFeatureValue(CommonUsages.devicePosition, out position);
            bool hasRotation = device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);
            if (!hasPosition || !hasRotation) return false;

            if (_xrTrackingRoot != null)
            {
                position = _xrTrackingRoot.TransformPoint(position);
                rotation = _xrTrackingRoot.rotation * rotation;
            }

            return true;
        }
    }
}
