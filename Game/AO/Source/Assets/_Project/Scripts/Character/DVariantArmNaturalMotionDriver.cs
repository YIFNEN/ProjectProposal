using UnityEngine;

namespace AO.Character
{
    [DefaultExecutionOrder(80)]
    [DisallowMultipleComponent]
    public sealed class DVariantArmNaturalMotionDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DVariantRiderRig _riderRig;
        [SerializeField] private Animator _visualAnimator;
        [SerializeField] private Transform _characterRoot;
        [SerializeField] private Transform _leftHandTarget;
        [SerializeField] private Transform _rightHandTarget;
        [SerializeField] private Transform _leftElbowHint;
        [SerializeField] private Transform _rightElbowHint;

        [Header("Elbow Hints")]
        [SerializeField] private bool _driveElbowHints = true;
        [SerializeField, Range(0.15f, 0.85f)] private float _hintAlongArm = 0.48f;
        [SerializeField, Min(0f)] private float _outwardHintDistance = 0.26f;
        [SerializeField, Min(0f)] private float _downHintDistance = 0.16f;
        [SerializeField, Min(0f)] private float _backHintDistance = 0.12f;
        [SerializeField, Range(0f, 0.7f)] private float _reachDistanceInfluence = 0.22f;
        [SerializeField, Range(1f, 60f)] private float _hintSmoothSpeed = 24f;

        [Header("Wrist Rotation")]
        [SerializeField] private bool _driveWristRotation = true;
        [SerializeField] private Vector3 _leftWristRotationOffsetEuler = Vector3.zero;
        [SerializeField] private Vector3 _rightWristRotationOffsetEuler = Vector3.zero;
        [SerializeField, Range(0f, 1f)] private float _leftWristRotationWeight = 0.9f;
        [SerializeField, Range(0f, 1f)] private float _rightWristRotationWeight = 0.9f;
        [SerializeField, Range(0f, 1f)] private float _twistDamping = 0.35f;
        [SerializeField] private Vector3 _leftTwistAxisLocal = Vector3.forward;
        [SerializeField] private Vector3 _rightTwistAxisLocal = Vector3.forward;
        [SerializeField, Range(1f, 60f)] private float _wristRotationSmoothSpeed = 36f;

        [Header("Debug")]
        [SerializeField] private bool _showArmHintGizmos = true;
        [SerializeField] private Color _leftGizmoColor = new Color(0.1f, 0.9f, 1f, 0.9f);
        [SerializeField] private Color _rightGizmoColor = new Color(1f, 0.35f, 0.9f, 0.9f);

        private Quaternion _leftRestTargetRotation = Quaternion.identity;
        private Quaternion _rightRestTargetRotation = Quaternion.identity;
        private bool _hasRestTargetRotations;

        private void Reset()
        {
            AutoBindMissingReferences();
            ApplyRecommendedDefaults();
            CaptureCurrentTargetRotationsAsRest();
        }

        private void Awake()
        {
            AutoBindMissingReferences();
            CaptureCurrentTargetRotationsAsRest();
        }

        private void OnEnable()
        {
            AutoBindMissingReferences();
            CaptureCurrentTargetRotationsAsRest();
        }

        private void LateUpdate()
        {
            AutoBindMissingReferences();

            if (_driveWristRotation)
            {
                ApplyWristRotation(false);
                ApplyWristRotation(true);
            }

            if (_driveElbowHints)
            {
                UpdateElbowHint(false);
                UpdateElbowHint(true);
            }
        }

        [ContextMenu("Apply Recommended Defaults")]
        public void ApplyRecommendedDefaults()
        {
            _driveElbowHints = true;
            _hintAlongArm = 0.48f;
            _outwardHintDistance = 0.26f;
            _downHintDistance = 0.16f;
            _backHintDistance = 0.12f;
            _reachDistanceInfluence = 0.22f;
            _hintSmoothSpeed = 24f;

            _driveWristRotation = true;
            _leftWristRotationOffsetEuler = Vector3.zero;
            _rightWristRotationOffsetEuler = Vector3.zero;
            _leftWristRotationWeight = 0.9f;
            _rightWristRotationWeight = 0.9f;
            _twistDamping = 0.35f;
            _leftTwistAxisLocal = Vector3.forward;
            _rightTwistAxisLocal = Vector3.forward;
            _wristRotationSmoothSpeed = 36f;
        }

