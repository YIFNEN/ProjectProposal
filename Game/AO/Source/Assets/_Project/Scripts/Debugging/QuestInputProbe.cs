using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

using InputSystemApi = UnityEngine.InputSystem.InputSystem;
using InputSystemDevice = UnityEngine.InputSystem.InputDevice;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;

namespace AO.Debugging
{
    [DisallowMultipleComponent]
    public sealed class QuestInputProbe : MonoBehaviour
    {
        private const int DefaultCapacity = 4096;

        [Header("Runtime Display")]
        [SerializeField] private bool _showOnGui = true;
        [SerializeField, Min(0.05f)] private float _guiRefreshSeconds = 0.2f;
        [SerializeField] private Vector2 _guiPosition = new Vector2(24f, 24f);
        [SerializeField] private Vector2 _guiSize = new Vector2(760f, 640f);
        [SerializeField] private int _guiFontSize = 14;

        [Header("Logging")]
        [SerializeField] private bool _logOnEnable = true;
        [SerializeField] private bool _logPeriodically;
        [SerializeField, Min(0.2f)] private float _logIntervalSeconds = 2f;
        [SerializeField] private bool _includeEnumeratedXrFeatures = true;
        [SerializeField, Range(8, 128)] private int _maxEnumeratedXrFeaturesPerDevice = 64;

        [Header("Sources")]
        [SerializeField] private bool _readLegacyXrDevices = true;
        [SerializeField] private bool _readInputSystemDevices = true;

        private readonly List<InputFeatureUsage> _featureUsages = new();
        private XRInputDevice _headDevice;
        private XRInputDevice _leftDevice;
        private XRInputDevice _rightDevice;
        private GUIStyle _guiStyle;
        private string _cachedGuiText = string.Empty;
        private float _nextGuiRefreshTime;
        private float _nextLogTime;

        private void OnEnable()
        {
            RefreshXrDevices();
            _cachedGuiText = BuildSnapshot(false);
            _nextGuiRefreshTime = 0f;
            _nextLogTime = Time.unscaledTime + Mathf.Max(0.2f, _logIntervalSeconds);

            if (_logOnEnable)
            {
                Debug.Log(BuildSnapshot(_includeEnumeratedXrFeatures), this);
            }
        }

        private void Update()
        {
            RefreshXrDevices();

            if (_logPeriodically && Time.unscaledTime >= _nextLogTime)
            {
                _nextLogTime = Time.unscaledTime + Mathf.Max(0.2f, _logIntervalSeconds);
                Debug.Log(BuildSnapshot(_includeEnumeratedXrFeatures), this);
            }
        }

        private void OnGUI()
        {
            if (!_showOnGui) return;

            if (_guiStyle == null)
            {
                _guiStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(10, _guiFontSize),
                    wordWrap = false,
                    richText = false,
                    normal = { textColor = Color.white }
                };
            }

            if (Time.unscaledTime >= _nextGuiRefreshTime)
            {
                _nextGuiRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, _guiRefreshSeconds);
                _cachedGuiText = BuildSnapshot(false);
            }

