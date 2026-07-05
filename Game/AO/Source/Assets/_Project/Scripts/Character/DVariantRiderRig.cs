using UnityEngine;
using UnityEngine.XR;

namespace AO.Character
{
    [DisallowMultipleComponent]
    public class DVariantRiderRig : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Transform _hmd;
        [SerializeField] private Transform _leftController;
        [SerializeField] private Transform _rightController;
        [SerializeField] private Transform _hitAnchor;
        [SerializeField] private Transform _leftHandTarget;
        [SerializeField] private Transform _rightHandTarget;
        [SerializeField] private Transform _leftVisualHandTarget;
        [SerializeField] private Transform _rightVisualHandTarget;
        [SerializeField] private Transform _characterRoot;
        [SerializeField] private Transform _mantaRoot;
        [SerializeField] private Animator _visualAnimator;

        [Header("D-Variant Placement")]
        [SerializeField] private bool _followHmd;
        [SerializeField] private bool _snapOnEnable;
        [SerializeField] private float _distanceInFrontOfHmd = 1f;
        [SerializeField] private float _heightOffsetFromHmd = -0.28f;
        [SerializeField] private float _lazyFollowThreshold = 0.35f;
        [SerializeField] private float _lazyFollowSpeed = 3.5f;
        [SerializeField] private bool _yawFollowsHmd;

        [Header("Hand Mapping")]
        [SerializeField] private bool _driveHandTargetsFromControllers = true;
        [SerializeField] private bool _readControllerPoseDirectlyFromXRNodes = true;
        [SerializeField] private XRNode _leftControllerNode = XRNode.LeftHand;
        [SerializeField] private XRNode _rightControllerNode = XRNode.RightHand;
        [SerializeField] private bool _useSceneAuthoredHandRest = true;
        [SerializeField] private bool _snapVisualTargetsToJudgementOnEnable = true;
        [SerializeField] private bool _useControllerStartCalibration = true;
        [SerializeField] private bool _useRelativeControllerRotation = true;
        [SerializeField, Min(0)] private int _calibrationDelayFrames = 2;
        [SerializeField] private float _horizontalScale = 1f;
        [SerializeField] private float _verticalScale = 1.15f;
        [SerializeField] private float _depthScale = 1.05f;
        [SerializeField, Tooltip("Maps controller X through the shared playfield span between both neutral hand poses, so either hand can cross over to either side's notes.")]
        private bool _useCommonPlayfieldHorizontalMapping = true;
        [SerializeField, Min(0.05f)] private float _minimumCommonPlayfieldControllerSpan = 0.12f;
        [SerializeField, Range(1f, 60f)] private float _handTargetSmoothSpeed = 24f;
        [SerializeField, Range(1f, 60f)] private float _visualHandTargetSmoothSpeed = 42f;
        [SerializeField] private Vector3 _leftHandRestLocal = new Vector3(-0.55f, 1.302f, 3.093f);
        [SerializeField] private Vector3 _rightHandRestLocal = new Vector3(0.509f, 1.302f, 3.093f);

        [Header("Debug Hand Workspace Clamp")]
        [SerializeField, Tooltip("Debug-only hard clamp for diagnosing or guarding extreme tracking spikes. Gameplay defaults keep this off so hand reach is governed by mapping scale.")]
        private bool _limitHandWorkspace;
        [SerializeField] private Vector3 _handWorkspacePositiveLocal = new Vector3(0.78f, 0.85f, 0.82f);
        [SerializeField] private Vector3 _handWorkspaceNegativeLocal = new Vector3(0.78f, 0.62f, 0.70f);

        [Header("Hand Workspace Gizmos")]
        [SerializeField] private bool _showHandWorkspaceGizmos = true;
        [SerializeField] private bool _showWorkspaceOnlyWhenSelected;
        [SerializeField, Range(0.02f, 0.5f)] private float _workspaceGizmoFillAlpha = 0.12f;
        [SerializeField] private Color _leftWorkspaceGizmoColor = new Color(0.1f, 0.9f, 1f, 1f);
        [SerializeField] private Color _rightWorkspaceGizmoColor = new Color(1f, 0.35f, 0.9f, 1f);
        [SerializeField] private Color _workspaceRestGizmoColor = Color.white;

        [Header("Hand Rotation")]
        [SerializeField] private bool _driveHandRotationFromControllers = true;
        [SerializeField] private bool _useSceneAuthoredHandRestRotation = true;
        [SerializeField, Range(0f, 1f)] private float _controllerRotationWeight = 0.95f;
        [SerializeField, Range(1f, 60f)] private float _handRotationSmoothSpeed = 36f;
        [SerializeField] private Vector3 _leftHandRestRotationEuler = Vector3.zero;
        [SerializeField] private Vector3 _rightHandRestRotationEuler = Vector3.zero;