        [ContextMenu("Capture Current Target Rotations As Rest")]
        public void CaptureCurrentTargetRotationsAsRest()
        {
            if (_leftHandTarget != null) _leftRestTargetRotation = _leftHandTarget.rotation;
            if (_rightHandTarget != null) _rightRestTargetRotation = _rightHandTarget.rotation;
            _hasRestTargetRotations = _leftHandTarget != null || _rightHandTarget != null;
        }

        public void BindSceneReferences(
            DVariantRiderRig riderRig,
            Animator visualAnimator,
            Transform characterRoot,
            Transform leftHandTarget,
            Transform rightHandTarget,
            Transform leftElbowHint,
            Transform rightElbowHint)
        {
            _riderRig = riderRig;
            _visualAnimator = visualAnimator;
            _characterRoot = characterRoot;
            _leftHandTarget = leftHandTarget;
            _rightHandTarget = rightHandTarget;
            _leftElbowHint = leftElbowHint;
            _rightElbowHint = rightElbowHint;
            CaptureCurrentTargetRotationsAsRest();
        }

        private void AutoBindMissingReferences()
        {
            if (_riderRig == null) _riderRig = FindFirstObjectByType<DVariantRiderRig>(FindObjectsInactive.Include);
            if (_riderRig != null)
            {
                if (_visualAnimator == null) _visualAnimator = _riderRig.VisualAnimator;
                if (_characterRoot == null) _characterRoot = _riderRig.CharacterRoot;
                if (_leftHandTarget == null) _leftHandTarget = _riderRig.LeftVisualHandTarget;
                if (_rightHandTarget == null) _rightHandTarget = _riderRig.RightVisualHandTarget;
            }

            if (_visualAnimator == null) _visualAnimator = FindFirstHumanAnimator();
            if (_characterRoot == null && _visualAnimator != null) _characterRoot = _visualAnimator.transform.parent;

            Transform visualTargetsRoot = _leftHandTarget != null ? _leftHandTarget.parent : transform;
            if (_leftElbowHint == null && visualTargetsRoot != null) _leftElbowHint = visualTargetsRoot.Find("LeftElbowHint");
            if (_rightElbowHint == null && visualTargetsRoot != null) _rightElbowHint = visualTargetsRoot.Find("RightElbowHint");
        }

