using AO.Core;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

namespace AO.UI
{
    public class GameplaySettingsOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private GameSession _session;
        [SerializeField] private SongLibrary _songLibrary;
        [SerializeField] private bool _placeInFrontOfCameraOnOpen = true;
        [SerializeField] private bool _preferSceneLayout = true;
        [SerializeField] private bool _allowRuntimeUiCreation = false;
        [SerializeField] private bool _showGameplayControls = true;
        [SerializeField] private bool _enforceRequestedLayout = true;
        [SerializeField] private Color _panelTint = Color.white;
        [Header("Settings Visual Theme")]
        [SerializeField] private Sprite _panelFrameSprite;
        [SerializeField] private Sprite _sliderTrackSprite;
        [SerializeField] private Sprite _sliderKnobSprite;
        [SerializeField] private Sprite _buttonSprite;
        [SerializeField] private Sprite _resetButtonSprite;
        [SerializeField] private Sprite _stepperLeftSprite;
        [SerializeField] private Sprite _stepperRightSprite;
        [SerializeField] private Color _backdropColor = new Color(0.01f, 0.08f, 0.14f, 0.22f);
        [SerializeField] private Vector2 _backdropSize = new Vector2(1400f, 900f);
        [SerializeField, Range(0.5f, 2f)] private float _cameraDistance = 0.85f;
        [SerializeField] private Vector3 _cameraSpaceOffset = new Vector3(0f, -0.02f, 0f);
        [SerializeField] private int _sortingOrderWhenOpen = 500;

        private const float MinPlaybackSpeed = 0.5f;
        private const float MaxPlaybackSpeed = 3f;
        private const float PlaybackSpeedStep = 0.5f;
        private const string VrHitAreaName = "VrHitArea";
        private const string TimingResetButtonName = "TimingResetButton";
        private const string BackdropName = "SettingsBackdrop";
        private GameObject _backdrop;
        private readonly List<Canvas> _temporarilyHiddenCanvases = new List<Canvas>();
        private static readonly string[] GameplayOnlyControlNames =
        {
            "RowBackplate_Speed",
            "RowBackplate_Offset",
            "NoteSpeedSlider",
            "NoteSpeedSliderLabel",
            "NoteSpeedValue",
            "PlaybackSpeedSlider",
            "PlaybackSpeedSliderLabel",
            "PlaybackSpeedLabel",
            "PlaybackSpeedDownButton",
            "PlaybackSpeedValue",
            "PlaybackSpeedUpButton",
            "OffsetSlider",
            "OffsetSliderLabel",
            "OffsetValue",
            TimingResetButtonName,
            "OxygenSafeButton"
        };

        private GameStateManager _manager;
        private Canvas _canvas;
        private TMP_Text _playbackLabel;
        private TMP_Text _noteLabel;
        private TMP_Text _offsetLabel;
        private TMP_Text _bgmLabel;
        private TMP_Text _sfxLabel;
        private TMP_Text _testLabel = null;
        private RectTransform _testNeedle = null;
        private Image _testNeedleImage = null;
        private Slider _noteSlider;
        private Slider _offsetSlider;
        private Slider _bgmSlider;
        private Slider _sfxSlider;
        private bool _isOpen;
        private bool _testRunning;
        private bool _leftMenuWasPressed;
        private bool _rightMenuWasPressed;
        private bool _loggedResetButtonTheme;
        private int _resetThemeFramesRemaining;
        private float _testTimer;
        private float _nextTestTickTime;
        private AudioSource _testAudioSource;
        private AudioClip _testTickClip;

        public bool IsOpen => _isOpen;

        public void Bind(GameStateManager manager)
        {
            _manager = manager;
            if (_manager != null) _session = _manager.Session;
            BuildIfNeeded();
            RefreshControls();
        }

        public void RebuildForEditor()
        {
            _canvas = GetComponentInParent<Canvas>();
            ConfigureCanvasSorting();

            if (_allowRuntimeUiCreation)
            {
                RuntimeUiFactory.ClearChildren(transform);
            }

            _panel = null;
            _playbackLabel = null;
            _noteLabel = null;
            _offsetLabel = null;
            _bgmLabel = null;
            _sfxLabel = null;
            _testLabel = null;
            _testNeedle = null;
            _testNeedleImage = null;
            _noteSlider = null;
            _offsetSlider = null;
            _bgmSlider = null;
            _sfxSlider = null;
            _testAudioSource = null;
            BuildIfNeeded();
            if (_panel != null) _panel.SetActive(true);
            RefreshControls();
        }

        public void RefreshSceneObjectsForEditor()
        {
            _canvas = GetComponentInParent<Canvas>();
            ConfigureCanvasSorting();
            BuildIfNeeded();
            RefreshControls();
        }

        private void Awake()
        {
            if (_manager == null) _manager = FindFirstObjectByType<GameStateManager>(FindObjectsInactive.Include);
            _canvas = GetComponentInParent<Canvas>();
            ConfigureCanvasSorting();
            BuildIfNeeded();
            SetOpen(false);
        }

        private void Update()
        {
            bool leftMenuPressed = IsMenuButtonPressed(XRNode.LeftHand);
            bool rightMenuPressed = IsMenuButtonPressed(XRNode.RightHand);

            if (IsSettingsTogglePressed(leftMenuPressed, rightMenuPressed))
            {
                Toggle();
            }

            _leftMenuWasPressed = leftMenuPressed;
            _rightMenuWasPressed = rightMenuPressed;

            if (_isOpen && _testRunning)
            {
                _testTimer += Time.unscaledDeltaTime;
                UpdateJudgementTestVisual();

                if (Time.unscaledTime >= _nextTestTickTime)
                {
                    PlayJudgementTestTick();
                    _nextTestTickTime = Time.unscaledTime + 1f;
                }
            }

            if (_isOpen && _panel != null && _resetThemeFramesRemaining > 0)
            {
                _resetThemeFramesRemaining--;
                ApplyResetButtonTheme(_panel.transform);
            }
        }

        public void Toggle()
        {
            SetOpen(!_isOpen);
        }

        public void SetOpen(bool open)
        {
            _isOpen = open;
            if (open) BringOverlayToFront();
            EnsureBackdrop();
            if (_backdrop != null) _backdrop.SetActive(open);
            if (_panel != null) _panel.SetActive(open);
            if (open) HideNonSettingsCanvases();
            else RestoreHiddenCanvases();
            if (!open) _testRunning = false;
            if (open) _resetThemeFramesRemaining = 30;
            _manager?.SetSettingsOpen(open);
            RefreshControls();
        }