        [Header("PC Debug Input")]
        [SerializeField, Tooltip("When XR controller poses are unavailable, drive the same judgement hand targets with keyboard and mouse for Windows editor/build debugging.")]
        private bool _enableKeyboardMouseDebugInput = true;
        [SerializeField, Min(0.05f)] private float _debugKeyboardMoveSpeed = 1.2f;
        [SerializeField, Min(0.0001f)] private float _debugMouseSensitivity = 0.002f;
        [SerializeField, Min(0.0001f)] private float _debugMouseScrollDepthSpeed = 0.0015f;
        [SerializeField] private Vector3 _leftDebugControllerLocal = new Vector3(-0.35f, -0.25f, 0.35f);
        [SerializeField] private Vector3 _rightDebugControllerLocal = new Vector3(0.35f, -0.25f, 0.35f);

        [Header("Optional Hit Anchor Follow")]
        [SerializeField] private bool _driveHitAnchorToRig;
        [SerializeField] private Vector3 _hitAnchorLocal = new Vector3(0f, 0.24f, 0f);

        private bool _warnedMissingLeftTarget;
        private bool _warnedMissingRightTarget;
        private int _calibrationReadyFrame;
        private bool _leftControllerCalibrated;
        private bool _rightControllerCalibrated;
        private Vector3 _leftControllerNeutralLocal;
        private Vector3 _rightControllerNeutralLocal;
        private Quaternion _leftControllerNeutralLocalRotation = Quaternion.identity;
        private Quaternion _rightControllerNeutralLocalRotation = Quaternion.identity;
        private InputDevice _leftInputDevice;
        private InputDevice _rightInputDevice;
        private bool _hasPersonalCalibration;
        private float _runtimeHorizontalScale = 1f;
        private float _runtimeVerticalScale = 1.15f;
        private float _runtimeDepthScale = 1.05f;
        private Vector3 _runtimeHandWorkspacePositiveLocal;
        private Vector3 _runtimeHandWorkspaceNegativeLocal;
        private Vector3 _leftDebugControllerOffset;
        private Vector3 _rightDebugControllerOffset;
        private bool _debugMouseControlsRightHand = true;
        private Vector2 _lastDebugMousePosition;
        private bool _hasDebugMousePosition;
        private int _lastDebugInputFrame = -1;

        public Transform LeftHandTarget => _leftHandTarget;
        public Transform RightHandTarget => _rightHandTarget;
        public Transform LeftVisualHandTarget => _leftVisualHandTarget != null ? _leftVisualHandTarget : _leftHandTarget;
        public Transform RightVisualHandTarget => _rightVisualHandTarget != null ? _rightVisualHandTarget : _rightHandTarget;
        public Transform CharacterRoot => _characterRoot;
        public Transform MantaRoot => _mantaRoot;
        public Animator VisualAnimator => _visualAnimator;
        public float BaseHorizontalScale => _horizontalScale;
        public float BaseVerticalScale => _verticalScale;
        public float BaseDepthScale => _depthScale;
        public bool UsesCommonPlayfieldHorizontalMapping => _useCommonPlayfieldHorizontalMapping;
        public Vector3 BaseHandWorkspacePositiveLocal => _handWorkspacePositiveLocal;
        public Vector3 BaseHandWorkspaceNegativeLocal => _handWorkspaceNegativeLocal;
        public bool HasPersonalCalibration => _hasPersonalCalibration;

        private float EffectiveHorizontalScale => _hasPersonalCalibration ? _runtimeHorizontalScale : _horizontalScale;
        private float EffectiveVerticalScale => _hasPersonalCalibration ? _runtimeVerticalScale : _verticalScale;
        private float EffectiveDepthScale => _hasPersonalCalibration ? _runtimeDepthScale : _depthScale;
        private Vector3 EffectiveHandWorkspacePositiveLocal => _hasPersonalCalibration ? _runtimeHandWorkspacePositiveLocal : _handWorkspacePositiveLocal;
        private Vector3 EffectiveHandWorkspaceNegativeLocal => _hasPersonalCalibration ? _runtimeHandWorkspaceNegativeLocal : _handWorkspaceNegativeLocal;

        private void Reset()
        {
            AutoBindMissingReferences();
        }

        private void Awake()
        {
            AutoBindMissingReferences();
            CaptureSceneAuthoredHandRest();
        }

        private void OnEnable()
        {
            AutoBindMissingReferences();
            CaptureSceneAuthoredHandRest();
            ResetControllerCalibration();
            if (_snapVisualTargetsToJudgementOnEnable) SnapVisualTargetsToJudgementTargets();
            if (_snapOnEnable) SnapToDesiredPose();
        }