            Rect rect = new Rect(_guiPosition, _guiSize);
            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.68f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = previousColor;
            GUI.Label(new Rect(rect.x + 12f, rect.y + 10f, rect.width - 24f, rect.height - 20f), _cachedGuiText, _guiStyle);
        }

        [ContextMenu("Log Current Snapshot")]
        public void LogCurrentSnapshot()
        {
            RefreshXrDevices();
            Debug.Log(BuildSnapshot(true), this);
        }

        private void RefreshXrDevices()
        {
            if (!_readLegacyXrDevices) return;

            if (!_headDevice.isValid) _headDevice = XRInputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (!_leftDevice.isValid) _leftDevice = XRInputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (!_rightDevice.isValid) _rightDevice = XRInputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        private string BuildSnapshot(bool includeEnumeratedFeatures)
        {
            StringBuilder builder = new StringBuilder(DefaultCapacity);
            builder
                .Append("[QuestInputProbe] frame=").Append(Time.frameCount)
                .Append(", t=").Append(Time.unscaledTime.ToString("0.00"))
                .Append('\n');

            if (_readLegacyXrDevices)
            {
                AppendXrDevice(builder, "HMD CenterEye", XRNode.CenterEye, ref _headDevice, includeEnumeratedFeatures);
                AppendXrDevice(builder, "Left Controller", XRNode.LeftHand, ref _leftDevice, includeEnumeratedFeatures);
                AppendXrDevice(builder, "Right Controller", XRNode.RightHand, ref _rightDevice, includeEnumeratedFeatures);
            }

            if (_readInputSystemDevices)
            {
                AppendInputSystemDevices(builder);
            }

            builder.Append("Use: grip/device pose for hand IK, pointer pose for UI rays, trackingState for validity gates.");
            return builder.ToString();
        }

        private void AppendXrDevice(
            StringBuilder builder,
            string label,
            XRNode node,
            ref XRInputDevice device,
            bool includeEnumeratedFeatures)
        {
            if (!device.isValid) device = XRInputDevices.GetDeviceAtXRNode(node);

            builder.Append('\n').Append(label).Append(" [Legacy XR]").Append('\n');
            if (!device.isValid)
            {
                builder.Append("  <not valid>").Append('\n');
                return;
            }

            builder
                .Append("  name=").Append(device.name)
                .Append(", manufacturer=").Append(device.manufacturer)
                .Append(", characteristics=").Append(device.characteristics)
                .Append('\n');

            AppendXrBool(builder, device, "isTracked");
            AppendXrTrackingState(builder, device, "trackingState");
            AppendXrVector3(builder, device, "devicePosition");
            AppendXrQuaternion(builder, device, "deviceRotation");
            AppendXrVector3(builder, device, "deviceVelocity");
            AppendXrVector3(builder, device, "deviceAngularVelocity");
            AppendXrFloat(builder, device, "trigger");
            AppendXrBool(builder, device, "triggerButton");
            AppendXrFloat(builder, device, "grip");
            AppendXrBool(builder, device, "gripButton");
            AppendXrVector2(builder, device, "primary2DAxis");

            if (includeEnumeratedFeatures)
            {
                AppendEnumeratedXrFeatures(builder, device);
            }
        }

        private void AppendEnumeratedXrFeatures(StringBuilder builder, XRInputDevice device)
        {
            _featureUsages.Clear();
            if (!device.TryGetFeatureUsages(_featureUsages) || _featureUsages.Count == 0)
            {
                builder.Append("  features: <none>").Append('\n');
                return;
            }

            builder.Append("  features:").Append('\n');
            int count = Mathf.Min(_featureUsages.Count, _maxEnumeratedXrFeaturesPerDevice);
            for (int i = 0; i < count; i++)
            {
                InputFeatureUsage usage = _featureUsages[i];
                builder
                    .Append("    ")
                    .Append(usage.name)
                    .Append(" : ")
                    .Append(usage.type.Name)
                    .Append('\n');
            }

            if (_featureUsages.Count > count)
            {
                builder.Append("    ... ").Append(_featureUsages.Count - count).Append(" more").Append('\n');
            }
        }

        private static void AppendXrBool(StringBuilder builder, XRInputDevice device, string name)
        {
            if (device.TryGetFeatureValue(new InputFeatureUsage<bool>(name), out bool value))
            {
                builder.Append("  ").Append(name).Append('=').Append(value).Append('\n');
            }
        }

        private static void AppendXrFloat(StringBuilder builder, XRInputDevice device, string name)
        {
            if (device.TryGetFeatureValue(new InputFeatureUsage<float>(name), out float value))
            {
                builder.Append("  ").Append(name).Append('=').Append(value.ToString("0.###")).Append('\n');
            }
        }

        private static void AppendXrVector2(StringBuilder builder, XRInputDevice device, string name)
        {
            if (device.TryGetFeatureValue(new InputFeatureUsage<Vector2>(name), out Vector2 value))
            {
                builder.Append("  ").Append(name).Append('=').Append(Format(value)).Append('\n');
            }
        }

        private static void AppendXrVector3(StringBuilder builder, XRInputDevice device, string name)
        {
            if (device.TryGetFeatureValue(new InputFeatureUsage<Vector3>(name), out Vector3 value))
            {
                builder.Append("  ").Append(name).Append('=').Append(Format(value)).Append('\n');
            }
        }

        private static void AppendXrQuaternion(StringBuilder builder, XRInputDevice device, string name)
        {
            if (device.TryGetFeatureValue(new InputFeatureUsage<Quaternion>(name), out Quaternion value))
            {
                builder
                    .Append("  ").Append(name)
                    .Append(" euler=").Append(Format(value.eulerAngles))
                    .Append('\n');
            }
        }

        private static void AppendXrTrackingState(StringBuilder builder, XRInputDevice device, string name)
        {
            if (device.TryGetFeatureValue(new InputFeatureUsage<InputTrackingState>(name), out InputTrackingState value))
            {
                builder.Append("  ").Append(name).Append('=').Append(value).Append('\n');
                return;
            }

            if (device.TryGetFeatureValue(new InputFeatureUsage<uint>(name), out uint rawValue))
            {
                builder.Append("  ").Append(name).Append(" raw=").Append(rawValue).Append('\n');
            }
        }

        private void AppendInputSystemDevices(StringBuilder builder)
        {
            builder.Append('\n').Append("Input System tracked/Quest devices").Append('\n');

            int shown = 0;
            foreach (InputSystemDevice device in InputSystemApi.devices)
            {
                if (!ShouldDisplayInputSystemDevice(device)) continue;
                shown++;
                AppendInputSystemDevice(builder, device);
            }

            if (shown == 0)
            {
                builder.Append("  <none>").Append('\n');
            }
        }

        private static bool ShouldDisplayInputSystemDevice(InputSystemDevice device)
        {
            if (device == null) return false;
            if (HasAnyChildControl(device, "isTracked", "trackingState", "devicePosition", "deviceRotation", "pointerPosition", "pointerRotation")) return true;

            string haystack = string.Concat(
                device.layout,
                " ",
                device.displayName,
                " ",
                device.description.product,
                " ",
                device.description.manufacturer);

            return haystack.IndexOf("Quest", System.StringComparison.OrdinalIgnoreCase) >= 0
                || haystack.IndexOf("Touch", System.StringComparison.OrdinalIgnoreCase) >= 0
                || haystack.IndexOf("Oculus", System.StringComparison.OrdinalIgnoreCase) >= 0
                || HasUsage(device, "LeftHand")
                || HasUsage(device, "RightHand");
        }

        private static bool HasAnyChildControl(InputSystemDevice device, params string[] controlNames)
        {
            for (int i = 0; i < controlNames.Length; i++)
            {
                if (device.TryGetChildControl<UnityEngine.InputSystem.InputControl>(controlNames[i]) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AppendInputSystemDevice(StringBuilder builder, InputSystemDevice device)
        {
            builder
                .Append("  ")
                .Append(device.displayName)
                .Append(" layout=").Append(device.layout)
                .Append(", usages=").Append(FormatUsages(device))
                .Append('\n');

            AppendInputBool(builder, device, "isTracked");
            AppendInputInteger(builder, device, "trackingState");
            AppendInputVector3(builder, device, "devicePosition");
            AppendInputQuaternion(builder, device, "deviceRotation");
            AppendInputVector3(builder, device, "pointerPosition");
            AppendInputQuaternion(builder, device, "pointerRotation");
            AppendInputVector2(builder, device, "thumbstick");
            AppendInputAxis(builder, device, "trigger");
            AppendInputBool(builder, device, "triggerPressed");
            AppendInputBool(builder, device, "triggerTouched");
            AppendInputAxis(builder, device, "grip");
            AppendInputBool(builder, device, "gripPressed");
            AppendInputAxis(builder, device, "triggerForce");
            AppendInputAxis(builder, device, "triggerCurl");
            AppendInputAxis(builder, device, "triggerSlide");
            AppendInputBool(builder, device, "triggerProximity");
            AppendInputBool(builder, device, "thumbProximity");
            AppendInputBool(builder, device, "thumbrestTouched");
            AppendInputBool(builder, device, "thumbstickTouched");
            AppendInputBool(builder, device, "primaryTouched");
            AppendInputBool(builder, device, "secondaryTouched");
        }

        private static void AppendInputBool(StringBuilder builder, InputSystemDevice device, string controlName)
        {
            ButtonControl control = device.TryGetChildControl<ButtonControl>(controlName);
            if (control != null) builder.Append("    ").Append(controlName).Append('=').Append(control.isPressed).Append('\n');
        }

        private static void AppendInputAxis(StringBuilder builder, InputSystemDevice device, string controlName)
        {
            AxisControl control = device.TryGetChildControl<AxisControl>(controlName);
            if (control != null) builder.Append("    ").Append(controlName).Append('=').Append(control.ReadValue().ToString("0.###")).Append('\n');
        }

        private static void AppendInputInteger(StringBuilder builder, InputSystemDevice device, string controlName)
        {
            IntegerControl control = device.TryGetChildControl<IntegerControl>(controlName);
            if (control != null) builder.Append("    ").Append(controlName).Append('=').Append(control.ReadValue()).Append('\n');
        }

        private static void AppendInputVector2(StringBuilder builder, InputSystemDevice device, string controlName)
        {
            Vector2Control control = device.TryGetChildControl<Vector2Control>(controlName);
            if (control != null) builder.Append("    ").Append(controlName).Append('=').Append(Format(control.ReadValue())).Append('\n');
        }

        private static void AppendInputVector3(StringBuilder builder, InputSystemDevice device, string controlName)
        {
            Vector3Control control = device.TryGetChildControl<Vector3Control>(controlName);
            if (control != null) builder.Append("    ").Append(controlName).Append('=').Append(Format(control.ReadValue())).Append('\n');
        }

        private static void AppendInputQuaternion(StringBuilder builder, InputSystemDevice device, string controlName)
        {
            QuaternionControl control = device.TryGetChildControl<QuaternionControl>(controlName);
            if (control != null)
            {
                builder
                    .Append("    ")
                    .Append(controlName)
                    .Append(" euler=")
                    .Append(Format(control.ReadValue().eulerAngles))
                    .Append('\n');
            }
        }

        private static bool HasUsage(InputSystemDevice device, string usage)
        {
            foreach (UnityEngine.InputSystem.Utilities.InternedString item in device.usages)
            {
                if (item.ToString() == usage) return true;
            }

            return false;
        }

        private static string FormatUsages(InputSystemDevice device)
        {
            StringBuilder builder = new StringBuilder();
            foreach (UnityEngine.InputSystem.Utilities.InternedString item in device.usages)
            {
                if (builder.Length > 0) builder.Append(',');
                builder.Append(item.ToString());
            }

            return builder.Length > 0 ? builder.ToString() : "<none>";
        }

        private static string Format(Vector2 value)
        {
            return $"({value.x:0.###}, {value.y:0.###})";
        }

        private static string Format(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }
    }
}