        private void BuildIfNeeded()
        {
            if (_panel == null)
            {
                Transform existingPanel = transform.Find("SettingsPanel");
                if (existingPanel != null) _panel = existingPanel.gameObject;
            }

            if (_panel != null)
            {
                EnsureBackdrop();
                EnsureSpeedControls(_panel.transform);
                if (_enforceRequestedLayout) ApplyRequestedLayout(_panel.transform);
                ApplyPanelTint();
                ApplySettingsVisualTheme(_panel.transform);
                BindExistingControls();
                ApplyControllerFriendlyHitTargets(_panel.transform);
                ApplyResetButtonTheme(_panel.transform);
                RefreshControls();
                return;
            }

            if (!_allowRuntimeUiCreation)
            {
                ReportMissing("GameObject", transform, "SettingsPanel");
                return;
            }

            RectTransform panel = RuntimeUiFactory.Panel(transform, "SettingsPanel", Vector2.zero, new Vector2(620f, 420f), new Color(0.02f, 0.08f, 0.12f, 0.92f));
            _panel = panel.gameObject;
            EnsureBackdrop();
            RuntimeUiFactory.Text(panel, "Title", "Settings", new Vector2(0f, 174f), new Vector2(520f, 42f), 34f, TextAlignmentOptions.Center);

            RuntimeUiFactory.Button(panel, "ResumeButton", "RESUME", new Vector2(-205f, 126f), new Vector2(160f, 46f), () => SetOpen(false));
            RuntimeUiFactory.Button(panel, "LobbyButton", "LOBBY", new Vector2(205f, 126f), new Vector2(160f, 46f), () => _manager?.ReturnToLobby());

            _bgmSlider = RuntimeUiFactory.Slider(panel, "BgmSlider", "BGM", new Vector2(-170f, 62f), new Vector2(110f, 14f), 0f, 1f, PlayerProgress.GetBgmVolume(), value =>
            {
                SetBgmVolume(value);
            });
            _bgmLabel = RuntimeUiFactory.Text(panel, "BgmValue", "", new Vector2(-82f, 62f), new Vector2(80f, 28f), 18f, TextAlignmentOptions.Center);

            _sfxSlider = RuntimeUiFactory.Slider(panel, "SfxSlider", "SFX", new Vector2(170f, 62f), new Vector2(110f, 14f), 0f, 1f, PlayerProgress.GetSfxVolume(), value =>
            {
                SetSfxVolume(value);
            });
            _sfxLabel = RuntimeUiFactory.Text(panel, "SfxValue", "", new Vector2(258f, 62f), new Vector2(80f, 28f), 18f, TextAlignmentOptions.Center);

            _noteSlider = RuntimeUiFactory.Slider(panel, "NoteSpeedSlider", "NOTE SPEED", new Vector2(-170f, -20f), new Vector2(110f, 14f), 0.5f, 2f, 1f, value =>
            {
                SetNoteSpeed(value);
            });
            _noteLabel = RuntimeUiFactory.Text(panel, "NoteSpeedValue", "", new Vector2(-82f, -20f), new Vector2(90f, 28f), 18f, TextAlignmentOptions.Center);

            RuntimeUiFactory.Text(panel, "PlaybackSpeedLabel", "PLAYBACK", new Vector2(170f, -1f), new Vector2(170f, 28f), 20f, TextAlignmentOptions.Center);
            RuntimeUiFactory.Button(panel, "PlaybackSpeedDownButton", "<", new Vector2(106f, -29f), new Vector2(42f, 36f), () => StepPlaybackSpeed(-1));
            _playbackLabel = RuntimeUiFactory.Text(panel, "PlaybackSpeedValue", "", new Vector2(170f, -29f), new Vector2(76f, 30f), 20f, TextAlignmentOptions.Center);
            RuntimeUiFactory.Button(panel, "PlaybackSpeedUpButton", ">", new Vector2(234f, -29f), new Vector2(42f, 36f), () => StepPlaybackSpeed(1));

            _offsetSlider = RuntimeUiFactory.Slider(panel, "OffsetSlider", "JUDGEMENT OFFSET", new Vector2(0f, -104f), new Vector2(180f, 14f), -300f, 300f, 0f, value =>
            {
                SetAudioOffset(value / 1000f);
            });
            _offsetLabel = RuntimeUiFactory.Text(panel, "OffsetValue", "", new Vector2(156f, -104f), new Vector2(120f, 28f), 18f, TextAlignmentOptions.Center);
            RuntimeUiFactory.Button(panel, TimingResetButtonName, "RESET", new Vector2(-235f, -104f), new Vector2(128f, 38f), ResetTimingOptions);

            // The metronome test is intentionally kept internal for now.
            // Actual offset tuning should use live notes/BGM and the judgement delta logs.
            _testAudioSource = panel.GetComponent<AudioSource>();
            if (_testAudioSource == null) _testAudioSource = panel.gameObject.AddComponent<AudioSource>();
            _testAudioSource.playOnAwake = false;
            _testAudioSource.spatialBlend = 0f;
            _testTickClip = CreateTestTickClip();

            EnsureSpeedControls(panel);
            if (_enforceRequestedLayout) ApplyRequestedLayout(panel);
            ApplyPanelTint();
            ApplySettingsVisualTheme(panel);
            BindExistingControls();
            ApplyControllerFriendlyHitTargets(panel);
            ApplyResetButtonTheme(panel);
            RefreshControls();
        }

        private void EnsureBackdrop()
        {
            if (_backdrop == null)
            {
                Transform existing = transform.Find(BackdropName);
                if (existing != null) _backdrop = existing.gameObject;
            }

            if (_backdrop == null)
            {
                _backdrop = new GameObject(BackdropName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rect = _backdrop.GetComponent<RectTransform>();
                rect.SetParent(transform, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = _backdropSize;

                Image image = _backdrop.GetComponent<Image>();
                image.color = _backdropColor;
                image.raycastTarget = false;
            }

            RectTransform backdropRect = _backdrop.transform as RectTransform;
            if (backdropRect != null) backdropRect.sizeDelta = _backdropSize;
            Image backdropImage = _backdrop.GetComponent<Image>();
            if (backdropImage != null) backdropImage.color = _backdropColor;

            if (_panel != null)
            {
                int panelIndex = _panel.transform.GetSiblingIndex();
                _backdrop.transform.SetSiblingIndex(Mathf.Max(0, panelIndex - 1));
            }
        }

        private void HideNonSettingsCanvases()
        {
            if (_temporarilyHiddenCanvases.Count > 0) return;

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || canvas == _canvas || !canvas.enabled) continue;
                canvas.enabled = false;
                _temporarilyHiddenCanvases.Add(canvas);
            }
        }

