using System.Collections;
using AO.Core;
using UnityEngine;
using UnityEngine.XR;

namespace AO.Character
{
    [DisallowMultipleComponent]
    public sealed class GameplayBodyCalibrationController : MonoBehaviour
    {
        private const string PrefPrefix = "AO.BodyCalibration.";

        [Header("References")]
        [SerializeField] private DVariantRiderRig _riderRig;
        [SerializeField] private OculusUpperBodyDriver _upperBodyDriver;
        [SerializeField] private Transform _hmd;

        [Header("Flow")]
        [SerializeField] private bool _enabledBeforeSong = true;
        [SerializeField] private bool _runEveryGameplayStart = true;
        [SerializeField] private bool _allowSavedProfileSkip = true;
        [SerializeField] private bool _loadSavedProfile = true;
        [SerializeField] private bool _saveProfile = true;
        [SerializeField, Range(0.5f, 15f)] private float _poseAvailabilityTimeoutSeconds = 5f;
        [SerializeField, Range(0f, 2f)] private float _stepSettleSeconds = 0.2f;

        [Header("Mapping Targets")]
        [SerializeField, Min(0.01f)] private float _minimumMeasuredRange = 0.08f;
        [SerializeField] private Vector2 _scaleClamp = new Vector2(0.6f, 3.2f);
        [SerializeField] private Vector3 _workspacePositiveLocal = new Vector3(0.78f, 0.85f, 0.82f);
        [SerializeField] private Vector3 _workspaceNegativeLocal = new Vector3(0.78f, 0.62f, 0.70f);

        [Header("Runtime Display")]
        [SerializeField] private bool _showOnGui = true;
        [SerializeField] private Rect _guiRect = new Rect(24f, 80f, 560f, 220f);

        private readonly InputDevice[] _devices = new InputDevice[2];
        private bool _isRunning;
        private string _status = string.Empty;
        private string _details = string.Empty;

        public bool EnabledBeforeSong => enabled && _enabledBeforeSong;
        public bool IsRunning => _isRunning;

        private void Awake()
        {
            AutoBindMissingReferences();
        }

        public IEnumerator RunBeforeSong()
        {
            AutoBindMissingReferences();
            if (_riderRig == null)
            {
                Debug.LogWarning("[GameplayBodyCalibrationController] DVariantRiderRig was not found. Gameplay will start without body calibration.", this);
                yield break;
            }

            GameplayRuntimeState.SetInputBlocked(true);
            _isRunning = true;

            DVariantBodyCalibrationProfile savedProfile = default;
            bool hasSavedProfile = _loadSavedProfile && TryLoadProfile(out savedProfile);
            if (!_runEveryGameplayStart && hasSavedProfile)
            {
                ApplyProfileWithCurrentReference(savedProfile);
                Finish("Loaded saved body calibration.");
                yield break;
            }

            if (hasSavedProfile && _allowSavedProfileSkip)
            {
                _status = "Body calibration";
                _details = "Press trigger/Space to recalibrate, or B/Esc to use the saved profile.";
                yield return WaitForRelease();

                while (!ConfirmPressed())
                {
                    if (SkipPressed())
                    {
                        ApplyProfileWithCurrentReference(savedProfile);
                        Finish("Loaded saved body calibration.");
                        yield break;
                    }

                    yield return null;
                }

                yield return WaitForRelease();
            }

            yield return WaitForPoseAvailability();
            if (!CapturePose("Neutral", out PoseSnapshot neutral))
            {
                Finish("Neutral pose was not available. Gameplay will use current rig defaults.");
                yield break;
            }

            yield return CaptureStep(
                "Neutral",
                "Sit or stand comfortably, face forward, put both controllers at your natural center hit position, then press trigger/Space.",
                neutral);

            neutral = CaptureRequiredPose("Neutral");
            if (!neutral.IsValid)
            {
                Finish("Neutral pose capture failed. Gameplay will use current rig defaults.");
                yield break;
            }

            PoseSnapshot up = neutral;
            yield return CaptureStep(
                "Reach up",
                "Raise both hands to the highest comfortable note height, then press trigger/Space.",
                up);
            up = CaptureRequiredPose("Reach up");

            PoseSnapshot outward = neutral;
            yield return CaptureStep(
                "Reach outward",
                "Move left hand left and right hand right to a comfortable outer range, then press trigger/Space.",
                outward);
            outward = CaptureRequiredPose("Reach outward");

            PoseSnapshot forward = neutral;
            yield return CaptureStep(
                "Reach forward",
                "Reach both hands forward to the far comfortable note distance, then press trigger/Space.",
                forward);
            forward = CaptureRequiredPose("Reach forward");

            if (!up.IsValid || !outward.IsValid || !forward.IsValid)
            {
                Finish("One or more calibration poses failed. Gameplay will use current rig defaults.");
                yield break;
            }

            DVariantBodyCalibrationProfile profile = BuildProfile(neutral, up, outward, forward);
            _riderRig.ApplyPersonalCalibration(profile);
            _riderRig.ApplyControllerCalibrationNeutral(
                neutral.LeftLocalPosition,
                neutral.LeftLocalRotation,
                neutral.RightLocalPosition,
                neutral.RightLocalRotation);

            if (_upperBodyDriver != null)
            {
                _upperBodyDriver.ApplyUpperBodyCalibration(neutral.HmdWorldPosition, neutral.HmdWorldRotation);
            }

            if (_saveProfile)
            {
                SaveProfile(profile);
            }

            _status = "Body calibration complete";
            _details = $"Scale X/Y/Z = {profile.HorizontalScale:0.00} / {profile.VerticalScale:0.00} / {profile.DepthScale:0.00}";
            Debug.Log($"[GameplayBodyCalibrationController] Calibration applied. {_details}", this);
            yield return new WaitForSecondsRealtime(0.65f);

            Finish("Body calibration complete.");
        }