        private void LateUpdate()
        {
            AutoBindMissingReferences();

            if (_followHmd)
            {
                FollowHmd();
            }

            if (_driveHandTargetsFromControllers)
            {
                UpdateHandTarget(
                    _leftController,
                    _leftControllerNode,
                    ref _leftInputDevice,
                    _leftHandTarget,
                    _leftHandRestLocal,
                    _leftHandRestRotationEuler,
                    ref _leftControllerCalibrated,
                    ref _leftControllerNeutralLocal,
                    ref _leftControllerNeutralLocalRotation);
                UpdateHandTarget(
                    _rightController,
                    _rightControllerNode,
                    ref _rightInputDevice,
                    _rightHandTarget,
                    _rightHandRestLocal,
                    _rightHandRestRotationEuler,
                    ref _rightControllerCalibrated,
                    ref _rightControllerNeutralLocal,
                    ref _rightControllerNeutralLocalRotation);
            }
            else
            {
                ApplyRestRigLocal(_leftHandTarget, _leftHandRestLocal, _leftHandRestRotationEuler);
                ApplyRestRigLocal(_rightHandTarget, _rightHandRestLocal, _rightHandRestRotationEuler);
            }

            UpdateVisualHandTarget(_leftHandTarget, _leftVisualHandTarget);
            UpdateVisualHandTarget(_rightHandTarget, _rightVisualHandTarget);

            if (_driveHitAnchorToRig && _hitAnchor != null)
            {
                _hitAnchor.SetPositionAndRotation(transform.TransformPoint(_hitAnchorLocal), transform.rotation);
            }
        }

        private void OnDrawGizmos()
        {
            if (!_showHandWorkspaceGizmos || _showWorkspaceOnlyWhenSelected) return;
            DrawHandWorkspaceGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (!_showHandWorkspaceGizmos || !_showWorkspaceOnlyWhenSelected) return;
            DrawHandWorkspaceGizmos();
        }

        [ContextMenu("Recenter Controller Calibration")]
        public void RecenterControllerCalibration()
        {
            AutoBindMissingReferences();
            CaptureSceneAuthoredHandRest();
            ResetControllerCalibration();
            _calibrationReadyFrame = Time.frameCount;
            TryCaptureControllerCalibration(
                _leftController,
                _leftControllerNode,
                ref _leftInputDevice,
                ref _leftControllerCalibrated,
                ref _leftControllerNeutralLocal,
                ref _leftControllerNeutralLocalRotation);
            TryCaptureControllerCalibration(
                _rightController,
                _rightControllerNode,
                ref _rightInputDevice,
                ref _rightControllerCalibrated,
                ref _rightControllerNeutralLocal,
                ref _rightControllerNeutralLocalRotation);
        }

        public void ApplyPersonalCalibration(DVariantBodyCalibrationProfile profile)
        {
            if (!profile.IsValid)
            {
                ClearPersonalCalibration();
                return;
            }

            _runtimeHorizontalScale = Mathf.Clamp(profile.HorizontalScale, 0.1f, 5f);
            _runtimeVerticalScale = Mathf.Clamp(profile.VerticalScale, 0.1f, 5f);
            _runtimeDepthScale = Mathf.Clamp(profile.DepthScale, 0.1f, 5f);
            _runtimeHandWorkspacePositiveLocal = AbsVector(profile.WorkspacePositiveLocal);
            _runtimeHandWorkspaceNegativeLocal = AbsVector(profile.WorkspaceNegativeLocal);
            _hasPersonalCalibration = true;
        }

        public void ApplyControllerCalibrationNeutral(
            Vector3 leftNeutralLocal,
            Quaternion leftNeutralLocalRotation,
            Vector3 rightNeutralLocal,
            Quaternion rightNeutralLocalRotation)
        {
            _leftControllerNeutralLocal = leftNeutralLocal;
            _leftControllerNeutralLocalRotation = leftNeutralLocalRotation;
            _leftControllerCalibrated = true;

            _rightControllerNeutralLocal = rightNeutralLocal;
            _rightControllerNeutralLocalRotation = rightNeutralLocalRotation;
            _rightControllerCalibrated = true;
            _calibrationReadyFrame = Time.frameCount;
        }

        public void ClearPersonalCalibration()
        {
            _hasPersonalCalibration = false;
            _runtimeHorizontalScale = _horizontalScale;
            _runtimeVerticalScale = _verticalScale;
            _runtimeDepthScale = _depthScale;
            _runtimeHandWorkspacePositiveLocal = _handWorkspacePositiveLocal;
            _runtimeHandWorkspaceNegativeLocal = _handWorkspaceNegativeLocal;
        }

        public bool TryGetCurrentControllerLocalPose(
            bool rightHand,
            out Vector3 localPosition,
            out Quaternion localRotation,
            out Quaternion worldRotation)
        {
            AutoBindMissingReferences();

            if (rightHand)
            {
                return TryGetControllerLocalPose(
                    _rightController,
                    _rightControllerNode,
                    ref _rightInputDevice,
                    out localPosition,
                    out localRotation,
                    out worldRotation);
            }

            return TryGetControllerLocalPose(
                _leftController,
                _leftControllerNode,
                ref _leftInputDevice,
                out localPosition,
                out localRotation,
                out worldRotation);
        }