        private void RestoreHiddenCanvases()
        {
            for (int i = 0; i < _temporarilyHiddenCanvases.Count; i++)
            {
                Canvas canvas = _temporarilyHiddenCanvases[i];
                if (canvas != null) canvas.enabled = true;
            }
            _temporarilyHiddenCanvases.Clear();
        }

        private static void ApplyRequestedLayout(Transform root)
        {
            if (root == null) return;

            RectTransform panelRect = root as RectTransform;
            if (panelRect != null) panelRect.sizeDelta = new Vector2(700f, 500f);

            SetChildActive(root, "Title", true);
            SetChildActive(root, "Background", true);
            SetLayoutRect(root, "Title", new Vector2(0f, 172f), new Vector2(320f, 42f));
            SetResetButtonRect(root);

            RectTransform divider = EnsureCenterDivider(root);
            if (divider != null)
            {
                divider.anchoredPosition = new Vector2(0f, 3f);
                divider.sizeDelta = new Vector2(3f, 278f);
                divider.SetAsFirstSibling();
            }

            SetChildActive(root, "RowBackplate_Audio", false);
            SetChildActive(root, "RowBackplate_Speed", false);
            SetChildActive(root, "RowBackplate_Offset", false);

            SetLayoutRect(root, "BgmSliderLabel", new Vector2(-170f, 105f), new Vector2(190f, 26f));
            SetLayoutRect(root, "BgmValue", new Vector2(-82f, 105f), new Vector2(80f, 26f));
            SetLayoutRect(root, "BgmSlider", new Vector2(-155f, 75f), new Vector2(220f, 22f));
            SetLayoutRect(root, "SfxSliderLabel", new Vector2(-170f, 5f), new Vector2(190f, 26f));
            SetLayoutRect(root, "SfxValue", new Vector2(-82f, 5f), new Vector2(80f, 26f));
            SetLayoutRect(root, "SfxSlider", new Vector2(-155f, -25f), new Vector2(220f, 22f));

            SetLayoutRect(root, "NoteSpeedSliderLabel", new Vector2(125f, 105f), new Vector2(165f, 24f));
            SetLayoutRect(root, "NoteSpeedValue", new Vector2(220f, 105f), new Vector2(75f, 24f));
            SetLayoutRect(root, "NoteSpeedSlider", new Vector2(145f, 75f), new Vector2(210f, 22f));

            SetLayoutRect(root, "OffsetSliderLabel", new Vector2(125f, 25f), new Vector2(165f, 24f));
            SetLayoutRect(root, "OffsetValue", new Vector2(220f, 25f), new Vector2(75f, 24f));
            SetLayoutRect(root, "OffsetSlider", new Vector2(145f, -5f), new Vector2(210f, 22f));
            SetLayoutRect(root, "PlaybackSpeedLabel", new Vector2(125f, -50f), new Vector2(170f, 24f));
            SetLayoutRect(root, "PlaybackSpeedDownButton", new Vector2(72f, -83f), new Vector2(38f, 38f));
            SetLayoutRect(root, "PlaybackSpeedValue", new Vector2(140f, -83f), new Vector2(70f, 26f));
            SetLayoutRect(root, "PlaybackSpeedUpButton", new Vector2(208f, -83f), new Vector2(38f, 38f));

            SetLayoutRect(root, "ResumeButton", new Vector2(-80f, -132f), new Vector2(140f, 40f));
            SetLayoutRect(root, "LobbyButton", new Vector2(80f, -132f), new Vector2(140f, 40f));

            SetTextAlignment(root, "BgmSliderLabel", TextAlignmentOptions.Left);
            SetTextAlignment(root, "SfxSliderLabel", TextAlignmentOptions.Left);
            SetTextAlignment(root, "NoteSpeedSliderLabel", TextAlignmentOptions.Left);
            SetTextAlignment(root, "PlaybackSpeedLabel", TextAlignmentOptions.Left);
            SetTextAlignment(root, "OffsetSliderLabel", TextAlignmentOptions.Left);
            SetTextAlignment(root, "BgmValue", TextAlignmentOptions.Right);
            SetTextAlignment(root, "SfxValue", TextAlignmentOptions.Right);
            SetTextAlignment(root, "NoteSpeedValue", TextAlignmentOptions.Right);
            SetTextAlignment(root, "OffsetValue", TextAlignmentOptions.Right);
            SetUniformTextSize(root, 15f,
                "BgmSliderLabel", "SfxSliderLabel", "NoteSpeedSliderLabel",
                "OffsetSliderLabel", "PlaybackSpeedLabel");
            SetUniformTextSize(root, 15f,
                "BgmValue", "SfxValue", "NoteSpeedValue", "OffsetValue", "PlaybackSpeedValue");
            SetUniformTextSize(root, 28f, "Title");
            SetButtonLabelSize(root, "ResumeButton", 18f);
            SetButtonLabelSize(root, "LobbyButton", 18f);
            SetResetButtonLabelSize(root, 17f);

        }

        private void ApplyPanelTint()
        {
            if (_panel == null) return;

            Image panelImage = _panel.GetComponent<Image>();
            if (panelImage != null) panelImage.color = new Color(1f, 1f, 1f, 0f);

            Transform background = _panel.transform.Find("Background");
            Image backgroundImage = background != null ? background.GetComponent<Image>() : null;
            if (backgroundImage != null) backgroundImage.color = _panelTint;
        }

