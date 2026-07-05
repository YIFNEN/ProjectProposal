using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AO.UI
{
    /// <summary>
    /// World-space gameplay HUD container. It can face the camera and keeps the
    /// Week 3 gameplay layout stable after scene reloads or prefab refreshes.
    /// </summary>
    [ExecuteAlways]
    public class HUDController : MonoBehaviour
    {
        [Header("Optional Camera Follow")]
        [SerializeField] private bool _faceCamera = false;
        [SerializeField] private Transform _cameraTarget;
        [SerializeField] private bool _followCamera = false;
        [SerializeField, Min(0.1f)] private float _cameraFollowDistance = 1.25f;
        [SerializeField] private Vector2 _cameraFollowOffset = new Vector2(0f, -0.1f);
        [SerializeField, Range(0f, 30f)] private float _cameraFollowPositionSpeed = 18f;
        [SerializeField, Range(0f, 30f)] private float _cameraFollowRotationSpeed = 18f;
        [SerializeField] private bool _preserveRoll = false;

        [Header("Gameplay Layout")]
        [SerializeField] private bool _enforceGameplayLayout = true;
        [SerializeField] private bool _allowRuntimeLayoutMutation = false;
        [SerializeField] private bool _ensureSongTimeDisplay = true;

        private void Awake()
        {
            if (_cameraTarget == null && Camera.main != null)
            {
                _cameraTarget = Camera.main.transform;
            }

            ApplyGameplayLayout();
            EnsureSongTimeDisplay();
        }

        private void OnEnable()
        {
            ApplyGameplayLayout();
            EnsureSongTimeDisplay();
        }

        private void LateUpdate()
        {
            if (_cameraTarget == null) return;

            if (_followCamera)
            {
                FollowCameraView();
                return;
            }

            if (_faceCamera)
            {
                Vector3 dir = transform.position - _cameraTarget.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(dir);
                }
            }
        }

        private void FollowCameraView()
        {
            Vector3 forward = _cameraTarget.forward;
            Vector3 up = _preserveRoll ? _cameraTarget.up : Vector3.up;
            if (!_preserveRoll)
            {
                forward.y = Mathf.Clamp(forward.y, -0.35f, 0.35f);
                if (forward.sqrMagnitude < 0.001f) forward = _cameraTarget.forward;
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(up, forward).normalized;
            if (right.sqrMagnitude < 0.001f) right = _cameraTarget.right;
            up = Vector3.Cross(forward, right).normalized;

            Vector3 targetPosition =
                _cameraTarget.position +
                forward * _cameraFollowDistance +
                right * _cameraFollowOffset.x +
                up * _cameraFollowOffset.y;

            Quaternion targetRotation = Quaternion.LookRotation(forward, up);

            float positionT = _cameraFollowPositionSpeed <= 0f ? 1f : 1f - Mathf.Exp(-_cameraFollowPositionSpeed * Time.deltaTime);
            float rotationT = _cameraFollowRotationSpeed <= 0f ? 1f : 1f - Mathf.Exp(-_cameraFollowRotationSpeed * Time.deltaTime);

            transform.position = Vector3.Lerp(transform.position, targetPosition, positionT);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationT);
        }

        private void ApplyGameplayLayout()
        {
            if (!_enforceGameplayLayout) return;
            if (Application.isPlaying && !_allowRuntimeLayoutMutation) return;
            if (!gameObject.scene.IsValid()) return;

            var root = transform as RectTransform;
            if (root == null) return;

            root.sizeDelta = new Vector2(900f, 360f);

            RectTransform oxygen = FindRect(root, "OxygenBar");
            if (oxygen != null)
            {
                SetRect(oxygen, new Vector2(-390f, -18f), new Vector2(80f, 240f));
                SetImage(oxygen.gameObject, new Color(1f, 1f, 1f, 0f));

                RectTransform fill = FindRect(oxygen, "Fill");
                if (fill != null)
                {
                    Stretch(fill);
                    Image image = SetImage(fill.gameObject, new Color(0.25f, 0.88f, 0.88f, 1f));
                    image.type = Image.Type.Simple;
                    image.fillMethod = Image.FillMethod.Vertical;
                    image.fillOrigin = (int)Image.OriginVertical.Bottom;
                }

                ConfigureFeverFrame(FindRect(oxygen, "FeverFrameGlow_Image"), 1.5f);
                ConfigureFeverFrame(FindRect(oxygen, "FeverFrameFill_Image"), 1.39f);
                ApplyOxygenGaugeVisualOrder(oxygen);
            }

            RectTransform fever = FindRect(root, "FeverGauge");
            if (fever != null)
            {
                SetRect(fever, new Vector2(-390f, -18f), new Vector2(80f, 240f));
                SetImage(fever.gameObject, new Color(0f, 0f, 0f, 0f));

                RectTransform legacyFill = FindRect(fever, "Fill");
                if (legacyFill != null) legacyFill.gameObject.SetActive(false);

                RectTransform glow = FindRect(oxygen, "FeverFrameGlow_Image");
                RectTransform fill = FindRect(oxygen, "FeverFrameFill_Image");
                if (glow == null) ConfigureFeverFrame(FindRect(fever, "FeverFrameGlow_Image"), 1.5f);
                if (fill == null) ConfigureFeverFrame(FindRect(fever, "FeverFrameFill_Image"), 1.39f);

                if (legacyFill != null &&
                    FindRect(oxygen, "FeverFrameFill_Image") == null &&
                    FindRect(oxygen, "FeverFrameGlow_Image") == null &&
                    FindRect(fever, "FeverFrameFill_Image") == null &&
                    FindRect(fever, "FeverFrameGlow_Image") == null)
                {
                    Stretch(legacyFill);
                    Image image = SetImage(legacyFill.gameObject, new Color(1f, 0.82f, 0.48f, 0.38f));
                    image.type = Image.Type.Filled;
                    image.fillMethod = Image.FillMethod.Vertical;
                    image.fillOrigin = (int)Image.OriginVertical.Bottom;
                    legacyFill.gameObject.SetActive(true);
                }

                Transform label = fever.Find("Label");
                if (label != null) label.gameObject.SetActive(false);
            }

            RectTransform score = FindRect(root, "ScoreDisplay");
            if (score != null) SetRect(score, new Vector2(315f, 168f), new Vector2(120f, 58f));

            RectTransform combo = FindRect(root, "ComboCounter");
            if (combo != null) ApplyComboFrameLayout(combo);

            RectTransform judgement = FindRect(root, "JudgementPopup");
            if (judgement != null) SetRect(judgement, new Vector2(0f, -18f), new Vector2(520f, 150f));

            RemoveLegacyPlayAreaFrame(root);
        }

        private void EnsureSongTimeDisplay()
        {
            if (!_ensureSongTimeDisplay) return;

            RectTransform root = transform as RectTransform;
            if (root == null) return;

            RectTransform rect = FindRect(root, "SongTimeDisplay");
            TMP_Text text = rect != null ? rect.GetComponent<TMP_Text>() : null;
            if (text == null && Application.isPlaying)
            {
                text = RuntimeUiFactory.Text(root, "SongTimeDisplay", "0:00", new Vector2(-245f, 168f), new Vector2(130f, 42f), 26f, TextAlignmentOptions.Center);
                rect = text.rectTransform;
            }

            if (rect == null || text == null) return;

            SetRect(rect, new Vector2(-245f, 168f), new Vector2(130f, 42f));
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 16f;
            text.fontSizeMax = 26f;
            text.color = new Color(0.88f, 1f, 1f, 0.92f);

            SongTimeDisplay display = rect.GetComponent<SongTimeDisplay>();
            if (display == null && Application.isPlaying) display = rect.gameObject.AddComponent<SongTimeDisplay>();
            display?.Configure(text);
        }

        private static RectTransform FindRect(Transform parent, string name)
        {
            Transform child = parent != null ? parent.Find(name) : null;
            return child != null ? child.GetComponent<RectTransform>() : null;
        }

        private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private static void ApplyComboFrameLayout(RectTransform combo)
        {
            SetStretchBorder(FindRect(combo, "Top"), BorderSide.Top, 2f);
            SetStretchBorder(FindRect(combo, "Bottom"), BorderSide.Bottom, 2f);
            SetStretchBorder(FindRect(combo, "Left"), BorderSide.Left, 2f);
            SetStretchBorder(FindRect(combo, "Right"), BorderSide.Right, 2f);
        }

        private static void ConfigureFeverFrame(RectTransform rect, float scale)
        {
            if (rect == null) return;

            Stretch(rect);
            rect.localScale = Vector3.one * scale;

            Image image = rect.GetComponent<Image>();
            if (image == null) return;

            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.raycastTarget = false;
            image.preserveAspect = true;
        }

        private static void ApplyOxygenGaugeVisualOrder(RectTransform oxygen)
        {
            if (oxygen == null) return;

            int index = 0;
            SetSiblingIndex(FindRect(oxygen, "FillMask"), index++);
            SetSiblingIndex(FindRect(oxygen, "FeverFrameGlow_Image"), index++);
            SetSiblingIndex(FindRect(oxygen, "FeverFrameFill_Image"), index++);
            SetSiblingIndex(FindRect(oxygen, "Frame_Image"), index++);
            SetSiblingIndex(FindRect(oxygen, "FeverLeadingSpark_Image"), index);
        }

        private static void SetSiblingIndex(RectTransform rect, int index)
        {
            if (rect != null) rect.SetSiblingIndex(index);
        }

        private enum BorderSide
        {
            Top,
            Bottom,
            Left,
            Right
        }

        private static void SetStretchBorder(RectTransform rect, BorderSide side, float thickness)
        {
            if (rect == null) return;

            switch (side)
            {
                case BorderSide.Top:
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(0f, thickness);
                    break;
                case BorderSide.Bottom:
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = new Vector2(1f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(0f, thickness);
                    break;
                case BorderSide.Left:
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(thickness, 0f);
                    break;
                case BorderSide.Right:
                    rect.anchorMin = new Vector2(1f, 0f);
                    rect.anchorMax = Vector2.one;
                    rect.pivot = new Vector2(1f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(thickness, 0f);
                    break;
            }

            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private static Image SetImage(GameObject go, Color color)
        {
            Image image = go.GetComponent<Image>();
            if (image == null) image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void RemoveLegacyPlayAreaFrame(RectTransform root)
        {
            RectTransform frame = FindRect(root, "PlayAreaFrame");
            if (frame == null) return;

#if UNITY_EDITOR
            if (!Application.isPlaying && Selection.activeTransform != null && Selection.activeTransform.IsChildOf(frame))
            {
                Selection.objects = new UnityEngine.Object[] { root.gameObject };
                Selection.activeObject = root.gameObject;
            }
#endif
            if (Application.isPlaying) Destroy(frame.gameObject);
            else DestroyImmediate(frame.gameObject);
        }
    }
}