        [ContextMenu("Clear Saved Body Calibration")]
        public void ClearSavedProfile()
        {
            PlayerPrefs.DeleteKey(PrefPrefix + "Valid");
            PlayerPrefs.DeleteKey(PrefPrefix + "HorizontalScale");
            PlayerPrefs.DeleteKey(PrefPrefix + "VerticalScale");
            PlayerPrefs.DeleteKey(PrefPrefix + "DepthScale");
            PlayerPrefs.DeleteKey(PrefPrefix + "WorkspacePositiveX");
            PlayerPrefs.DeleteKey(PrefPrefix + "WorkspacePositiveY");
            PlayerPrefs.DeleteKey(PrefPrefix + "WorkspacePositiveZ");
            PlayerPrefs.DeleteKey(PrefPrefix + "WorkspaceNegativeX");
            PlayerPrefs.DeleteKey(PrefPrefix + "WorkspaceNegativeY");
            PlayerPrefs.DeleteKey(PrefPrefix + "WorkspaceNegativeZ");
            PlayerPrefs.Save();
        }

        private IEnumerator CaptureStep(string title, string detail, PoseSnapshot previous)
        {
            _status = title;
            _details = detail;
            yield return WaitForRelease();
            while (!ConfirmPressed())
            {
                if (SkipPressed())
                {
                    _details = "Skip is only available before recalibration starts.";
                }

                yield return null;
            }

            if (_stepSettleSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(_stepSettleSeconds);
            }
        }

        private IEnumerator WaitForPoseAvailability()
        {
            float deadline = Time.unscaledTime + _poseAvailabilityTimeoutSeconds;
            while (Time.unscaledTime < deadline)
            {
                if (CapturePose("Availability", out _))
                {
                    yield break;
                }

                _status = "Body calibration";
                _details = "Waiting for HMD/controller pose...";
                yield return null;
            }
        }

        private PoseSnapshot CaptureRequiredPose(string label)
        {
            if (CapturePose(label, out PoseSnapshot snapshot))
            {
                return snapshot;
            }

            Debug.LogWarning($"[GameplayBodyCalibrationController] Failed to capture '{label}'.", this);
            return PoseSnapshot.Invalid;
        }

        private bool CapturePose(string label, out PoseSnapshot snapshot)
        {
            snapshot = PoseSnapshot.Invalid;
            AutoBindMissingReferences();

            if (_riderRig == null) return false;
            bool left = _riderRig.TryGetCurrentControllerLocalPose(false, out Vector3 leftPosition, out Quaternion leftRotation, out _);
            bool right = _riderRig.TryGetCurrentControllerLocalPose(true, out Vector3 rightPosition, out Quaternion rightRotation, out _);
            if (!left || !right) return false;

            Vector3 hmdPosition = _hmd != null ? _hmd.position : Vector3.zero;
            Quaternion hmdRotation = _hmd != null ? _hmd.rotation : Quaternion.identity;

            snapshot = new PoseSnapshot
            {
                IsValid = true,
                Label = label,
                LeftLocalPosition = leftPosition,
                RightLocalPosition = rightPosition,
                LeftLocalRotation = leftRotation,
                RightLocalRotation = rightRotation,
                HmdWorldPosition = hmdPosition,
                HmdWorldRotation = hmdRotation
            };

            return true;
        }