        private void ApplySettingsVisualTheme(Transform root)
        {
            if (root == null) return;

            SetImageSprite(root.Find("Background"), _panelFrameSprite);

            ApplySliderTheme(root.Find("BgmSlider"));
            ApplySliderTheme(root.Find("SfxSlider"));
            ApplySliderTheme(root.Find("NoteSpeedSlider"));
            ApplySliderTheme(root.Find("OffsetSlider"));

            SetImageSprite(root.Find("ResumeButton"), _buttonSprite);
            SetImageSprite(root.Find("LobbyButton"), _buttonSprite);
            ApplyResetButtonTheme(root);
            SetImageSprite(root.Find("PlaybackSpeedDownButton"), _stepperLeftSprite);
            SetImageSprite(root.Find("PlaybackSpeedUpButton"), _stepperRightSprite);

            Transform divider = root.Find("CenterDivider");
            Image dividerImage = divider != null ? divider.GetComponent<Image>() : null;
            if (dividerImage != null) dividerImage.color = new Color(0.68f, 0.78f, 0.92f, 0.36f);
        }

        private void ApplySliderTheme(Transform sliderRoot)
        {
            if (sliderRoot == null) return;

            SetImageSprite(sliderRoot, _sliderTrackSprite);
            RectTransform fillArea = sliderRoot.Find("Fill Area") as RectTransform;
            if (fillArea != null)
            {
                Vector2 offsetMin = fillArea.offsetMin;
                offsetMin.x = 18f;
                fillArea.offsetMin = offsetMin;
            }

            Image[] images = sliderRoot.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image.name == "Handle" && _sliderKnobSprite != null)
                {
                    image.sprite = _sliderKnobSprite;
                    image.color = Color.white;
                }
                else if (image.name == "Fill")
                {
                    image.sprite = null;
                    image.color = new Color(0.48f, 0.72f, 0.86f, 0.84f);
                }
            }
        }

        private static void SetImageSprite(Transform target, Sprite sprite)
        {
            if (target == null || sprite == null) return;
            Image image = target.GetComponent<Image>();
            if (image == null) return;
            image.sprite = sprite;
            image.color = Color.white;
        }

        private void ApplyResetButtonTheme(Transform root)
        {
            Transform resetButton = FindResetButtonTransform(root);
            CopyResetFrameFromResume(root, resetButton);
            SetResetButtonRect(root);
            SetResetButtonLabelSize(root, 17f);
            HideResetButtonChildImages(resetButton);
            HideResetFrameSiblings(root, resetButton);

            if (!_loggedResetButtonTheme && resetButton != null)
            {
                _loggedResetButtonTheme = true;
                RectTransform rect = resetButton as RectTransform;
                Image image = resetButton.GetComponent<Image>();
                Debug.Log($"[GameplaySettingsOverlay] Reset button themed: path={GetTransformPath(resetButton)}, sprite={(image != null && image.sprite != null ? image.sprite.name : "<none>")}, size={(rect != null ? rect.sizeDelta.ToString() : "<none>")}, resetImages={DescribeResetImages(root)}", this);
            }
        }

        private static void HideResetButtonChildImages(Transform resetButton)
        {
            if (resetButton == null) return;

            Image[] images = resetButton.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null) continue;

                if (image.transform == resetButton)
                {
                    image.raycastTarget = true;
                    continue;
                }

                if (image.gameObject.name == VrHitAreaName)
                {
                    image.sprite = null;
                    image.color = new Color(1f, 1f, 1f, 0.001f);
                    image.raycastTarget = true;
                    continue;
                }

                image.sprite = null;
                image.color = new Color(1f, 1f, 1f, 0f);
                image.raycastTarget = false;
            }
        }

        private static void HideResetFrameSiblings(Transform root, Transform resetButton)
        {
            RectTransform resetRect = resetButton as RectTransform;
            if (root == null || resetRect == null) return;

            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null || image.transform == resetButton) continue;
                if (image.transform.IsChildOf(resetButton)) continue;
                if (image.GetComponentInParent<Selectable>(true) != null) continue;

                RectTransform rect = image.transform as RectTransform;
                if (rect == null) continue;
                if (Vector2.Distance(rect.anchoredPosition, resetRect.anchoredPosition) > 6f) continue;
                if (Mathf.Abs(rect.sizeDelta.x - resetRect.sizeDelta.x) > 80f) continue;
                if (Mathf.Abs(rect.sizeDelta.y - resetRect.sizeDelta.y) > 40f) continue;

                image.sprite = null;
                image.color = new Color(1f, 1f, 1f, 0f);
                image.raycastTarget = false;
            }
        }

        private static string DescribeResetImages(Transform root)
        {
            if (root == null) return "<none>";

            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            Vector2 resetPosition = new Vector2(190f, 172f);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null || !string.Equals(text.text, "RESET", System.StringComparison.OrdinalIgnoreCase)) continue;
                RectTransform textRect = text.transform as RectTransform;
                if (textRect != null) resetPosition = textRect.anchoredPosition;
                break;
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                RectTransform rect = image != null ? image.transform as RectTransform : null;
                if (image == null || rect == null) continue;
                if (Vector2.Distance(rect.anchoredPosition, resetPosition) > 260f) continue;

                if (builder.Length > 0) builder.Append(" | ");
                builder.Append(GetTransformPath(image.transform));
                builder.Append(" sprite=");
                builder.Append(image.sprite != null ? image.sprite.name : "<none>");
                builder.Append(" colorA=");
                builder.Append(image.color.a.ToString("0.###"));
                builder.Append(" size=");
                builder.Append(rect.sizeDelta);
            }

            return builder.Length > 0 ? builder.ToString() : "<none>";
        }

        private static void SetResetButtonRect(Transform root)
        {
            RectTransform rect = FindResetButtonTransform(root) as RectTransform;
            if (rect == null) return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(190f, 172f);
            rect.sizeDelta = new Vector2(104f, 36f);
        }

        private static void SetResetButtonLabelSize(Transform root, float fontSize)
        {
            Transform resetButton = FindResetButtonTransform(root);
            TMP_Text text = resetButton != null ? resetButton.GetComponentInChildren<TMP_Text>(true) : null;
            if (text == null) return;

            text.enableAutoSizing = false;
            text.fontSize = fontSize;
        }

        private static Transform FindResetButtonTransform(Transform root)
        {
            if (root == null) return null;

            Transform direct = root.Find(TimingResetButtonName);
            if (direct != null) return direct;

            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null || !string.Equals(text.text, "RESET", System.StringComparison.OrdinalIgnoreCase)) continue;

                Transform candidate = text.transform.parent;
                while (candidate != null && candidate != root)
                {
                    if (candidate.GetComponent<Button>() != null || candidate.GetComponent<Image>() != null) return candidate;
                    candidate = candidate.parent;
                }
            }

            return null;
        }

        private void CopyResetFrameFromResume(Transform root, Transform resetButton)
        {
            if (resetButton == null) return;

            Image resetImage = resetButton.GetComponent<Image>();
            if (resetImage == null) return;

            Image resumeImage = null;
            Transform resume = root != null ? root.Find("ResumeButton") : null;
            if (resume != null) resumeImage = resume.GetComponent<Image>();

            if (resumeImage != null && resumeImage.sprite != null)
            {
                resetImage.sprite = resumeImage.sprite;
                resetImage.material = resumeImage.material;
                resetImage.type = resumeImage.type;
                resetImage.fillCenter = resumeImage.fillCenter;
                resetImage.pixelsPerUnitMultiplier = resumeImage.pixelsPerUnitMultiplier;
            }
            else
            {
                resetImage.sprite = _buttonSprite;
            }

            resetImage.color = Color.white;
            resetImage.preserveAspect = false;
            resetImage.raycastTarget = true;

            Button button = resetButton.GetComponent<Button>();
            if (button != null)
            {
                button.targetGraphic = resetImage;
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.9f, 1f, 1f, 1f);
                colors.pressedColor = new Color(0.78f, 0.92f, 1f, 1f);
                colors.disabledColor = new Color(1f, 1f, 1f, 0.42f);
                button.colors = colors;
            }
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null) return "<none>";

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static void SetLayoutRect(Transform root, string name, Vector2 position, Vector2 size)
        {
            RectTransform rect = root.Find(name) as RectTransform;
            if (rect == null) return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetTextAlignment(Transform root, string name, TextAlignmentOptions alignment)
        {
            Transform child = root.Find(name);
            TMP_Text text = child != null ? child.GetComponent<TMP_Text>() : null;
            if (text != null) text.alignment = alignment;
        }

        private static void SetUniformTextSize(Transform root, float fontSize, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform child = root.Find(names[i]);
                TMP_Text text = child != null ? child.GetComponent<TMP_Text>() : null;
                if (text == null) continue;

                text.enableAutoSizing = false;
                text.fontSize = fontSize;
            }
        }

        private static void SetButtonLabelSize(Transform root, string buttonName, float fontSize)
        {
            Transform button = root.Find(buttonName);
            Transform label = button != null ? button.Find("Label") : null;
            TMP_Text text = label != null ? label.GetComponent<TMP_Text>() : null;
            if (text == null) return;

            text.enableAutoSizing = false;
            text.fontSize = fontSize;
        }

        private static RectTransform EnsureCenterDivider(Transform root)
        {
            const string dividerName = "CenterDivider";
            RectTransform rect = root.Find(dividerName) as RectTransform;
            Image image;
            if (rect == null)
            {
                GameObject go = new GameObject(dividerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(root, false);
                rect = go.transform as RectTransform;
                image = go.GetComponent<Image>();
            }
            else
            {
                image = rect.GetComponent<Image>();
                if (image == null) image = rect.gameObject.AddComponent<Image>();
            }

            image.color = new Color(0.62f, 0.94f, 1f, 0.55f);
            image.raycastTarget = false;
            return rect;
        }

        private void BindExistingControls()
        {
            if (_panel == null) return;
            Transform root = _panel.transform;
            bool gameplayActions = ShouldShowGameplayActions();
            bool timingControls = ShouldShowTimingControls();

            BindButton(root, "ResumeButton", () => SetOpen(false));
            if (gameplayActions)
            {
                BindButton(root, "LobbyButton", () => _manager?.ReturnToLobby());
            }
            else
            {
                SetChildActive(root, "LobbyButton", false);
            }

            _bgmSlider = BindSlider(root, "BgmSlider", value =>
            {
                SetBgmVolume(value);
            });
            _sfxSlider = BindSlider(root, "SfxSlider", value =>
            {
                SetSfxVolume(value);
            });

            if (timingControls)
            {
                EnsureTimingResetButton(root);
                _noteSlider = BindSlider(root, "NoteSpeedSlider", value =>
                {
                    SetNoteSpeed(value);
                });
                _offsetSlider = BindSlider(root, "OffsetSlider", value =>
                {
                    SetAudioOffset(value / 1000f);
                });
                BindButton(root, "PlaybackSpeedDownButton", () => StepPlaybackSpeed(-1));
                BindButton(root, "PlaybackSpeedUpButton", () => StepPlaybackSpeed(1));
                BindOptionalButton(root, TimingResetButtonName, ResetTimingOptions);
            }
            else
            {
                _noteSlider = null;
                _offsetSlider = null;
            }

            _bgmLabel = FindText(root, "BgmValue");
            _sfxLabel = FindText(root, "SfxValue");
            _noteLabel = timingControls ? FindText(root, "NoteSpeedValue") : null;
            _playbackLabel = timingControls ? FindText(root, "PlaybackSpeedValue") : null;
            _offsetLabel = timingControls ? FindText(root, "OffsetValue") : null;
            ApplyControllerFriendlyHitTargets(root);
        }

        private void BindButton(Transform root, string name, UnityEngine.Events.UnityAction action)
        {
            Transform child = root.Find(name);
            Button button = child != null ? child.GetComponent<Button>() : null;
            if (button == null)
            {
                ReportMissing("Button", root, name);
                return;
            }

            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(action);
        }

        private void BindOptionalButton(Transform root, string name, UnityEngine.Events.UnityAction action)
        {
            Transform child = root.Find(name);
            Button button = child != null ? child.GetComponent<Button>() : null;
            if (button == null) return;

            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(action);
        }

        private Slider BindSlider(Transform root, string name, UnityEngine.Events.UnityAction<float> action)
        {
            Transform child = root.Find(name);
            Slider slider = child != null ? child.GetComponent<Slider>() : null;
            if (slider == null)
            {
                ReportMissing("Slider", root, name);
                return null;
            }

            slider.onValueChanged.RemoveAllListeners();
            if (action != null) slider.onValueChanged.AddListener(action);
            return slider;
        }

        private TMP_Text FindText(Transform root, string name)
        {
            Transform child = root.Find(name);
            TMP_Text text = child != null ? child.GetComponent<TMP_Text>() : null;
            if (text == null) ReportMissing("TMP_Text", root, name);
            return text;
        }

        private void EnsureSpeedControls(Transform root)
        {
            if (root == null) return;

            bool timingControls = ShouldShowTimingControls();
            SetGameplayOnlyControlsActive(root, timingControls);
            if (!timingControls) return;

            EnsureChildActive(root, "NoteSpeedSlider");
            EnsureChildActive(root, "NoteSpeedSliderLabel");
            EnsureChildActive(root, "NoteSpeedValue");
            SetChildActive(root, "PlaybackSpeedSlider", false);
            SetChildActive(root, "PlaybackSpeedSliderLabel", false);
            EnsureChildActive(root, "PlaybackSpeedLabel");
            EnsureChildActive(root, "PlaybackSpeedDownButton");
            EnsureChildActive(root, "PlaybackSpeedValue");
            EnsureChildActive(root, "PlaybackSpeedUpButton");
            EnsureChildActive(root, TimingResetButtonName);

            RectTransform panel = root as RectTransform;
            if (panel == null) return;

            if (root.Find("NoteSpeedSlider") == null)
            {
                if (!_allowRuntimeUiCreation)
                {
                    ReportMissing("Slider", root, "NoteSpeedSlider");
                    return;
                }

                RuntimeUiFactory.Slider(panel, "NoteSpeedSlider", "NOTE SPEED", new Vector2(-170f, -20f), new Vector2(110f, 14f), 0.5f, 2f, 1f, value =>
                {
                    _manager?.SetNoteSpeed(value);
                    RefreshLabels();
                });
            }

            if (root.Find("NoteSpeedValue") == null)
            {
                if (!_allowRuntimeUiCreation)
                {
                    ReportMissing("TMP_Text", root, "NoteSpeedValue");
                    return;
                }

                RuntimeUiFactory.Text(panel, "NoteSpeedValue", "", new Vector2(-82f, -20f), new Vector2(90f, 28f), 18f, TextAlignmentOptions.Center);
            }

            if (root.Find("PlaybackSpeedLabel") == null)
            {
                if (!_allowRuntimeUiCreation)
                {
                    ReportMissing("TMP_Text", root, "PlaybackSpeedLabel");
                    return;
                }

                RuntimeUiFactory.Text(panel, "PlaybackSpeedLabel", "PLAYBACK", new Vector2(170f, -1f), new Vector2(170f, 28f), 20f, TextAlignmentOptions.Center);
            }

            if (root.Find("PlaybackSpeedDownButton") == null)
            {
                if (!_allowRuntimeUiCreation)
                {
                    ReportMissing("Button", root, "PlaybackSpeedDownButton");
                    return;
                }

                RuntimeUiFactory.Button(panel, "PlaybackSpeedDownButton", "<", new Vector2(106f, -29f), new Vector2(42f, 36f), () => StepPlaybackSpeed(-1));
            }

            if (root.Find("PlaybackSpeedValue") == null)
            {
                if (!_allowRuntimeUiCreation)
                {
                    ReportMissing("TMP_Text", root, "PlaybackSpeedValue");
                    return;
                }

                RuntimeUiFactory.Text(panel, "PlaybackSpeedValue", "", new Vector2(170f, -29f), new Vector2(76f, 30f), 20f, TextAlignmentOptions.Center);
            }

            if (root.Find("PlaybackSpeedUpButton") == null)
            {
                if (!_allowRuntimeUiCreation)
                {
                    ReportMissing("Button", root, "PlaybackSpeedUpButton");
                    return;
                }

                RuntimeUiFactory.Button(panel, "PlaybackSpeedUpButton", ">", new Vector2(234f, -29f), new Vector2(42f, 36f), () => StepPlaybackSpeed(1));
            }

            SetChildActive(root, "OxygenSafeButton", false);
            EnsureTimingResetButton(root);

            if (!_preferSceneLayout)
            {
                MoveControlIfStillAt(root, "OffsetSlider", new Vector2(0f, -34f), new Vector2(0f, -104f));
                MoveControlIfStillAt(root, "OffsetSliderLabel", new Vector2(0f, -26.3f), new Vector2(0f, -96.3f));
                MoveControlIfStillAt(root, "OffsetValue", new Vector2(156f, -34f), new Vector2(156f, -104f));
                MoveControlIfStillAt(root, "PlaybackSpeedValue", new Vector2(258f, -20f), new Vector2(170f, -29f));
                MoveControlIfStillAt(root, "PlaybackSpeedLabel", new Vector2(170f, -12.3f), new Vector2(170f, -1f));
            }
        }

        private void EnsureTimingResetButton(Transform root)
        {
            if (root == null) return;
            Transform existing = root.Find(TimingResetButtonName);
            if (existing != null)
            {
                existing.gameObject.SetActive(ShouldShowTimingControls());
                ApplyResetButtonTheme(root);
                return;
            }

            RectTransform panel = root as RectTransform;
            if (panel == null || !Application.isPlaying) return;

            RuntimeUiFactory.Button(panel, TimingResetButtonName, "RESET", new Vector2(190f, 172f), new Vector2(118f, 38f), ResetTimingOptions);
            ApplyResetButtonTheme(root);
        }

        private static void EnsureChildActive(Transform root, string childName)
        {
            Transform child = root.Find(childName);
            if (child != null) child.gameObject.SetActive(true);
        }

        private static void SetChildActive(Transform root, string childName, bool active)
        {
            Transform child = root.Find(childName);
            if (child != null) child.gameObject.SetActive(active);
        }

        private static void SetGameplayOnlyControlsActive(Transform root, bool active)
        {
            for (int i = 0; i < GameplayOnlyControlNames.Length; i++)
            {
                SetChildActive(root, GameplayOnlyControlNames[i], active);
            }
        }

        private static void MoveControlIfStillAt(Transform root, string childName, Vector2 oldPosition, Vector2 newPosition)
        {
            Transform child = root.Find(childName);
            RectTransform rect = child != null ? child as RectTransform : null;
            if (rect == null) return;
            if (Vector2.Distance(rect.anchoredPosition, oldPosition) <= 1f) rect.anchoredPosition = newPosition;
        }

        private void StepPlaybackSpeed(int direction)
        {
            float current = QuantizePlaybackSpeed(GetCurrentPlaybackSpeed());
            float next = Mathf.Clamp(current + PlaybackSpeedStep * direction, MinPlaybackSpeed, MaxPlaybackSpeed);
            SetPlaybackSpeed(next);
            RefreshControls();
        }

        private void SetPlaybackSpeed(float value)
        {
            value = QuantizePlaybackSpeed(value);
            if (_manager != null)
            {
                _manager.SetPlaybackSpeed(value);
            }
            else
            {
                string songId = ResolveSettingsSongId();
                if (!string.IsNullOrEmpty(songId)) PlayerProgress.SetPlaybackSpeed(songId, value);
                if (_session != null) _session.PlaybackSpeed = value;
                PlayerPrefs.Save();
            }

            RefreshLabels();
        }

        private void SetNoteSpeed(float value)
        {
            value = Mathf.Clamp(value, 0.5f, 2f);
            if (_manager != null)
            {
                _manager.SetNoteSpeed(value);
            }
            else
            {
                string songId = ResolveSettingsSongId();
                if (!string.IsNullOrEmpty(songId)) PlayerProgress.SetNoteSpeed(songId, value);
                if (_session != null) _session.NoteSpeed = value;
                PlayerPrefs.Save();
            }

            RefreshLabels();
        }

        private void SetAudioOffset(float seconds)
        {
            seconds = Mathf.Clamp(seconds, -0.3f, 0.3f);
            if (_manager != null)
            {
                _manager.SetAudioOffset(seconds);
            }
            else
            {
                string songId = ResolveSettingsSongId();
                if (!string.IsNullOrEmpty(songId)) PlayerProgress.SetAudioOffset(songId, seconds);
                if (_session != null) _session.AudioOffsetSeconds = seconds;
                PlayerPrefs.Save();
            }

            RefreshLabels();
        }

        private void ResetTimingOptions()
        {
            if (_manager != null)
            {
                _manager.ResetTimingOptionsToDefaults();
                RefreshControls();
                return;
            }

            SongDefinition song = ResolveSettingsSong();
            if (song == null) return;

            PlayerProgress.ResetTimingOptions(song);
            PlayerPrefs.Save();

            if (_session != null)
            {
                _session.SetSpeed(PlayerProgress.GetPlaybackSpeed(song), PlayerProgress.GetNoteSpeed(song));
                _session.SetAudioOffset(PlayerProgress.GetAudioOffset(song));
            }

            RefreshControls();
        }

        private void SetBgmVolume(float value)
        {
            value = Mathf.Clamp01(value);
            if (_manager != null)
            {
                _manager.SetBgmVolume(value);
            }
            else
            {
                PlayerProgress.SetBgmVolume(value);
                PlayerPrefs.Save();
            }

            RefreshLabels();
        }

        private void SetSfxVolume(float value)
        {
            value = Mathf.Clamp01(value);
            if (_manager != null)
            {
                _manager.SetSfxVolume(value);
            }
            else
            {
                PlayerProgress.SetSfxVolume(value);
                PlayerPrefs.Save();
            }

            RefreshLabels();
        }

        private bool ShouldShowGameplayActions()
        {
            return _showGameplayControls;
        }

        private bool ShouldShowTimingControls()
        {
            return _showGameplayControls;
        }

        private static void ApplyControllerFriendlyHitTargets(Transform root)
        {
            if (root == null) return;

            DisablePassiveRaycastTargets(root);
            EnsureSelectableHitArea(root, "ResumeButton", new Vector2(200f, 66f));
            EnsureSelectableHitArea(root, "LobbyButton", new Vector2(200f, 66f));
            EnsureSelectableHitArea(root, "PlaybackSpeedDownButton", new Vector2(72f, 72f));
            EnsureSelectableHitArea(root, "PlaybackSpeedUpButton", new Vector2(72f, 72f));
            EnsureSelectableHitArea(root, TimingResetButtonName, new Vector2(160f, 64f));
            EnsureSelectableHitArea(root, "BgmSlider", new Vector2(260f, 48f));
            EnsureSelectableHitArea(root, "SfxSlider", new Vector2(260f, 48f));
            EnsureSelectableHitArea(root, "NoteSpeedSlider", new Vector2(260f, 48f));
            EnsureSelectableHitArea(root, "OffsetSlider", new Vector2(340f, 48f));
        }

        private static void DisablePassiveRaycastTargets(Transform root)
        {
            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null) continue;

                if (graphic is TMP_Text)
                {
                    graphic.raycastTarget = false;
                    continue;
                }

                if (graphic.gameObject.name == VrHitAreaName)
                {
                    graphic.raycastTarget = true;
                    continue;
                }

                if (graphic.GetComponentInParent<Selectable>(true) == null)
                {
                    graphic.raycastTarget = false;
                }
            }
        }

        private static void EnsureSelectableHitArea(Transform root, string controlName, Vector2 minSize)
        {
            Transform control = root.Find(controlName);
            RectTransform controlRect = control as RectTransform;
            Selectable selectable = control != null ? control.GetComponent<Selectable>() : null;
            if (controlRect == null || selectable == null) return;

            RectTransform hitArea = control.Find(VrHitAreaName) as RectTransform;
            if (hitArea == null)
            {
                GameObject hitObject = new GameObject(VrHitAreaName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                hitObject.transform.SetParent(control, false);
                hitArea = hitObject.transform as RectTransform;
            }

            hitArea.anchorMin = new Vector2(0.5f, 0.5f);
            hitArea.anchorMax = new Vector2(0.5f, 0.5f);
            hitArea.pivot = new Vector2(0.5f, 0.5f);
            hitArea.anchoredPosition = Vector2.zero;
            hitArea.sizeDelta = new Vector2(
                Mathf.Max(Mathf.Abs(controlRect.rect.width), minSize.x),
                Mathf.Max(Mathf.Abs(controlRect.rect.height), minSize.y));
            hitArea.SetAsLastSibling();

            Image image = hitArea.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.001f);
            image.raycastTarget = true;
        }

        private static float QuantizePlaybackSpeed(float value)
        {
            float stepped = Mathf.Round(value / PlaybackSpeedStep) * PlaybackSpeedStep;
            return Mathf.Clamp(stepped, MinPlaybackSpeed, MaxPlaybackSpeed);
        }

        private void ToggleJudgementTest()
        {
            _testRunning = !_testRunning;
            _testTimer = 0f;
            _nextTestTickTime = Time.unscaledTime;
            if (_testLabel != null)
            {
                _testLabel.text = _testRunning ? "HIT NOW" : "READY";
                _testLabel.alpha = 1f;
            }
            UpdateJudgementTestVisual();
        }

        private void PlayJudgementTestTick()
        {
            if (_testAudioSource == null || _testTickClip == null) return;
            _testAudioSource.PlayOneShot(_testTickClip, Mathf.Clamp01(PlayerProgress.GetSfxVolume()));
        }

        private void UpdateJudgementTestVisual()
        {
            float phase = _testRunning ? Mathf.Repeat(_testTimer, 1f) : 0f;
            float x = Mathf.Sin(phase * Mathf.PI * 2f) * 180f;
            if (_testNeedle != null)
            {
                _testNeedle.anchoredPosition = new Vector2(x, 0f);
            }

            float centerFactor = 1f - Mathf.Clamp01(Mathf.Abs(x) / 180f);
            if (_testNeedleImage != null)
            {
                _testNeedleImage.color = Color.Lerp(new Color(0.55f, 0.85f, 1f, 0.9f), new Color(1f, 0.95f, 0.48f, 1f), centerFactor);
            }

            if (_testLabel != null)
            {
                _testLabel.alpha = Mathf.Lerp(0.38f, 1f, centerFactor);
                if (_testRunning) _testLabel.text = centerFactor > 0.92f ? "HIT NOW" : "LISTEN";
            }
        }

        private static AudioClip CreateTestTickClip()
        {
            const int sampleRate = 44100;
            const float lengthSeconds = 0.065f;
            int sampleCount = Mathf.CeilToInt(sampleRate * lengthSeconds);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t * 55f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * 1200f * t) * envelope * 0.45f;
            }

            AudioClip clip = AudioClip.Create("AO_JudgementTestTick", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void RefreshLabels()
        {
            if (_playbackLabel != null) _playbackLabel.text = $"{QuantizePlaybackSpeed(GetCurrentPlaybackSpeed()):F1}x";
            if (_noteLabel != null) _noteLabel.text = $"{GetCurrentNoteSpeed():F2}x";
            if (_offsetLabel != null) _offsetLabel.text = $"{Mathf.RoundToInt(GetCurrentAudioOffset() * 1000f)} ms";
            if (_bgmLabel != null) _bgmLabel.text = $"{Mathf.RoundToInt(PlayerProgress.GetBgmVolume() * 100f)}%";
            if (_sfxLabel != null) _sfxLabel.text = $"{Mathf.RoundToInt(PlayerProgress.GetSfxVolume() * 100f)}%";
            if (_testLabel != null && !_testRunning) _testLabel.text = "READY";
            if (!_testRunning) UpdateJudgementTestVisual();
        }

        private void RefreshControls()
        {
            SetSliderValueWithoutNotify(_bgmSlider, PlayerProgress.GetBgmVolume());
            SetSliderValueWithoutNotify(_sfxSlider, PlayerProgress.GetSfxVolume());
            SetSliderValueWithoutNotify(_noteSlider, GetCurrentNoteSpeed());
            SetSliderValueWithoutNotify(_offsetSlider, GetCurrentAudioOffset() * 1000f);
            RefreshLabels();
        }

        private float GetCurrentPlaybackSpeed()
        {
            if (_manager != null) return _manager.PlaybackSpeed;

            SongDefinition song = ResolveSettingsSong();
            if (song != null) return PlayerProgress.GetPlaybackSpeed(song);
            return _session != null && _session.PlaybackSpeed > 0f ? _session.PlaybackSpeed : 1f;
        }

        private float GetCurrentNoteSpeed()
        {
            if (_manager != null) return _manager.NoteSpeed;

            SongDefinition song = ResolveSettingsSong();
            if (song != null) return PlayerProgress.GetNoteSpeed(song);
            return _session != null && _session.NoteSpeed > 0f ? _session.NoteSpeed : 1f;
        }

        private float GetCurrentAudioOffset()
        {
            if (_manager != null) return _manager.AudioOffsetSeconds;

            SongDefinition song = ResolveSettingsSong();
            if (song != null) return PlayerProgress.GetAudioOffset(song);
            return _session != null ? _session.AudioOffsetSeconds : 0f;
        }

        private string ResolveSettingsSongId()
        {
            SongDefinition song = ResolveSettingsSong();
            if (song != null && !string.IsNullOrEmpty(song.SongId)) return song.SongId;
            return _session != null ? _session.SelectedSongId : "";
        }

        private SongDefinition ResolveSettingsSong()
        {
            if (_manager != null && _manager.CurrentSong != null) return _manager.CurrentSong;
            if (_songLibrary == null) return null;
            return _songLibrary.FindById(_session != null ? _session.SelectedSongId : null);
        }

        private void ConfigureCanvasSorting()
        {
            if (_canvas == null) return;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = _sortingOrderWhenOpen;
        }

        private void BringOverlayToFront()
        {
            ConfigureCanvasSorting();
            if (!_placeInFrontOfCameraOnOpen) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Transform canvasTransform = _canvas != null ? _canvas.transform : transform;
            canvasTransform.position = cam.transform.position
                + cam.transform.forward * _cameraDistance
                + cam.transform.TransformVector(_cameraSpaceOffset);
            canvasTransform.rotation = cam.transform.rotation;
        }

        private bool IsSettingsTogglePressed(bool leftMenuPressed, bool rightMenuPressed)
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
            return (leftMenuPressed && !_leftMenuWasPressed) || (rightMenuPressed && !_rightMenuWasPressed);
        }

        private static bool IsMenuButtonPressed(XRNode node)
        {
            UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            return device.isValid
                && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool pressed)
                && pressed;
        }

        private static void SetSliderValueWithoutNotify(Slider slider, float value)
        {
            if (slider != null) slider.SetValueWithoutNotify(value);
        }

        private void ReportMissing(string expectedType, Transform parent, string name)
        {
            string parentName = parent != null ? parent.name : "<null>";
            Debug.LogError($"[GameplaySettingsOverlay] Required Gameplay settings object '{name}' ({expectedType}) under '{parentName}' is missing or misconfigured. Runtime UI creation is disabled, so no replacement was generated.", this);
        }

    }
}