        [ContextMenu("Auto Bind Missing References")]
        public void AutoBindMissingReferences()
        {
            if (_hmd == null && Camera.main != null) _hmd = Camera.main.transform;
            if (_hitAnchor == null)
            {
                GameObject hit = GameObject.Find("HitAnchor");
                if (hit != null) _hitAnchor = hit.transform;
            }

            if (_leftController == null)
            {
                Transform left = FindControllerProxy("LeftControllerProxy");
                if (left == null && !_readControllerPoseDirectlyFromXRNodes)
                {
                    GameObject legacyLeft = GameObject.Find("LeftHand");
                    if (legacyLeft != null) left = legacyLeft.transform;
                }
                if (left != null) _leftController = left;
            }

            if (_rightController == null)
            {
                Transform right = FindControllerProxy("RightControllerProxy");
                if (right == null && !_readControllerPoseDirectlyFromXRNodes)
                {
                    GameObject legacyRight = GameObject.Find("RightHand");
                    if (legacyRight != null) right = legacyRight.transform;
                }
                if (right != null) _rightController = right;
            }

            if (_leftHandTarget == null) _leftHandTarget = FindChildPath("JudgementRig/LeftHandTarget") ?? transform.Find("LeftHandTarget");
            if (_rightHandTarget == null) _rightHandTarget = FindChildPath("JudgementRig/RightHandTarget") ?? transform.Find("RightHandTarget");
            if (_characterRoot == null) _characterRoot = FindChildPath("SeatAnchor/CharacterRoot") ?? transform.Find("CharacterRoot");
            if (_leftVisualHandTarget == null) _leftVisualHandTarget = FindChildPath("SeatAnchor/CharacterRoot/VisualHandTargets/LeftHandTarget");
            if (_rightVisualHandTarget == null) _rightVisualHandTarget = FindChildPath("SeatAnchor/CharacterRoot/VisualHandTargets/RightHandTarget");
            if (_mantaRoot == null) _mantaRoot = transform.Find("SeatAnchor") ?? transform.Find("MantaMountRoot");
            if (_visualAnimator == null) _visualAnimator = GetComponentInChildren<Animator>(true);

            WarnMissingTarget(_leftHandTarget, "LeftHandTarget", ref _warnedMissingLeftTarget);
            WarnMissingTarget(_rightHandTarget, "RightHandTarget", ref _warnedMissingRightTarget);
        }

