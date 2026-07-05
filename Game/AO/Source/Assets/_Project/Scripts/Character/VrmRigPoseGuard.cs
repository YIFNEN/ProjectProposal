using System.Collections.Generic;
using UnityEngine;

namespace AO.Character
{
    [DefaultExecutionOrder(32000)]
    [DisallowMultipleComponent]
    public sealed class VrmRigPoseGuard : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private bool _restoreVisualRigTransform = true;
        [SerializeField] private bool _restoreHumanoidRootAncestors = true;
        [SerializeField] private bool _restoreLowerBody = true;
        [SerializeField] private bool _restoreLocalScale = true;

        private readonly List<PoseSnapshot> _snapshots = new List<PoseSnapshot>(16);
        private readonly HashSet<Transform> _registered = new HashSet<Transform>();
        private bool _hasSnapshot;

        private void Reset()
        {
            _animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            CaptureCurrentPose();
        }

        private void OnEnable()
        {
            if (!_hasSnapshot) CaptureCurrentPose();
        }

        private void LateUpdate()
        {
            RestoreCapturedPose();
        }

        [ContextMenu("Capture Current Pose")]
        public void CaptureCurrentPose()
        {
            if (_animator == null) _animator = GetComponent<Animator>();

            _snapshots.Clear();
            _registered.Clear();

            if (_restoreVisualRigTransform)
            {
                AddSnapshot(transform);
            }

            Transform hips = Bone(HumanBodyBones.Hips);
            if (_restoreHumanoidRootAncestors && hips != null)
            {
                for (Transform current = hips.parent; current != null && current != transform; current = current.parent)
                {
                    AddSnapshot(current);
                }
            }

            if (_restoreLowerBody)
            {
                AddSnapshot(hips);
                AddSnapshot(Bone(HumanBodyBones.LeftUpperLeg));
                AddSnapshot(Bone(HumanBodyBones.LeftLowerLeg));
                AddSnapshot(Bone(HumanBodyBones.LeftFoot));
                AddSnapshot(Bone(HumanBodyBones.LeftToes));
                AddSnapshot(Bone(HumanBodyBones.RightUpperLeg));
                AddSnapshot(Bone(HumanBodyBones.RightLowerLeg));
                AddSnapshot(Bone(HumanBodyBones.RightFoot));
                AddSnapshot(Bone(HumanBodyBones.RightToes));
            }

            _registered.Clear();
            _hasSnapshot = _snapshots.Count > 0;
        }

        [ContextMenu("Restore Captured Pose")]
        public void RestoreCapturedPose()
        {
            if (!_hasSnapshot) return;

            for (int i = 0; i < _snapshots.Count; i++)
            {
                _snapshots[i].Restore(_restoreLocalScale);
            }
        }

        private Transform Bone(HumanBodyBones bone)
        {
            if (_animator == null || !_animator.isHuman) return null;
            return _animator.GetBoneTransform(bone);
        }

        private void AddSnapshot(Transform target)
        {
            if (target == null || !_registered.Add(target)) return;
            _snapshots.Add(new PoseSnapshot(target));
        }

        private readonly struct PoseSnapshot
        {
            private readonly Transform _target;
            private readonly Vector3 _localPosition;
            private readonly Quaternion _localRotation;
            private readonly Vector3 _localScale;

            public PoseSnapshot(Transform target)
            {
                _target = target;
                _localPosition = target.localPosition;
                _localRotation = target.localRotation;
                _localScale = target.localScale;
            }

            public void Restore(bool restoreScale)
            {
                if (_target == null) return;

                _target.localPosition = _localPosition;
                _target.localRotation = _localRotation;
                if (restoreScale) _target.localScale = _localScale;
            }
        }
    }
}
