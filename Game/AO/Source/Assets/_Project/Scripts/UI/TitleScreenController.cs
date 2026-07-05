using AO.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRNode = UnityEngine.XR.XRNode;

namespace AO.UI
{
    public class TitleScreenController : MonoBehaviour
    {
        [SerializeField] private bool _preferSceneLayout = true;
        [SerializeField] private bool _preferSceneVisuals = true;
        [SerializeField] private bool _allowRuntimeUiCreation = false;

        private bool _built;
        private bool _wasControllerPressed;

        private void Start()
        {
            MenuSceneWorld.EnsureCommonBackdrop(MenuSceneCharacterMode.None);
            Build();
        }

        private void Update()
        {
            if (Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
            {
                SceneTransition.GoToLobby();
                return;
            }

            bool controllerPressed = IsControllerStartPressed(XRNode.LeftHand) || IsControllerStartPressed(XRNode.RightHand);
            if (controllerPressed && !_wasControllerPressed)
            {
                SceneTransition.GoToLobby();
            }

            _wasControllerPressed = controllerPressed;
        }

        public void RebuildForEditor()
        {
            if (!_allowRuntimeUiCreation)
            {
                Debug.LogError("[TitleScreenController] RebuildForEditor is disabled because runtime UI creation is off. Edit the Title scene objects directly.", this);
                return;
            }

            _built = false;
            RuntimeUiFactory.ClearChildren(transform);
            Build();
        }

        private void Build()
        {
            if (_built) return;
            _built = true;

            TMP_Text title = GetOptionalText(transform, "Title", "AO", new Vector2(0f, 128f), new Vector2(620f, 132f), 96f, TextAlignmentOptions.Center);
            if (title != null) PlaceIfGenerated(title.rectTransform, transform, "Title", new Vector2(0f, 128f), new Vector2(620f, 132f));

            TMP_Text subtitle = GetOptionalText(transform, "Subtitle", "VR Rhythm Action", new Vector2(0f, 42f), new Vector2(620f, 54f), 34f, TextAlignmentOptions.Center);
            if (subtitle != null) PlaceIfGenerated(subtitle.rectTransform, transform, "Subtitle", new Vector2(0f, 42f), new Vector2(620f, 54f));

            Button start = GetButton(transform, "StartButton", "START", new Vector2(0f, -72f), new Vector2(280f, 78f), SceneTransition.GoToLobby);
            if (start != null) PlaceIfGenerated((RectTransform)start.transform, transform, "StartButton", new Vector2(0f, -72f), new Vector2(280f, 78f));
        }

        private Button GetButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform rect = FindRect(parent, name);
            if (rect == null)
            {
                if (!_allowRuntimeUiCreation)
                {
                    ReportMissing("Button", parent, name);
                    return null;
                }

                return RuntimeUiFactory.EnsureButton(parent, name, label, anchoredPosition, size, onClick);
            }

            Button button = rect.GetComponent<Button>();
            if (button == null)
            {
                if (!_allowRuntimeUiCreation)
                {
                    ReportMissing("Button", parent, name);
                    return null;
                }

                button = rect.gameObject.AddComponent<Button>();
            }

            button.onClick.RemoveAllListeners();
            if (onClick != null) button.onClick.AddListener(onClick);

            TMP_Text labelText = rect.Find("Label") != null ? rect.Find("Label").GetComponent<TMP_Text>() : null;
            if (labelText != null) labelText.text = label;
            return button;
        }

        private TMP_Text GetOptionalText(Transform parent, string name, string value, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            RectTransform rect = FindRect(parent, name);
            if (rect == null) return null;

            TMP_Text text = rect.GetComponent<TMP_Text>();
            if (text == null) text = rect.GetComponentInChildren<TMP_Text>(true);
            if (text == null) return null;

            text.text = value;
            if (!_preferSceneVisuals)
            {
                RuntimeUiFactory.ApplyPreferredFont(text);
                text.fontSize = fontSize;
                text.enableAutoSizing = true;
                text.fontSizeMin = Mathf.Max(8f, fontSize * 0.55f);
                text.fontSizeMax = fontSize;
                text.alignment = alignment;
            }

            return text;
        }

        private TMP_Text GetText(Transform parent, string name, string value, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            RectTransform rect = FindRect(parent, name);
            if (rect == null)
            {
                if (!_allowRuntimeUiCreation)
                {
                    ReportMissing("TMP_Text", parent, name);
                    return null;
                }

                return RuntimeUiFactory.EnsureText(parent, name, value, anchoredPosition, size, fontSize, alignment);
            }

            TMP_Text text = rect.GetComponent<TMP_Text>();
            if (text == null) text = rect.GetComponentInChildren<TMP_Text>(true);
            if (text == null)
            {
                if (!_allowRuntimeUiCreation)
                {
                    ReportMissing("TMP_Text", parent, name);
                    return null;
                }

                text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            }

            text.text = value;
            if (!_preferSceneVisuals)
            {
                RuntimeUiFactory.ApplyPreferredFont(text);
                text.fontSize = fontSize;
                text.enableAutoSizing = true;
                text.fontSizeMin = Mathf.Max(8f, fontSize * 0.55f);
                text.fontSizeMax = fontSize;
                text.alignment = alignment;
            }

            return text;
        }

        private void PlaceIfGenerated(RectTransform rect, Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            if (_preferSceneLayout && FindRect(parent, name) != null) return;
            Place(rect, anchoredPosition, size);
        }

        private static RectTransform FindRect(Transform parent, string name)
        {
            Transform child = FindRecursive(parent, name);
            return child != null ? child.GetComponent<RectTransform>() : null;
        }

        private static Transform FindRecursive(Transform parent, string name)
        {
            if (parent == null) return null;

            Transform direct = parent.Find(name);
            if (direct != null) return direct;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindRecursive(parent.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        private void ReportMissing(string expectedType, Transform parent, string name)
        {
            string parentName = parent != null ? parent.name : "<null>";
            Debug.LogError($"[TitleScreenController] Required Title scene object '{name}' ({expectedType}) under '{parentName}' is missing or misconfigured. Runtime UI creation is disabled, so no replacement was generated.", this);
        }

        private static void Place(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            if (rect == null) return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private static bool IsControllerStartPressed(XRNode node)
        {
            XRInputDevice device = XRInputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid) return false;

            if (device.TryGetFeatureValue(XRCommonUsages.triggerButton, out bool trigger) && trigger) return true;
            if (device.TryGetFeatureValue(XRCommonUsages.primaryButton, out bool primary) && primary) return true;
            return device.TryGetFeatureValue(XRCommonUsages.secondaryButton, out bool secondary) && secondary;
        }
    }
}