        [ContextMenu("Snap To Desired Pose")]
        public void SnapToDesiredPose()
        {
            if (_hmd == null) AutoBindMissingReferences();
            if (_hmd == null) return;

            transform.position = DesiredPosition();
            Vector3 forward = FlatForward();
            if (_yawFollowsHmd && forward.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }

        public void BindSceneReferences(
            Transform hmd,
            Transform leftController,
            Transform rightController,
            Transform hitAnchor,
            Transform leftHandTarget,
            Transform rightHandTarget,
            Transform leftVisualHandTarget,
            Transform rightVisualHandTarget,
            Transform characterRoot,
            Transform mantaRoot,
            Animator visualAnimator)
        {
            _hmd = hmd;
            _leftController = leftController;
            _rightController = rightController;
            _hitAnchor = hitAnchor;
            _leftHandTarget = leftHandTarget;
            _rightHandTarget = rightHandTarget;
            _leftVisualHandTarget = leftVisualHandTarget;
            _rightVisualHandTarget = rightVisualHandTarget;
            _characterRoot = characterRoot;
            _mantaRoot = mantaRoot;
            _visualAnimator = visualAnimator;
            CaptureSceneAuthoredHandRest();
        }

        private void FollowHmd()
        {
            if (_hmd == null) return;

            Vector3 desired = DesiredPosition();
            float distance = Vector3.Distance(transform.position, desired);
            if (distance > _lazyFollowThreshold)
            {
                float t = 1f - Mathf.Exp(-_lazyFollowSpeed * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, desired, t);
            }

            Vector3 forward = FlatForward();
            if (_yawFollowsHmd && forward.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }

        private Vector3 DesiredPosition()
        {
            Vector3 forward = FlatForward();
            return _hmd.position + forward * _distanceInFrontOfHmd + Vector3.up * _heightOffsetFromHmd;
        }

        private Vector3 FlatForward()
        {
            Vector3 forward = _hmd != null ? _hmd.forward : transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private void UpdateHandTarget(
            Transform controller,
            XRNode controllerNode,
            ref InputDevice inputDevice,
            Transform target,
            Vector3 restLocal,
            Vector3 restRotationEuler,
            ref bool calibrated,
            ref Vector3 neutralLocal,
            ref Quaternion neutralLocalRotation)
        {
            if (target == null) return;

            if (_hmd == null)
            {
                ApplyRestRigLocal(target, restLocal, restRotationEuler);
                return;
            }

            Vector3 mapped;
            Quaternion controllerDrivenRotation;
            Quaternion restRotation = RestWorldRotation(restRotationEuler);

            if (_useControllerStartCalibration)
            {
                if (!calibrated)
                {
                    TryCaptureControllerCalibration(
                        controller,
                        controllerNode,
                        ref inputDevice,
                        ref calibrated,
                        ref neutralLocal,
                        ref neutralLocalRotation);
                }

                if (!calibrated)
                {
                    ApplyRestRigLocal(target, restLocal, restRotationEuler);
                    return;
                }

                if (!TryGetControllerLocalPose(controller, controllerNode, ref inputDevice, out Vector3 controllerLocal, out Quaternion controllerLocalRotation, out Quaternion controllerWorldRotation))
                {
                    ApplyRestRigLocal(target, restLocal, restRotationEuler);
                    return;
                }

                Vector3 delta = controllerLocal - neutralLocal;

                mapped = restLocal + new Vector3(
                    0f,
                    delta.y * EffectiveVerticalScale,
                    delta.z * EffectiveDepthScale);
                mapped.x = MapHorizontalLocal(controllerLocal.x, neutralLocal.x, restLocal.x);
                mapped = ClampToHandWorkspace(mapped, restLocal);

                Quaternion relativeRotation = controllerLocalRotation * Quaternion.Inverse(neutralLocalRotation);
                controllerDrivenRotation = _useRelativeControllerRotation
                    ? transform.rotation * relativeRotation * Quaternion.Euler(restRotationEuler)
                    : controllerWorldRotation * Quaternion.Euler(restRotationEuler);
            }
            else
            {
                if (!TryGetControllerLocalPose(controller, controllerNode, ref inputDevice, out Vector3 controllerLocal, out _, out Quaternion controllerWorldRotation))
                {
                    ApplyRestRigLocal(target, restLocal, restRotationEuler);
                    return;
                }

                mapped = new Vector3(
                    controllerLocal.x * EffectiveHorizontalScale,
                    controllerLocal.y * EffectiveVerticalScale - 0.15f,
                    controllerLocal.z * EffectiveDepthScale);
                mapped = ClampToHandWorkspace(mapped, restLocal);
                controllerDrivenRotation = controllerWorldRotation * Quaternion.Euler(restRotationEuler);
            }

            Vector3 targetPosition = transform.TransformPoint(mapped);
            Quaternion targetRotation = _driveHandRotationFromControllers
                ? Quaternion.Slerp(restRotation, controllerDrivenRotation, _controllerRotationWeight)
                : restRotation;

            float positionT = 1f - Mathf.Exp(-_handTargetSmoothSpeed * Time.deltaTime);
            float rotationT = 1f - Mathf.Exp(-_handRotationSmoothSpeed * Time.deltaTime);
            target.position = Vector3.Lerp(target.position, targetPosition, positionT);
            target.rotation = Quaternion.Slerp(target.rotation, targetRotation, rotationT);
        }

        private Vector3 ClampToHandWorkspace(Vector3 mappedLocal, Vector3 restLocal)
        {
            if (!_limitHandWorkspace) return mappedLocal;

            Vector3 positive = AbsVector(EffectiveHandWorkspacePositiveLocal);
            Vector3 negative = AbsVector(EffectiveHandWorkspaceNegativeLocal);
            Vector3 offset = mappedLocal - restLocal;
            offset.x = Mathf.Clamp(offset.x, -negative.x, positive.x);
            offset.y = Mathf.Clamp(offset.y, -negative.y, positive.y);
            offset.z = Mathf.Clamp(offset.z, -negative.z, positive.z);
            return restLocal + offset;
        }

        private float MapHorizontalLocal(float controllerLocalX, float ownNeutralLocalX, float ownRestLocalX)
        {
            float direct = ownRestLocalX + (controllerLocalX - ownNeutralLocalX) * EffectiveHorizontalScale;
            if (!_useCommonPlayfieldHorizontalMapping) return direct;
            if (!_leftControllerCalibrated || !_rightControllerCalibrated) return direct;

            float sourceLeft = _leftControllerNeutralLocal.x;
            float sourceRight = _rightControllerNeutralLocal.x;
            float sourceSpan = Mathf.Abs(sourceRight - sourceLeft);
            if (sourceSpan < Mathf.Max(0.05f, _minimumCommonPlayfieldControllerSpan)) return direct;

            float targetLeft = _leftHandRestLocal.x;
            float targetRight = _rightHandRestLocal.x;
            float targetSpan = Mathf.Abs(targetRight - targetLeft);
            if (targetSpan < 0.0001f) return direct;

            bool sourceLeftIsMinimum = sourceLeft <= sourceRight;
            float sourceMin = sourceLeftIsMinimum ? sourceLeft : sourceRight;
            float sourceMax = sourceLeftIsMinimum ? sourceRight : sourceLeft;
            float targetAtMin = sourceLeftIsMinimum ? targetLeft : targetRight;
            float targetAtMax = sourceLeftIsMinimum ? targetRight : targetLeft;

            if (controllerLocalX >= sourceMin && controllerLocalX <= sourceMax)
            {
                float t = Mathf.InverseLerp(sourceMin, sourceMax, controllerLocalX);
                return Mathf.Lerp(targetAtMin, targetAtMax, t);
            }

            float outsideScale = (targetSpan / sourceSpan) * Mathf.Max(0.01f, Mathf.Abs(EffectiveHorizontalScale));
            if (controllerLocalX < sourceMin)
            {
                return targetAtMin + (controllerLocalX - sourceMin) * outsideScale;
            }

            return targetAtMax + (controllerLocalX - sourceMax) * outsideScale;
        }

        private void DrawHandWorkspaceGizmos()
        {
            if (!_limitHandWorkspace) return;

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            DrawHandWorkspaceGizmo(_leftHandRestLocal, _leftWorkspaceGizmoColor);
            DrawHandWorkspaceGizmo(_rightHandRestLocal, _rightWorkspaceGizmoColor);

            Gizmos.matrix = previousMatrix;
        }

        private void DrawHandWorkspaceGizmo(Vector3 restLocal, Color color)
        {
            Vector3 positive = AbsVector(EffectiveHandWorkspacePositiveLocal);
            Vector3 negative = AbsVector(EffectiveHandWorkspaceNegativeLocal);
            Vector3 min = restLocal - negative;
            Vector3 max = restLocal + positive;
            Vector3 center = (min + max) * 0.5f;
            Vector3 size = max - min;

            Gizmos.color = WithAlpha(color, _workspaceGizmoFillAlpha);
            Gizmos.DrawCube(center, size);
            Gizmos.color = WithAlpha(color, 0.9f);
            Gizmos.DrawWireCube(center, size);

            Gizmos.color = WithAlpha(_workspaceRestGizmoColor, 0.95f);
            Gizmos.DrawWireSphere(restLocal, 0.04f);
        }

        private void UpdateVisualHandTarget(Transform judgementTarget, Transform visualTarget)
        {
            if (judgementTarget == null || visualTarget == null) return;

            float t = 1f - Mathf.Exp(-_visualHandTargetSmoothSpeed * Time.deltaTime);
            visualTarget.position = Vector3.Lerp(visualTarget.position, judgementTarget.position, t);
            visualTarget.rotation = Quaternion.Slerp(visualTarget.rotation, judgementTarget.rotation, t);
        }

        [ContextMenu("Capture Scene Authored Hand Rest")]
        public void CaptureSceneAuthoredHandRest()
        {
            if (!_useSceneAuthoredHandRest) return;

            if (_leftHandTarget != null)
            {
                _leftHandRestLocal = transform.InverseTransformPoint(_leftHandTarget.position);
                if (_useSceneAuthoredHandRestRotation) _leftHandRestRotationEuler = RigLocalRotationEuler(_leftHandTarget);
            }

            if (_rightHandTarget != null)
            {
                _rightHandRestLocal = transform.InverseTransformPoint(_rightHandTarget.position);
                if (_useSceneAuthoredHandRestRotation) _rightHandRestRotationEuler = RigLocalRotationEuler(_rightHandTarget);
            }
        }

        [ContextMenu("Snap Visual Targets To Judgement Targets")]
        public void SnapVisualTargetsToJudgementTargets()
        {
            SnapVisualTarget(_leftHandTarget, _leftVisualHandTarget);
            SnapVisualTarget(_rightHandTarget, _rightVisualHandTarget);
        }

        private void ApplyRestRigLocal(Transform target, Vector3 restLocal, Vector3 restRotationEuler)
        {
            if (target == null) return;
            target.SetPositionAndRotation(transform.TransformPoint(restLocal), RestWorldRotation(restRotationEuler));
        }

        private Quaternion RestWorldRotation(Vector3 restRotationEuler)
        {
            return transform.rotation * Quaternion.Euler(restRotationEuler);
        }

        private Vector3 RigLocalRotationEuler(Transform target)
        {
            Quaternion localRotation = Quaternion.Inverse(transform.rotation) * target.rotation;
            return localRotation.eulerAngles;
        }

        private static void SnapVisualTarget(Transform judgementTarget, Transform visualTarget)
        {
            if (judgementTarget == null || visualTarget == null) return;
            visualTarget.SetPositionAndRotation(judgementTarget.position, judgementTarget.rotation);
        }

        private void ResetControllerCalibration()
        {
            _calibrationReadyFrame = Time.frameCount + Mathf.Max(0, _calibrationDelayFrames);
            _leftControllerCalibrated = false;
            _rightControllerCalibrated = false;
            _leftControllerNeutralLocalRotation = Quaternion.identity;
            _rightControllerNeutralLocalRotation = Quaternion.identity;
            ResetKeyboardMouseDebugState();
        }

        private bool TryCaptureControllerCalibration(
            Transform controller,
            XRNode controllerNode,
            ref InputDevice inputDevice,
            ref bool calibrated,
            ref Vector3 neutralLocal,
            ref Quaternion neutralLocalRotation)
        {
            if (!_useControllerStartCalibration || calibrated) return calibrated;
            if (Time.frameCount < _calibrationReadyFrame) return false;
            if (_hmd == null) return false;

            if (!TryGetControllerLocalPose(controller, controllerNode, ref inputDevice, out neutralLocal, out neutralLocalRotation, out _))
            {
                return false;
            }

            calibrated = true;
            return true;
        }

        private bool TryGetControllerLocalPose(
            Transform fallbackController,
            XRNode controllerNode,
            ref InputDevice inputDevice,
            out Vector3 localPosition,
            out Quaternion localRotation,
            out Quaternion worldRotation)
        {
            localPosition = default;
            localRotation = Quaternion.identity;
            worldRotation = Quaternion.identity;

            if (_hmd == null) return false;

            if (_readControllerPoseDirectlyFromXRNodes)
            {
                if (TryGetXrControllerLocalPose(controllerNode, ref inputDevice, out localPosition, out localRotation, out worldRotation))
                {
                    return true;
                }

                return TryGetKeyboardMouseControllerLocalPose(controllerNode, out localPosition, out localRotation, out worldRotation);
            }

            if (fallbackController == null)
            {
                return TryGetKeyboardMouseControllerLocalPose(controllerNode, out localPosition, out localRotation, out worldRotation);
            }

            localPosition = _hmd.InverseTransformPoint(fallbackController.position);
            worldRotation = fallbackController.rotation;
            localRotation = Quaternion.Inverse(_hmd.rotation) * worldRotation;
            return true;
        }

        private bool TryGetXrControllerLocalPose(
            XRNode controllerNode,
            ref InputDevice inputDevice,
            out Vector3 localPosition,
            out Quaternion localRotation,
            out Quaternion worldRotation)
        {
            localPosition = default;
            localRotation = Quaternion.identity;
            worldRotation = Quaternion.identity;

            if (!inputDevice.isValid) inputDevice = InputDevices.GetDeviceAtXRNode(controllerNode);
            if (!inputDevice.isValid) return false;
            if (!inputDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 devicePosition)) return false;

            bool hasRotation = inputDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion deviceRotation);
            if (!hasRotation) deviceRotation = Quaternion.identity;

            Transform trackingRoot = _hmd.parent;
            Vector3 worldPosition = trackingRoot != null ? trackingRoot.TransformPoint(devicePosition) : devicePosition;
            worldRotation = trackingRoot != null ? trackingRoot.rotation * deviceRotation : deviceRotation;
            localPosition = _hmd.InverseTransformPoint(worldPosition);
            localRotation = Quaternion.Inverse(_hmd.rotation) * worldRotation;
            return true;
        }

        private bool TryGetKeyboardMouseControllerLocalPose(
            XRNode controllerNode,
            out Vector3 localPosition,
            out Quaternion localRotation,
            out Quaternion worldRotation)
        {
            localPosition = default;
            localRotation = Quaternion.identity;
            worldRotation = Quaternion.identity;

            if (!_enableKeyboardMouseDebugInput || _hmd == null) return false;
            if (controllerNode != XRNode.LeftHand && controllerNode != XRNode.RightHand) return false;

            UpdateKeyboardMouseDebugInput();

            bool rightHand = controllerNode == XRNode.RightHand;
            Vector3 baseLocal = rightHand ? _rightDebugControllerLocal : _leftDebugControllerLocal;
            Vector3 offset = rightHand ? _rightDebugControllerOffset : _leftDebugControllerOffset;

            localPosition = baseLocal + offset;
            worldRotation = _hmd.rotation;
            localRotation = Quaternion.identity;
            return true;
        }

        private void UpdateKeyboardMouseDebugInput()
        {
            if (_lastDebugInputFrame == Time.frameCount) return;
            _lastDebugInputFrame = Time.frameCount;

            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : Time.deltaTime;
            float keyboardStep = Mathf.Max(0.05f, _debugKeyboardMoveSpeed) * dt;

            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) _debugMouseControlsRightHand = false;
                if (keyboard.digit2Key.wasPressedThisFrame) _debugMouseControlsRightHand = true;
                if (keyboard.tabKey.wasPressedThisFrame) _debugMouseControlsRightHand = !_debugMouseControlsRightHand;
                if (keyboard.rKey.wasPressedThisFrame)
                {
                    _leftDebugControllerOffset = Vector3.zero;
                    _rightDebugControllerOffset = Vector3.zero;
                }

                _leftDebugControllerOffset += new Vector3(
                    KeyAxis(keyboard.aKey, keyboard.dKey),
                    KeyAxis(keyboard.qKey, keyboard.eKey),
                    KeyAxis(keyboard.sKey, keyboard.wKey)) * keyboardStep;

                _rightDebugControllerOffset += new Vector3(
                    KeyAxis(keyboard.jKey, keyboard.lKey),
                    KeyAxis(keyboard.uKey, keyboard.oKey),
                    KeyAxis(keyboard.kKey, keyboard.iKey)) * keyboardStep;
            }

