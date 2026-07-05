using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AO.UI
{
    public static class RuntimeUiFactory
    {
        private static TMP_FontAsset _preferredFont;

        public static RectTransform Panel(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = color;
            return rect;
        }

        public static RectTransform Container(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return rect;
        }

        public static TMP_Text Text(Transform parent, string name, string value, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            TMP_Text text = go.GetComponent<TMP_Text>();
            text.text = value;
            ApplyPreferredFont(text);
            text.fontSize = fontSize;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(8f, fontSize * 0.55f);
            text.fontSizeMax = fontSize;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }

        public static Button Button(Transform parent, string name, string label, Vector2 pos, Vector2 size, UnityAction onClick)
        {
            RectTransform rect = Panel(parent, name, pos, size, new Color(0.05f, 0.22f, 0.28f, 0.88f));
            Button button = rect.gameObject.AddComponent<Button>();
            ConfigureButton(button, rect.GetComponent<Image>());
            if (onClick != null) button.onClick.AddListener(onClick);

            Text(rect, "Label", label, Vector2.zero, size - new Vector2(12f, 8f), Mathf.Min(size.y * 0.45f, 30f), TextAlignmentOptions.Center);
            return button;
        }

        public static Slider Slider(Transform parent, string name, string label, Vector2 pos, Vector2 size, float min, float max, float value, UnityAction<float> onValueChanged)
        {
            Text(parent, name + "Label", label, pos + new Vector2(0f, size.y * 0.55f), new Vector2(size.x, 28f), 20f, TextAlignmentOptions.Center);
            RectTransform root = Panel(parent, name, pos, size, new Color(0.03f, 0.12f, 0.16f, 0.82f));

            RectTransform fillArea = Panel(root, "Fill Area", Vector2.zero, size - new Vector2(24f, 12f), new Color(0f, 0f, 0f, 0f));
            Stretch(fillArea, new Vector2(10f, 4f), new Vector2(-10f, -4f));
            RectTransform fill = Panel(fillArea, "Fill", Vector2.zero, Vector2.zero, new Color(0.25f, 0.88f, 0.88f, 0.9f));
            Stretch(fill, Vector2.zero, Vector2.zero);

            RectTransform handleArea = Panel(root, "Handle Slide Area", Vector2.zero, size - new Vector2(24f, 0f), new Color(0f, 0f, 0f, 0f));
            Stretch(handleArea, new Vector2(10f, 0f), new Vector2(-10f, 0f));
            RectTransform handle = Panel(handleArea, "Handle", Vector2.zero, new Vector2(18f, size.y + 8f), new Color(1f, 0.9f, 0.45f, 1f));

            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = Mathf.Clamp(value, min, max);
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            if (onValueChanged != null) slider.onValueChanged.AddListener(onValueChanged);
            return slider;
        }

        public static RectTransform EnsurePanel(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            RectTransform rect = FindDirectRect(parent, name);
            if (rect == null) return Panel(parent, name, pos, size, color);

            rect.gameObject.SetActive(true);
            Image image = rect.GetComponent<Image>();
            if (image == null) image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        public static RectTransform EnsureContainer(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            RectTransform rect = FindDirectRect(parent, name);
            if (rect == null) return Container(parent, name, pos, size);

            rect.gameObject.SetActive(true);
            RemoveImageOnly(rect.gameObject);
            return rect;
        }

        public static TMP_Text EnsureText(Transform parent, string name, string value, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            RectTransform rect = FindDirectRect(parent, name);
            if (rect == null) return Text(parent, name, value, pos, size, fontSize, alignment);

            rect.gameObject.SetActive(true);
            TMP_Text text = rect.GetComponent<TMP_Text>();
            if (text == null) text = rect.GetComponentInChildren<TMP_Text>(true);
            if (text == null) text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            ApplyPreferredFont(text);
            text.fontSize = fontSize;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(8f, fontSize * 0.55f);
            text.fontSizeMax = fontSize;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = alignment;
            return text;
        }

        public static Button EnsureButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, UnityAction onClick)
        {
            RectTransform rect = FindDirectRect(parent, name);
            if (rect == null) return Button(parent, name, label, pos, size, onClick);

            rect.gameObject.SetActive(true);
            Image image = rect.GetComponent<Image>();
            if (image == null) image = rect.gameObject.AddComponent<Image>();
            Button button = rect.GetComponent<Button>();
            if (button == null) button = rect.gameObject.AddComponent<Button>();
            ConfigureButton(button, image);

            button.onClick.RemoveAllListeners();
            if (onClick != null) button.onClick.AddListener(onClick);

            TMP_Text labelText = rect.Find("Label") != null ? rect.Find("Label").GetComponent<TMP_Text>() : null;
            if (labelText == null)
            {
                labelText = Text(rect, "Label", label, Vector2.zero, size - new Vector2(12f, 8f), Mathf.Min(size.y * 0.45f, 30f), TextAlignmentOptions.Center);
            }
            else
            {
                labelText.gameObject.SetActive(true);
                labelText.text = label;
                ApplyPreferredFont(labelText);
            }

            return button;
        }

        public static void ApplyPreferredFont(TMP_Text text)
        {
            if (text == null) return;

            TMP_FontAsset font = GetPreferredFont();
            if (font != null && text.font != font)
            {
                text.font = font;
            }
        }

        public static void SetDirectChildActive(Transform parent, string name, bool active)
        {
            if (parent == null) return;
            Transform child = FindRecursive(parent, name);
            if (child != null) child.gameObject.SetActive(active);
        }

        public static void ClearChildren(Transform parent)
        {
#if UNITY_EDITOR
            ClearEditorSelectionIfDescendant(parent);
#endif
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(child);
                else UnityEngine.Object.DestroyImmediate(child);
            }
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static RectTransform FindDirectRect(Transform parent, string name)
        {
            if (parent == null) return null;
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

        private static void RemoveImageOnly(GameObject go)
        {
            if (go == null) return;

            Image image = go.GetComponent<Image>();
            if (image != null) DestroyComponent(image);

            CanvasRenderer renderer = go.GetComponent<CanvasRenderer>();
            if (renderer != null && go.GetComponent<Graphic>() == null) DestroyComponent(renderer);
        }

        private static void DestroyComponent(Component component)
        {
            if (component == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(component);
            else UnityEngine.Object.DestroyImmediate(component);
        }

#if UNITY_EDITOR
        private static void ClearEditorSelectionIfDescendant(Transform parent)
        {
            if (Application.isPlaying || parent == null) return;

            bool selectionWillBeDestroyed = false;
            UnityEngine.Object[] selectedObjects = Selection.objects;
            foreach (UnityEngine.Object selected in selectedObjects)
            {
                Transform selectedTransform = SelectedTransform(selected);
                if (selectedTransform != null && selectedTransform != parent && selectedTransform.IsChildOf(parent))
                {
                    selectionWillBeDestroyed = true;
                    break;
                }
            }

            if (!selectionWillBeDestroyed) return;
            Selection.objects = new UnityEngine.Object[] { parent.gameObject };
            Selection.activeObject = parent.gameObject;
        }

        private static Transform SelectedTransform(UnityEngine.Object selected)
        {
            if (selected is GameObject go) return go.transform;
            if (selected is Component component) return component.transform;
            return null;
        }
#endif

        private static TMP_FontAsset GetPreferredFont()
        {
            if (_preferredFont != null) return _preferredFont;

            TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (TMP_FontAsset font in fonts)
            {
                if (font != null && font.name.Contains("MPLUSRounded1c-Medium"))
                {
                    _preferredFont = font;
                    return _preferredFont;
                }
            }

            _preferredFont = TMP_Settings.defaultFontAsset;
            return _preferredFont;
        }

        private static void ConfigureButton(Button button, Graphic targetGraphic)
        {
            if (button == null) return;

            bool hasSprite = targetGraphic is Image image && image.sprite != null;
            ColorBlock colors = button.colors;
            if (hasSprite)
            {
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.92f, 1f, 1f, 1f);
                colors.pressedColor = new Color(0.82f, 0.95f, 1f, 1f);
                colors.disabledColor = new Color(1f, 1f, 1f, 0.42f);
            }
            else
            {
                colors.normalColor = new Color(0.05f, 0.22f, 0.28f, 0.88f);
                colors.highlightedColor = new Color(0.12f, 0.42f, 0.5f, 0.95f);
                colors.pressedColor = new Color(0.03f, 0.15f, 0.2f, 1f);
                colors.disabledColor = new Color(0.08f, 0.09f, 0.1f, 0.45f);
            }

            button.colors = colors;
            button.targetGraphic = targetGraphic;
        }
    }
}