        private DVariantBodyCalibrationProfile BuildProfile(
            PoseSnapshot neutral,
            PoseSnapshot up,
            PoseSnapshot outward,
            PoseSnapshot forward)
        {
            DVariantBodyCalibrationProfile defaults = DVariantBodyCalibrationProfile.FromRigDefaults(_riderRig);
            Vector3 positive = _workspacePositiveLocal.sqrMagnitude > 0.0001f
                ? AbsVector(_workspacePositiveLocal)
                : AbsVector(defaults.WorkspacePositiveLocal);
            Vector3 negative = _workspaceNegativeLocal.sqrMagnitude > 0.0001f
                ? AbsVector(_workspaceNegativeLocal)
                : AbsVector(defaults.WorkspaceNegativeLocal);

            float measuredUp = Mathf.Max(
                up.LeftLocalPosition.y - neutral.LeftLocalPosition.y,
                up.RightLocalPosition.y - neutral.RightLocalPosition.y);
            float measuredHorizontal = Mathf.Max(
                neutral.LeftLocalPosition.x - outward.LeftLocalPosition.x,
                outward.RightLocalPosition.x - neutral.RightLocalPosition.x);
            float measuredForward = Mathf.Max(
                forward.LeftLocalPosition.z - neutral.LeftLocalPosition.z,
                forward.RightLocalPosition.z - neutral.RightLocalPosition.z);

            float min = Mathf.Min(_scaleClamp.x, _scaleClamp.y);
            float max = Mathf.Max(_scaleClamp.x, _scaleClamp.y);

            return new DVariantBodyCalibrationProfile
            {
                IsValid = true,
                HorizontalScale = ScaleForRange(measuredHorizontal, positive.x, defaults.HorizontalScale, min, max),
                VerticalScale = ScaleForRange(measuredUp, positive.y, defaults.VerticalScale, min, max),
                DepthScale = ScaleForRange(measuredForward, positive.z, defaults.DepthScale, min, max),
                WorkspacePositiveLocal = positive,
                WorkspaceNegativeLocal = negative
            };
        }

        private float ScaleForRange(float measuredRange, float targetRange, float fallback, float min, float max)
        {
            if (measuredRange < _minimumMeasuredRange || targetRange <= 0f)
            {
                return Mathf.Clamp(fallback, min, max);
            }

            return Mathf.Clamp(targetRange / measuredRange, min, max);
        }

        private void ApplyProfileWithCurrentReference(DVariantBodyCalibrationProfile profile)
        {
            _riderRig.ApplyPersonalCalibration(profile);
            _riderRig.RecenterControllerCalibration();

            if (_upperBodyDriver != null)
            {
                _upperBodyDriver.CaptureCurrentHmdAsUpperBodyReference();
            }
        }

        private void Finish(string message)
        {
            _status = message;
            _details = string.Empty;
            _isRunning = false;
            GameplayRuntimeState.SetInputBlocked(false);
        }

        private IEnumerator WaitForRelease()
        {
            while (ConfirmPressed() || SkipPressed())
            {
                yield return null;
            }
        }