        private void UpdateElbowHint(bool rightHand)
        {
            Transform hint = rightHand ? _rightElbowHint : _leftElbowHint;
            Transform handTarget = rightHand ? _rightHandTarget : _leftHandTarget;
            Transform upperArm = GetBone(rightHand ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            if (hint == null || handTarget == null || upperArm == null) return;

            Vector3 shoulder = upperArm.position;
            Vector3 hand = handTarget.position;
            Vector3 shoulderToHand = hand - shoulder;
            float reach = shoulderToHand.magnitude;
            if (reach < 0.001f) return;

            Transform frame = _characterRoot != null ? _characterRoot : (_riderRig != null ? _riderRig.transform : transform);
            Vector3 side = SafeDirection(frame.right, Vector3.right) * (rightHand ? 1f : -1f);
            Vector3 up = SafeDirection(frame.up, Vector3.up);
            Vector3 forward = SafeDirection(frame.forward, Vector3.forward);
            Vector3 armDirection = shoulderToHand / reach;

            Vector3 hintOffset =
                side * (_outwardHintDistance + reach * _reachDistanceInfluence)
                - up * _downHintDistance
                - forward * _backHintDistance;

            hintOffset = Vector3.ProjectOnPlane(hintOffset, armDirection);
            if (hintOffset.sqrMagnitude < 0.0001f)
            {
                hintOffset = Vector3.ProjectOnPlane(side * Mathf.Max(0.05f, _outwardHintDistance), armDirection);
            }

            Vector3 desired = Vector3.Lerp(shoulder, hand, _hintAlongArm) + hintOffset;
            float t = SmoothStep(_hintSmoothSpeed);
            hint.position = Vector3.Lerp(hint.position, desired, t);
        }

        private void ApplyWristRotation(bool rightHand)
        {
            Transform handTarget = rightHand ? _rightHandTarget : _leftHandTarget;
            if (handTarget == null) return;

            if (!_hasRestTargetRotations) CaptureCurrentTargetRotationsAsRest();

            Quaternion rest = rightHand ? _rightRestTargetRotation : _leftRestTargetRotation;
            Vector3 offsetEuler = rightHand ? _rightWristRotationOffsetEuler : _leftWristRotationOffsetEuler;
            float rotationWeight = rightHand ? _rightWristRotationWeight : _leftWristRotationWeight;
            Vector3 twistAxisLocal = rightHand ? _rightTwistAxisLocal : _leftTwistAxisLocal;

            Quaternion desired = handTarget.rotation * Quaternion.Euler(offsetEuler);
            Quaternion corrected = DampTwistFromRest(rest, desired, twistAxisLocal, _twistDamping);
            Quaternion weighted = Quaternion.Slerp(rest, corrected, rotationWeight);
            float t = SmoothStep(_wristRotationSmoothSpeed);
            handTarget.rotation = Quaternion.Slerp(handTarget.rotation, weighted, t);
        }

        private Quaternion DampTwistFromRest(Quaternion rest, Quaternion desired, Vector3 twistAxisLocal, float damping)
        {
            if (damping <= 0f) return desired;

            Vector3 axis = twistAxisLocal.sqrMagnitude > 0.0001f ? twistAxisLocal.normalized : Vector3.forward;
            axis = rest * axis;
            Quaternion delta = desired * Quaternion.Inverse(rest);
            DecomposeSwingTwist(delta, axis, out Quaternion swing, out Quaternion twist);
            Quaternion dampedTwist = Quaternion.Slerp(twist, Quaternion.identity, Mathf.Clamp01(damping));
            return Normalize((swing * dampedTwist) * rest);
        }

        private Transform GetBone(HumanBodyBones bone)
        {
            if (_visualAnimator == null || !_visualAnimator.isHuman) return null;
            return _visualAnimator.GetBoneTransform(bone);
        }

        private static Animator FindFirstHumanAnimator()
        {
            foreach (Animator animator in FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (animator != null && animator.isHuman) return animator;
            }

            return null;
        }

        private static void DecomposeSwingTwist(Quaternion rotation, Vector3 twistAxis, out Quaternion swing, out Quaternion twist)
        {
            Vector3 axis = twistAxis.sqrMagnitude > 0.0001f ? twistAxis.normalized : Vector3.forward;
            Vector3 vector = new Vector3(rotation.x, rotation.y, rotation.z);
            Vector3 projected = Vector3.Project(vector, axis);
            twist = Normalize(new Quaternion(projected.x, projected.y, projected.z, rotation.w));
            swing = Normalize(rotation * Quaternion.Inverse(twist));
        }

        private static Quaternion Normalize(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
            if (magnitude < 0.000001f) return Quaternion.identity;

            float inverse = 1f / magnitude;
            return new Quaternion(value.x * inverse, value.y * inverse, value.z * inverse, value.w * inverse);
        }

        private static Vector3 SafeDirection(Vector3 value, Vector3 fallback)
        {
            return value.sqrMagnitude > 0.0001f ? value.normalized : fallback.normalized;
        }

        private static float SmoothStep(float speed)
        {
            float dt = Time.deltaTime > 0f ? Time.deltaTime : Time.unscaledDeltaTime;
            return 1f - Mathf.Exp(-Mathf.Max(0.01f, speed) * dt);
        }

        private void OnDrawGizmosSelected()
        {
            if (!_showArmHintGizmos) return;
            DrawArmGizmo(false, _leftGizmoColor);
            DrawArmGizmo(true, _rightGizmoColor);
        }

        private void DrawArmGizmo(bool rightHand, Color color)
        {
            Transform hint = rightHand ? _rightElbowHint : _leftElbowHint;
            Transform handTarget = rightHand ? _rightHandTarget : _leftHandTarget;
            Transform upperArm = GetBone(rightHand ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            if (hint == null || handTarget == null || upperArm == null) return;

            Gizmos.color = color;
            Gizmos.DrawLine(upperArm.position, hint.position);
            Gizmos.DrawLine(hint.position, handTarget.position);
            Gizmos.DrawWireSphere(hint.position, 0.035f);
        }
    }
}