            if (mouse != null)
            {
                Vector2 mousePosition = mouse.position.ReadValue();
                if (_hasDebugMousePosition)
                {
                    Vector2 mouseDelta = mousePosition - _lastDebugMousePosition;
                    float scroll = mouse.scroll.ReadValue().y;
                    Vector3 debugDelta = new Vector3(
                        mouseDelta.x * Mathf.Max(0.0001f, _debugMouseSensitivity),
                        mouseDelta.y * Mathf.Max(0.0001f, _debugMouseSensitivity),
                        scroll * Mathf.Max(0.0001f, _debugMouseScrollDepthSpeed));

                    if (debugDelta.sqrMagnitude > 0f)
                    {
                        bool leftMouse = mouse.leftButton.isPressed && !mouse.rightButton.isPressed;
                        bool rightMouse = mouse.rightButton.isPressed && !mouse.leftButton.isPressed;
                        if (leftMouse)
                        {
                            _leftDebugControllerOffset += debugDelta;
                            _debugMouseControlsRightHand = false;
                        }
                        else if (rightMouse)
                        {
                            _rightDebugControllerOffset += debugDelta;
                            _debugMouseControlsRightHand = true;
                        }
                        else if (_debugMouseControlsRightHand)
                        {
                            _rightDebugControllerOffset += debugDelta;
                        }
                        else
                        {
                            _leftDebugControllerOffset += debugDelta;
                        }
                    }
                }

                _lastDebugMousePosition = mousePosition;
                _hasDebugMousePosition = true;
            }