        private bool ConfirmPressed()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.isPressed || keyboard.enterKey.isPressed))
            {
                return true;
            }

            return DeviceButton(XRNode.LeftHand, CommonUsages.triggerButton)
                || DeviceButton(XRNode.RightHand, CommonUsages.triggerButton)
                || DeviceButton(XRNode.LeftHand, CommonUsages.primaryButton)
                || DeviceButton(XRNode.RightHand, CommonUsages.primaryButton);
        }

        private bool SkipPressed()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.isPressed)
            {
                return true;
            }

            return DeviceButton(XRNode.LeftHand, CommonUsages.secondaryButton)
                || DeviceButton(XRNode.RightHand, CommonUsages.secondaryButton);
        }

        private bool DeviceButton(XRNode node, InputFeatureUsage<bool> usage)
        {
            int index = node == XRNode.RightHand ? 1 : 0;
            if (!_devices[index].isValid) _devices[index] = InputDevices.GetDeviceAtXRNode(node);
            return _devices[index].isValid
                && _devices[index].TryGetFeatureValue(usage, out bool pressed)
                && pressed;
        }

        private void AutoBindMissingReferences()
        {
            if (_riderRig == null) _riderRig = FindFirstObjectByType<DVariantRiderRig>(FindObjectsInactive.Include);
            if (_upperBodyDriver == null) _upperBodyDriver = FindFirstObjectByType<OculusUpperBodyDriver>(FindObjectsInactive.Include);
            if (_hmd == null && Camera.main != null) _hmd = Camera.main.transform;

            if (_riderRig != null)
            {
                _workspacePositiveLocal = AbsVector(_riderRig.BaseHandWorkspacePositiveLocal);
                _workspaceNegativeLocal = AbsVector(_riderRig.BaseHandWorkspaceNegativeLocal);
            }
        }

        private void SaveProfile(DVariantBodyCalibrationProfile profile)
        {
            if (!profile.IsValid) return;

            PlayerPrefs.SetInt(PrefPrefix + "Valid", 1);
            PlayerPrefs.SetFloat(PrefPrefix + "HorizontalScale", profile.HorizontalScale);
            PlayerPrefs.SetFloat(PrefPrefix + "VerticalScale", profile.VerticalScale);
            PlayerPrefs.SetFloat(PrefPrefix + "DepthScale", profile.DepthScale);
            PlayerPrefs.SetFloat(PrefPrefix + "WorkspacePositiveX", profile.WorkspacePositiveLocal.x);
            PlayerPrefs.SetFloat(PrefPrefix + "WorkspacePositiveY", profile.WorkspacePositiveLocal.y);
            PlayerPrefs.SetFloat(PrefPrefix + "WorkspacePositiveZ", profile.WorkspacePositiveLocal.z);
            PlayerPrefs.SetFloat(PrefPrefix + "WorkspaceNegativeX", profile.WorkspaceNegativeLocal.x);
            PlayerPrefs.SetFloat(PrefPrefix + "WorkspaceNegativeY", profile.WorkspaceNegativeLocal.y);
            PlayerPrefs.SetFloat(PrefPrefix + "WorkspaceNegativeZ", profile.WorkspaceNegativeLocal.z);
            PlayerPrefs.Save();
        }

        private bool TryLoadProfile(out DVariantBodyCalibrationProfile profile)
        {
            profile = default;
            if (PlayerPrefs.GetInt(PrefPrefix + "Valid", 0) == 0) return false;

            profile = new DVariantBodyCalibrationProfile
            {
                IsValid = true,
                HorizontalScale = PlayerPrefs.GetFloat(PrefPrefix + "HorizontalScale", _riderRig != null ? _riderRig.BaseHorizontalScale : 1f),
                VerticalScale = PlayerPrefs.GetFloat(PrefPrefix + "VerticalScale", _riderRig != null ? _riderRig.BaseVerticalScale : 1f),
                DepthScale = PlayerPrefs.GetFloat(PrefPrefix + "DepthScale", _riderRig != null ? _riderRig.BaseDepthScale : 1f),
                WorkspacePositiveLocal = new Vector3(
                    PlayerPrefs.GetFloat(PrefPrefix + "WorkspacePositiveX", _workspacePositiveLocal.x),
                    PlayerPrefs.GetFloat(PrefPrefix + "WorkspacePositiveY", _workspacePositiveLocal.y),
                    PlayerPrefs.GetFloat(PrefPrefix + "WorkspacePositiveZ", _workspacePositiveLocal.z)),
                WorkspaceNegativeLocal = new Vector3(
                    PlayerPrefs.GetFloat(PrefPrefix + "WorkspaceNegativeX", _workspaceNegativeLocal.x),
                    PlayerPrefs.GetFloat(PrefPrefix + "WorkspaceNegativeY", _workspaceNegativeLocal.y),
                    PlayerPrefs.GetFloat(PrefPrefix + "WorkspaceNegativeZ", _workspaceNegativeLocal.z))
            };

            return profile.IsValid;
        }

        private void OnGUI()
        {
            if (!_showOnGui || !_isRunning) return;

            GUILayout.BeginArea(_guiRect, GUI.skin.box);
            GUILayout.Label(_status, GUI.skin.label);
            GUILayout.Space(8f);
            GUILayout.Label(_details, GUI.skin.label);
            GUILayout.Space(10f);
            GUILayout.Label("Confirm: trigger / A / Space / Enter", GUI.skin.label);
            GUILayout.Label("Skip saved profile prompt: B / Esc", GUI.skin.label);
            GUILayout.EndArea();
        }

        private static Vector3 AbsVector(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private struct PoseSnapshot
        {
            public bool IsValid;
            public string Label;
            public Vector3 LeftLocalPosition;
            public Vector3 RightLocalPosition;
            public Quaternion LeftLocalRotation;
            public Quaternion RightLocalRotation;
            public Vector3 HmdWorldPosition;
            public Quaternion HmdWorldRotation;

            public static PoseSnapshot Invalid => new PoseSnapshot
            {
                IsValid = false,
                Label = "Invalid",
                LeftLocalRotation = Quaternion.identity,
                RightLocalRotation = Quaternion.identity,
                HmdWorldRotation = Quaternion.identity
            };
        }
    }
}