            _leftDebugControllerOffset = ClampDebugControllerOffset(_leftDebugControllerOffset);
            _rightDebugControllerOffset = ClampDebugControllerOffset(_rightDebugControllerOffset);
        }

        private Vector3 ClampDebugControllerOffset(Vector3 offset)
        {
            if (!_limitHandWorkspace) return offset;

            Vector3 positive = AbsVector(EffectiveHandWorkspacePositiveLocal);
            Vector3 negative = AbsVector(EffectiveHandWorkspaceNegativeLocal);
            float horizontalScale = Mathf.Max(0.0001f, Mathf.Abs(EffectiveHorizontalScale));
            float verticalScale = Mathf.Max(0.0001f, Mathf.Abs(EffectiveVerticalScale));
            float depthScale = Mathf.Max(0.0001f, Mathf.Abs(EffectiveDepthScale));

            offset.x = Mathf.Clamp(offset.x, -negative.x / horizontalScale, positive.x / horizontalScale);
            offset.y = Mathf.Clamp(offset.y, -negative.y / verticalScale, positive.y / verticalScale);
            offset.z = Mathf.Clamp(offset.z, -negative.z / depthScale, positive.z / depthScale);
            return offset;
        }

        private void ResetKeyboardMouseDebugState()
        {
            _leftDebugControllerOffset = Vector3.zero;
            _rightDebugControllerOffset = Vector3.zero;
            _debugMouseControlsRightHand = true;
            _hasDebugMousePosition = false;
            _lastDebugInputFrame = -1;
        }

        private static float KeyAxis(
            UnityEngine.InputSystem.Controls.ButtonControl negative,
            UnityEngine.InputSystem.Controls.ButtonControl positive)
        {
            float value = 0f;
            if (negative != null && negative.isPressed) value -= 1f;
            if (positive != null && positive.isPressed) value += 1f;
            return value;
        }

        private Transform FindControllerProxy(string proxyName)
        {
            Transform hmdParent = _hmd != null ? _hmd.parent : null;
            Transform proxyRoot = hmdParent != null ? hmdParent.Find("ControllerInputProxies") : null;
            return proxyRoot != null ? proxyRoot.Find(proxyName) : null;
        }

        private static Vector3 AbsVector(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private Transform FindChildPath(string path)
        {
            return transform.Find(path);
        }

        private void WarnMissingTarget(Transform target, string childName, ref bool warned)
        {
            if (target != null || warned) return;
            Debug.LogError($"[DVariantRiderRig] Required scene-authored child '{childName}' is missing under '{name}'. Runtime target creation is disabled.", this);
            warned = true;
        }
    }
}
