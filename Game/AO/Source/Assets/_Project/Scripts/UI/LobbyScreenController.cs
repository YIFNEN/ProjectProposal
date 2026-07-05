using AO.Core;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AO.UI
{
    [ExecuteAlways]
    public class LobbyScreenController : MonoBehaviour
    {
        [SerializeField] private SongLibrary _songLibrary;
        [SerializeField] private GameSession _session;
        [SerializeField] private LobbyPreviewPlayer _previewPlayer;
        [SerializeField] private Sprite _previewPlaySprite;
        [SerializeField] private Sprite _previewPauseSprite;

        [Header("OceanBlend Lobby Theme")]
        [SerializeField] private Sprite _titleFrameSprite;
        [SerializeField] private Sprite _topUtilityButtonSprite;
        [SerializeField] private Sprite _songCardFrameSprite;
        [SerializeField] private Sprite _songCardGlowSprite;
        [SerializeField] private Sprite _thumbnailFrameSprite;
        [SerializeField] private Sprite _previousArrowSprite;
        [SerializeField] private Sprite _nextArrowSprite;
        [SerializeField] private Sprite _normalModeSprite;
        [SerializeField] private Sprite _eternalModeSprite;
        [SerializeField] private Sprite _eternalLockSprite;

        [Header("Eternal Unlock Presentation")]
        [SerializeField] private GameObject _eternalUnlockParticlePrefab;
        [SerializeField, Range(0f, 3f)] private float _eternalUnlockPresentationDelay = 0.75f;
        [SerializeField, Range(0.2f, 3f)] private float _eternalUnlockDuration = 1.05f;
        [SerializeField, Range(1f, 3f)] private float _eternalUnlockEndScale = 1.55f;
        [SerializeField, Range(0f, 720f)] private float _eternalUnlockSpinDegrees = 240f;
        [SerializeField] private Vector2 _eternalUnlockIconDrift = new Vector2(0f, 42f);
        [SerializeField, Range(0, 24)] private int _eternalUnlockShardCount = 12;
        [SerializeField] private Vector2 _eternalUnlockShardSize = new Vector2(18f, 18f);
        [SerializeField] private Vector2 _eternalUnlockShardDistance = new Vector2(44f, 112f);
        [SerializeField, Min(0.1f)] private float _eternalUnlockParticleLifetime = 2f;

        [SerializeField] private bool _preferSceneLayout = true;
        [SerializeField] private bool _preferSceneVisuals = true;
        [SerializeField] private bool _allowRuntimeUiCreation = false;

        private int _songIndex;
        private RectTransform _songContentRoot;
        private SongDefinition _selectedSong;
        private Button _previewToggleButton;
        private Coroutine _eternalUnlockRoutine;

        private void Awake()
        {
            EnsurePreviewPlayer();
        }

        private void Start()
        {
            MenuSceneWorld.EnsureCommonBackdrop(MenuSceneCharacterMode.LobbyLeft);
            Build();
        }

        private void OnEnable()
        {
            EnsurePreviewPlayer();
            if (Application.isPlaying)
            {
                if (_previewPlayer != null) _previewPlayer.ResetPreviewState();
            }
#if UNITY_EDITOR
            else
            {
                UnityEditor.EditorApplication.delayCall -= RefreshEditorPreview;
                UnityEditor.EditorApplication.delayCall += RefreshEditorPreview;
            }
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= RefreshEditorPreview;
#endif
            if (_previewPlayer != null) _previewPlayer.ResetPreviewState();
        }

#if UNITY_EDITOR
        private void RefreshEditorPreview()
        {
            if (this == null || Application.isPlaying || !isActiveAndEnabled) return;
            EnsurePreviewPlayer();
            Build();
        }
#endif

        public void Configure(SongLibrary library, GameSession session)
        {
            _songLibrary = library;
            _session = session;
        }

        public void RebuildForEditor()
        {
            if (!_allowRuntimeUiCreation)
            {
                Debug.LogError("[LobbyScreenController] RebuildForEditor is disabled because runtime UI creation is off. Edit the Lobby scene objects directly.", this);
                return;
            }

            RuntimeUiFactory.ClearChildren(transform);
            _songContentRoot = null;
            Build();
        }

        private void Build()
        {
            EnsurePreviewPlayer();
            if (Application.isPlaying && _previewPlayer != null) _previewPlayer.ResetPreviewState();
            Button exitButton = GetButton(transform, "ExitButton", "EXIT", new Vector2(520f, 318f), new Vector2(170f, 52f), QuitGame);
            if (exitButton != null && !_preferSceneLayout)
            {
                RectTransform exitRect = (RectTransform)exitButton.transform;
                exitRect.anchoredPosition = new Vector2(520f, 318f);
                exitRect.sizeDelta = new Vector2(170f, 52f);
            }
            TMP_Text title = GetText(transform, "Title", "Lobby", new Vector2(0f, -318f), new Vector2(320f, 58f), 42f, TextAlignmentOptions.Center);
            if (title != null) PlaceIfGenerated(title.rectTransform, transform, "Title", new Vector2(0f, -318f), new Vector2(320f, 58f));
            _songContentRoot = FindOrCreateSongContentRoot();
            if (_songContentRoot == null) return;
            if (!_preferSceneLayout) _songContentRoot.anchoredPosition = new Vector2(18f, _songContentRoot.anchoredPosition.y);

            SongDefinition[] songs = _songLibrary != null ? _songLibrary.Songs : new SongDefinition[0];
            _songIndex = songs.Length > 0 ? Mathf.Clamp(_songIndex, 0, songs.Length - 1) : 0;

            if (songs.Length == 0)
            {
                RefreshSelectedSongPanel(songs);
            }
            else
            {
                SelectSessionSongIfPresent(songs);
                Button previous = GetButton(transform, "PreviousSongButton", "<", new Vector2(78f, -18f), new Vector2(72f, 310f), PreviousSong);
                if (previous != null)
                {
                    PlaceIfGenerated((RectTransform)previous.transform, transform, "PreviousSongButton", new Vector2(78f, -18f), new Vector2(72f, 310f));
                    if (!_preferSceneLayout) ((RectTransform)previous.transform).anchoredPosition = new Vector2(192f, 19f);
                }
                Button next = GetButton(transform, "NextSongButton", ">", new Vector2(590f, -18f), new Vector2(72f, 310f), NextSong);
                if (next != null)
                {
                    PlaceIfGenerated((RectTransform)next.transform, transform, "NextSongButton", new Vector2(590f, -18f), new Vector2(72f, 310f));
                    if (!_preferSceneLayout) ((RectTransform)next.transform).anchoredPosition = new Vector2(511f, 19f);
                }
                RefreshSelectedSongPanel(songs);
            }

            ApplyOceanBlendVisualTheme();
        }

        private void BuildSelectedSongPanel(SongDefinition song, int index, int count)
        {
            _selectedSong = song;
            Transform parent = _songContentRoot != null ? _songContentRoot : transform;
            bool playable = song != null && song.IsPlayable;
            RectTransform info = EnsureContainer(parent, "SongInfoPanel", new Vector2(330f, 120f), new Vector2(430f, 250f));
            if (info == null) return;
            ConfigureThumbnail(info, song != null ? song.Thumbnail : null, playable);

            TMP_Text indexText = GetText(info, "SongIndex", $"{index}/{count}", new Vector2(0f, 106f), new Vector2(360f, 28f), 18f, TextAlignmentOptions.Center);
            if (indexText != null)
            {
                PlaceIfGenerated(indexText.rectTransform, info, "SongIndex", new Vector2(0f, 106f), new Vector2(360f, 28f));
                if (!_preferSceneVisuals) indexText.color = new Color(0.02f, 0.08f, 0.12f, 1f);
            }

            string songTitle = song != null ? song.EffectiveTitle : "No song";
            string composer = song != null ? song.EffectiveComposer : "";

            TMP_Text nameText = GetText(info, "SongName", songTitle, new Vector2(98f, 52f), new Vector2(190f, 54f), 23f, TextAlignmentOptions.Left);
            if (nameText != null)
            {
                PlaceIfGenerated(nameText.rectTransform, info, "SongName", new Vector2(98f, 52f), new Vector2(190f, 54f));
                ConfigureSongInfoText(nameText);
                if (!_preferSceneVisuals) nameText.color = new Color(0.02f, 0.08f, 0.12f, 1f);
            }

            TMP_Text composerText = GetOptionalText(info, "SongComposer", composer, new Vector2(98f, 12f), new Vector2(190f, 34f), 15f, TextAlignmentOptions.Left);
            if (composerText != null)
            {
                PlaceIfGenerated(composerText.rectTransform, info, "SongComposer", new Vector2(98f, 12f), new Vector2(190f, 34f));
                ConfigureSongInfoText(composerText);
                if (!_preferSceneVisuals) composerText.color = new Color(0.02f, 0.08f, 0.12f, 0.82f);
            }

            TMP_Text statusText = GetOptionalText(info, "SongStatus", playable ? "READY" : "ASSET MISSING", new Vector2(98f, -16f), new Vector2(190f, 24f), 16f, TextAlignmentOptions.Left);
            if (statusText != null)
            {
                PlaceIfGenerated(statusText.rectTransform, info, "SongStatus", new Vector2(98f, -16f), new Vector2(190f, 24f));
                ConfigureSongInfoText(statusText);
                if (!_preferSceneVisuals)
                {
                    statusText.color = playable ? new Color(0.03f, 0.34f, 0.28f, 1f) : new Color(0.76f, 0.16f, 0.18f, 1f);
                }
            }

            NormalSongRecord normal = PlayerProgress.GetNormalRecord(song.SongId);
            bool eternalUnlocked = PlayerProgress.IsEternalUnlocked(song.SongId);
            bool presentEternalUnlock = playable && eternalUnlocked && PlayerProgress.ConsumePendingEternalUnlockPresentation(song.SongId);
            string infoText =
                (playable ? $"Best {normal.BestScore:N0}" : "Add WAV + beatmap") + "\n" +
                $"Combo {normal.BestCombo}\n" +
                $"Rank {FormatRank(normal.BestRank)}";
            TMP_Text recordText = GetText(info, "SongRecord", infoText, new Vector2(0f, -72f), new Vector2(370f, 94f), 17f, TextAlignmentOptions.Center);
            if (recordText != null)
            {
                PlaceIfGenerated(recordText.rectTransform, info, "SongRecord", new Vector2(0f, -72f), new Vector2(370f, 94f));
                ConfigureSongInfoText(recordText);
                if (!_preferSceneVisuals) recordText.color = new Color(0.02f, 0.08f, 0.12f, 1f);
            }

            RectTransform play = EnsureContainer(parent, "SongPlayPanel", new Vector2(330f, -160f), new Vector2(430f, 138f));
            if (play == null) return;
            PlaceIfGenerated(play, parent, "SongPlayPanel", new Vector2(330f, -160f), new Vector2(430f, 138f));

            _previewToggleButton = EnsurePreviewButton(play, "PreviewToggleButton", "PREVIEW", new Vector2(0f, 44f), new Vector2(360f, 42f), ToggleSelectedPreview);
            if (_previewToggleButton != null)
            {
                PlaceIfGenerated((RectTransform)_previewToggleButton.transform, play, "PreviewToggleButton", new Vector2(0f, 44f), new Vector2(360f, 42f));
                ConfigurePreviewToggleButton(_previewToggleButton, song);
            }

            Button normalButton = GetButton(play, "NormalButton", playable ? "NORMAL" : "MISSING", new Vector2(-98f, -24f), new Vector2(168f, 56f), () => StartSong(song, AO.Core.PlayMode.Normal));
            if (normalButton != null)
            {
                PlaceIfGenerated((RectTransform)normalButton.transform, play, "NormalButton", new Vector2(-98f, -24f), new Vector2(168f, 56f));
                normalButton.interactable = playable;
            }

            Button eternalButton = GetButton(play, "EternalButton", playable && eternalUnlocked && !presentEternalUnlock ? "ETERNAL" : "LOCKED", new Vector2(98f, -24f), new Vector2(168f, 56f), () => StartSong(song, AO.Core.PlayMode.Eternal));
            if (eternalButton != null)
            {
                PlaceIfGenerated((RectTransform)eternalButton.transform, play, "EternalButton", new Vector2(98f, -24f), new Vector2(168f, 56f));
                eternalButton.interactable = playable && eternalUnlocked && !presentEternalUnlock;
            }

            Transform lockIcon = eternalButton != null ? eternalButton.transform.Find("EternalLockIcon") : null;
            if (lockIcon != null)
            {
                if (!_preferSceneVisuals || presentEternalUnlock)
                {
                    lockIcon.gameObject.SetActive(!(playable && eternalUnlocked) || presentEternalUnlock);
                }

                if (presentEternalUnlock) PlayEternalUnlockPresentation(lockIcon, eternalButton);
            }
            else if (presentEternalUnlock && eternalButton != null)
            {
                eternalButton.interactable = true;
                SetButtonLabel(eternalButton, "ETERNAL");
            }
        }

        private void SelectSessionSongIfPresent(SongDefinition[] songs)
        {
            if (_session == null || songs == null || songs.Length == 0) return;
            if (string.IsNullOrEmpty(_session.SelectedSongId)) return;

            for (int i = 0; i < songs.Length; i++)
            {
                if (songs[i] != null && songs[i].SongId == _session.SelectedSongId)
                {
                    _songIndex = i;
                    return;
                }
            }
        }

        private void PreviousSong()
        {
            SelectSongDelta(-1);
        }

        private void NextSong()
        {
            SelectSongDelta(1);
        }

        private void SelectSongDelta(int delta)
        {
            SongDefinition[] songs = _songLibrary != null ? _songLibrary.Songs : new SongDefinition[0];
            if (songs.Length <= 0) return;

            _songIndex = (_songIndex + delta) % songs.Length;
            if (_songIndex < 0) _songIndex += songs.Length;
            RefreshSelectedSongPanel(songs);
        }

        private void RefreshSelectedSongPanel(SongDefinition[] songs)
        {
            if (_songContentRoot == null) _songContentRoot = FindOrCreateSongContentRoot();
            if (_songContentRoot == null) return;
            bool continuePreview = Application.isPlaying && _previewPlayer != null && _previewPlayer.IsPlaybackRequested;

            if (songs == null || songs.Length == 0)
            {
                _selectedSong = null;
                if (Application.isPlaying && _previewPlayer != null) _previewPlayer.ResetPreviewState();
                SetDirectChildActive(_songContentRoot, "SongInfoPanel", false);
                SetDirectChildActive(_songContentRoot, "SongPlayPanel", false);
                TMP_Text noSongs = GetText(_songContentRoot, "NoSongs", "No songs in Song Library", new Vector2(338f, 80f), new Vector2(420f, 80f), 28f, TextAlignmentOptions.Center);
                if (noSongs == null)
                {
                    Debug.LogError("[LobbyScreenController] SongLibrary is empty and required scene text 'NoSongs' is missing.", this);
                }
                return;
            }

            SetDirectChildActive(_songContentRoot, "NoSongs", false);
            _songIndex = Mathf.Clamp(_songIndex, 0, songs.Length - 1);
            BuildSelectedSongPanel(songs[_songIndex], _songIndex + 1, songs.Length);
            if (continuePreview) _previewPlayer.PlayPreview(songs[_songIndex]);
            if (_previewToggleButton != null) ConfigurePreviewToggleButton(_previewToggleButton, _selectedSong);
            ApplyOceanBlendVisualTheme();
        }

        private void ApplyOceanBlendVisualTheme()
        {
            if (_preferSceneVisuals) return;

            Transform title = transform.Find("Title");
            if (title != null)
            {
                Transform titleFrame = title.Find("GameObject");
                ApplyThemeSprite(titleFrame, _titleFrameSprite);
            }
            ApplyThemeSprite(transform.Find("ExitButton"), _topUtilityButtonSprite);
            ApplyThemeSprite(transform.Find("PreviousSongButton"), _previousArrowSprite);
            ApplyThemeSprite(transform.Find("NextSongButton"), _nextArrowSprite);

            Transform content = transform.Find("SongContentRoot");
            if (content == null) return;

            Transform info = content.Find("SongInfoPanel");
            if (info != null)
            {
                // SongInfoPanel is the content container, not the visual frame. Applying
                // a sprite here creates a second small frame over the song text.
                Image accidentalInnerFrame = info.GetComponent<Image>();
                if (accidentalInnerFrame != null &&
                    (accidentalInnerFrame.sprite == _songCardFrameSprite || accidentalInnerFrame.sprite == _songCardGlowSprite))
                {
                    accidentalInnerFrame.enabled = false;
                }

                ApplyThemeSprite(info.Find("SelectedGlow"), _songCardFrameSprite);
                Transform thumbnailFrame = info.Find("ThumbnailFrame/Frame");
                ApplyThemeSprite(thumbnailFrame, _thumbnailFrameSprite);
            }

            Transform play = content.Find("SongPlayPanel");
            if (play == null) return;

            ApplyThemeSprite(play.Find("NormalButton"), _normalModeSprite);
            Transform eternal = play.Find("EternalButton");
            ApplyThemeSprite(eternal, _eternalModeSprite);
            if (eternal != null) ApplyThemeSprite(eternal.Find("EternalLockIcon"), _eternalLockSprite);
        }

        private static void ApplyThemeSprite(Transform target, Sprite sprite)
        {
            if (target == null || sprite == null) return;
            Image image = target.GetComponent<Image>();
            if (image == null) return;

            image.enabled = true;
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
        }

        private RectTransform FindOrCreateSongContentRoot()
        {
            Transform existing = transform.Find("SongContentRoot");
            if (existing != null && existing.TryGetComponent(out RectTransform existingRect))
            {
                if (!_preferSceneLayout) existing.gameObject.SetActive(true);
                return existingRect;
            }

            if (!_allowRuntimeUiCreation)
            {
                ReportMissing("RectTransform", transform, "SongContentRoot");
                return null;
            }

            GameObject go = new GameObject("SongContentRoot", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        private void StartSong(SongDefinition song, AO.Core.PlayMode mode)
        {
            if (song == null || _session == null) return;
            if (!song.IsPlayable)
            {
                Debug.LogWarning($"[LobbyScreenController] Song '{song.DisplayName}' is missing BGM or beatmap.");
                return;
            }

            if (mode == AO.Core.PlayMode.Eternal && !PlayerProgress.IsEternalUnlocked(song.SongId)) return;

            if (_previewPlayer != null) _previewPlayer.ResetPreviewState();
            _session.SetMode(mode);
            _session.SelectSong(song.SongId, song.DisplayName);
            _session.SetSpeed(PlayerProgress.GetPlaybackSpeed(song), PlayerProgress.GetNoteSpeed(song));
            _session.SetAudioOffset(PlayerProgress.GetAudioOffset(song));
            SceneTransition.GoToGameplay();
        }

        private static string FormatRank(string rank)
        {
            return string.IsNullOrWhiteSpace(rank) ? "-" : rank;
        }

        private static void ConfigureSongInfoText(TMP_Text text)
        {
            if (text == null) return;

            text.richText = true;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        private void PlayEternalUnlockPresentation(Transform lockIcon, Button eternalButton)
        {
            if (!Application.isPlaying || lockIcon == null) return;
            if (_eternalUnlockRoutine != null) StopCoroutine(_eternalUnlockRoutine);
            _eternalUnlockRoutine = StartCoroutine(PlayEternalUnlockPresentationRoutine(lockIcon, eternalButton));
        }

        private IEnumerator PlayEternalUnlockPresentationRoutine(Transform lockIcon, Button eternalButton)
        {
            RectTransform iconRect = lockIcon as RectTransform;
            Image iconImage = lockIcon.GetComponent<Image>();
            Vector2 startAnchoredPosition = iconRect != null ? iconRect.anchoredPosition : Vector2.zero;
            Vector3 startScale = lockIcon.localScale;
            Quaternion startRotation = lockIcon.localRotation;
            Color startColor = iconImage != null ? iconImage.color : Color.white;
            if (startColor.a <= 0.05f) startColor.a = 1f;

            lockIcon.gameObject.SetActive(true);
            if (iconImage != null) iconImage.color = startColor;
            if (eternalButton != null)
            {
                eternalButton.interactable = false;
                SetButtonLabel(eternalButton, "LOCKED");
            }

            float delayElapsed = 0f;
            while (delayElapsed < _eternalUnlockPresentationDelay && lockIcon != null)
            {
                delayElapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                if (iconRect != null) iconRect.anchoredPosition = startAnchoredPosition;
                lockIcon.localScale = startScale;
                lockIcon.localRotation = startRotation;
                if (iconImage != null) iconImage.color = startColor;
                yield return null;
            }

            SpawnUnlockParticlePrefab(lockIcon);
            UnlockShard[] shards = CreateUnlockShards(iconRect, iconImage);

            float elapsed = 0f;
            while (elapsed < _eternalUnlockDuration && lockIcon != null)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _eternalUnlockDuration));
                float ease = 1f - Mathf.Pow(1f - t, 3f);

                if (iconRect != null) iconRect.anchoredPosition = startAnchoredPosition + _eternalUnlockIconDrift * ease;
                lockIcon.localScale = Vector3.Lerp(startScale, startScale * _eternalUnlockEndScale, ease);
                lockIcon.localRotation = startRotation * Quaternion.Euler(0f, 0f, _eternalUnlockSpinDegrees * ease);

                if (iconImage != null)
                {
                    Color color = startColor;
                    color.a = Mathf.Lerp(startColor.a, 0f, ease);
                    iconImage.color = color;
                }

                UpdateUnlockShards(shards, startAnchoredPosition, t);
                yield return null;
            }

            DestroyUnlockShards(shards);

            if (lockIcon != null)
            {
                if (iconRect != null) iconRect.anchoredPosition = startAnchoredPosition;
                lockIcon.localScale = startScale;
                lockIcon.localRotation = startRotation;
                if (iconImage != null) iconImage.color = startColor;
                lockIcon.gameObject.SetActive(false);
            }

            if (eternalButton != null)
            {
                eternalButton.interactable = true;
                SetButtonLabel(eternalButton, "ETERNAL");
            }

            _eternalUnlockRoutine = null;
        }

        private void SpawnUnlockParticlePrefab(Transform lockIcon)
        {
            if (_eternalUnlockParticlePrefab == null || lockIcon == null) return;

            GameObject fx = Instantiate(_eternalUnlockParticlePrefab, lockIcon.position, lockIcon.rotation);
            fx.name = "EternalUnlockParticles";
            Destroy(fx, _eternalUnlockParticleLifetime);
        }

        private UnlockShard[] CreateUnlockShards(RectTransform iconRect, Image iconImage)
        {
            if (iconRect == null || iconImage == null || iconImage.sprite == null || _eternalUnlockShardCount <= 0)
            {
                return System.Array.Empty<UnlockShard>();
            }

            Transform parent = iconRect.parent;
            UnlockShard[] shards = new UnlockShard[_eternalUnlockShardCount];
            for (int i = 0; i < shards.Length; i++)
            {
                GameObject go = new GameObject("EternalUnlockShard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(parent, false);

                RectTransform rect = go.GetComponent<RectTransform>();
                rect.anchorMin = iconRect.anchorMin;
                rect.anchorMax = iconRect.anchorMax;
                rect.pivot = iconRect.pivot;
                rect.anchoredPosition = iconRect.anchoredPosition;
                rect.sizeDelta = _eternalUnlockShardSize;
                rect.localScale = Vector3.one;

                Image image = go.GetComponent<Image>();
                image.sprite = iconImage.sprite;
                image.material = iconImage.material;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.color = iconImage.color;

                float angle = (360f / shards.Length) * i + 17f;
                float distanceT = ((i * 37) % 100) / 100f;
                float distance = Mathf.Lerp(_eternalUnlockShardDistance.x, _eternalUnlockShardDistance.y, distanceT);
                Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                shards[i] = new UnlockShard(rect, image, direction * distance, angle);
            }

            return shards;
        }

        private static void UpdateUnlockShards(UnlockShard[] shards, Vector2 origin, float t)
        {
            if (shards == null) return;

            float ease = 1f - Mathf.Pow(1f - t, 3f);
            for (int i = 0; i < shards.Length; i++)
            {
                if (shards[i].Rect == null || shards[i].Image == null) continue;

                Vector2 gravity = Vector2.down * (24f * t * t);
                shards[i].Rect.anchoredPosition = origin + shards[i].Offset * ease + gravity;
                shards[i].Rect.localRotation = Quaternion.Euler(0f, 0f, shards[i].SpinDegrees * ease * 2f);
                shards[i].Rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.25f, ease);

                Color color = shards[i].Image.color;
                color.a = Mathf.Lerp(1f, 0f, ease);
                shards[i].Image.color = color;
            }
        }

        private static void DestroyUnlockShards(UnlockShard[] shards)
        {
            if (shards == null) return;

            for (int i = 0; i < shards.Length; i++)
            {
                if (shards[i].Rect != null) Destroy(shards[i].Rect.gameObject);
            }
        }

        private static void SetButtonLabel(Button button, string text)
        {
            if (button == null) return;

            Transform labelTransform = button.transform.Find("Label");
            TMP_Text label = labelTransform != null
                ? labelTransform.GetComponent<TMP_Text>()
                : button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = text;
        }

        private readonly struct UnlockShard
        {
            public readonly RectTransform Rect;
            public readonly Image Image;
            public readonly Vector2 Offset;
            public readonly float SpinDegrees;

            public UnlockShard(RectTransform rect, Image image, Vector2 offset, float spinDegrees)
            {
                Rect = rect;
                Image = image;
                Offset = offset;
                SpinDegrees = spinDegrees;
            }
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ConfigureThumbnail(RectTransform parent, Sprite sprite, bool playable)
        {
            RectTransform frame = EnsureContainer(parent, "ThumbnailFrame", new Vector2(-104f, 36f), new Vector2(162f, 92f));
            if (frame == null) return;
            frame.localScale = new Vector3(2.15f, 2f, 1f);

            RectTransform imageRect = EnsurePanel(frame, "ThumbnailImage", Vector2.zero, new Vector2(152f, 82f), new Color(0.09f, 0.28f, 0.34f, 1f));
            if (imageRect == null) return;

            RectTransform viewport = EnsureThumbnailViewport(frame);
            if (viewport != null && imageRect.parent != viewport)
            {
                imageRect.SetParent(viewport, false);
            }

            if (viewport != null)
            {
                imageRect.anchorMin = new Vector2(0.5f, 0.5f);
                imageRect.anchorMax = new Vector2(0.5f, 0.5f);
                imageRect.pivot = new Vector2(0.5f, 0.5f);
                imageRect.anchoredPosition = Vector2.zero;
            }

            Image image = imageRect.GetComponent<Image>();
            if (image == null)
            {
                ReportMissing("Image", imageRect.parent, imageRect.name);
                return;
            }
            image.sprite = sprite;
            image.preserveAspect = false;
            image.raycastTarget = false;
            image.color = sprite != null ? Color.white : (playable ? new Color(0.09f, 0.28f, 0.34f, 1f) : new Color(0.18f, 0.19f, 0.2f, 1f));

            AspectRatioFitter fitter = imageRect.GetComponent<AspectRatioFitter>();
            if (fitter == null) fitter = imageRect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = sprite != null ? AspectRatioFitter.AspectMode.EnvelopeParent : AspectRatioFitter.AspectMode.None;
            if (sprite != null && sprite.rect.height > 0f)
            {
                fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            }

            RectTransform overlay = FindRect(frame, "Frame");
            if (overlay != null)
            {
                Image overlayImage = overlay.GetComponent<Image>();
                if (overlayImage != null)
                {
                    overlayImage.raycastTarget = false;
                    overlayImage.maskable = false;
                }
                overlay.SetAsLastSibling();
            }
        }

        private static RectTransform EnsureThumbnailViewport(RectTransform frame)
        {
            RectTransform viewport = FindRect(frame, "ThumbnailViewport");
            if (viewport == null)
            {
                GameObject viewportObject = new GameObject("ThumbnailViewport", typeof(RectTransform), typeof(ChamferedMaskGraphic), typeof(Mask));
                viewportObject.transform.SetParent(frame, false);
                viewport = viewportObject.GetComponent<RectTransform>();
            }

            viewport.anchorMin = new Vector2(0.5f, 0.5f);
            viewport.anchorMax = new Vector2(0.5f, 0.5f);
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.anchoredPosition = Vector2.zero;
            viewport.sizeDelta = new Vector2(184f, 98f);
            viewport.localRotation = Quaternion.identity;
            viewport.localScale = Vector3.one;
            viewport.SetAsFirstSibling();

            RectMask2D rectangularMask = viewport.GetComponent<RectMask2D>();
            if (rectangularMask != null) rectangularMask.enabled = false;

            ChamferedMaskGraphic maskShape = viewport.GetComponent<ChamferedMaskGraphic>();
            if (maskShape == null) maskShape = viewport.gameObject.AddComponent<ChamferedMaskGraphic>();
            maskShape.CornerCut = 9f;
            maskShape.color = Color.white;
            maskShape.raycastTarget = false;

            Mask mask = viewport.GetComponent<Mask>();
            if (mask == null) mask = viewport.gameObject.AddComponent<Mask>();
            mask.enabled = true;
            mask.showMaskGraphic = false;
            return viewport;
        }

        private void ToggleSelectedPreview()
        {
            if (_selectedSong == null) return;

            EnsurePreviewPlayer();
            if (_previewPlayer == null) return;
            _previewPlayer.TogglePreview(_selectedSong);
            ConfigurePreviewToggleButton(_previewToggleButton, _selectedSong);
        }

        private void ConfigurePreviewToggleButton(Button button, SongDefinition song)
        {
            if (button == null) return;

            bool hasPreview = song != null && song.PreviewClip != null;
            bool previewRequested = _previewPlayer != null && _previewPlayer.IsPlaybackRequested;
            button.interactable = hasPreview;

            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                if (!_preferSceneVisuals)
                {
                    buttonImage.color = hasPreview
                        ? new Color(0.03f, 0.19f, 0.25f, 0.88f)
                        : new Color(0.07f, 0.08f, 0.09f, 0.48f);
                }
                buttonImage.raycastTarget = true;
            }

            Transform labelTransform = button.transform.Find("Label");
            bool labelExisted = labelTransform != null;
            TMP_Text label = labelExisted ? labelTransform.GetComponent<TMP_Text>() : null;
            if (label != null)
            {
                label.text = hasPreview ? (previewRequested ? "PAUSE" : "PREVIEW") : "NO PREVIEW";
                if (!_preferSceneVisuals || !labelExisted)
                {
                    label.fontSize = 20f;
                    label.fontSizeMax = 20f;
                    label.fontSizeMin = 12f;
                    label.alignment = TextAlignmentOptions.Center;
                    label.color = hasPreview ? new Color(0.86f, 1f, 0.96f, 1f) : new Color(0.68f, 0.78f, 0.8f, 0.72f);
                }
                PlaceIfGenerated(label.rectTransform, button.transform, "Label", new Vector2(18f, 0f), new Vector2(230f, 34f), labelExisted);
            }

            Sprite iconSprite = previewRequested ? _previewPauseSprite : _previewPlaySprite;
            RectTransform existingIcon = FindRect(button.transform, "PreviewIcon");
            if (existingIcon == null) return;

            Image icon = existingIcon.GetComponent<Image>();
            if (icon != null)
            {
                if (!_preferSceneVisuals)
                {
                    icon.sprite = iconSprite;
                    icon.preserveAspect = true;
                    icon.color = iconSprite != null && hasPreview ? Color.white : new Color(1f, 1f, 1f, 0f);
                    icon.gameObject.SetActive((icon.sprite != null || iconSprite != null) && hasPreview);
                }

                icon.raycastTarget = false;
            }
        }

        private RectTransform EnsurePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            return EnsurePanel(parent, name, anchoredPosition, size, color, true);
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

            if (!_preferSceneLayout) rect.gameObject.SetActive(true);
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

            if (!_preferSceneLayout) rect.gameObject.SetActive(true);
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
                text.textWrappingMode = TextWrappingModes.Normal;
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.alignment = alignment;
            }

            return text;
        }

        private TMP_Text GetOptionalText(Transform parent, string name, string value, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            RectTransform rect = FindRect(parent, name);
            if (rect == null) return null;

            if (!_preferSceneLayout) rect.gameObject.SetActive(true);
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
                text.textWrappingMode = TextWrappingModes.Normal;
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.alignment = alignment;
            }

            return text;
        }

        private RectTransform EnsureContainer(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            bool existed = FindRect(parent, name) != null;
            RectTransform rect;
            if (existed)
            {
                rect = FindRect(parent, name);
                if (!_preferSceneLayout) rect.gameObject.SetActive(true);
            }
            else
            {
                if (!_allowRuntimeUiCreation)
                {
                    ReportMissing("RectTransform", parent, name);
                    return null;
                }

                rect = RuntimeUiFactory.EnsureContainer(parent, name, anchoredPosition, size);
            }

            PlaceIfGenerated(rect, parent, name, anchoredPosition, size, existed);
            return rect;
        }

        private Button EnsurePreviewButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction onClick)
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

            if (!_preferSceneLayout) rect.gameObject.SetActive(true);
            Image image = rect.GetComponent<Image>();
            if (image == null && _allowRuntimeUiCreation) image = rect.gameObject.AddComponent<Image>();
            if (image != null) image.raycastTarget = true;

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

            if (button.targetGraphic == null && image != null) button.targetGraphic = image;

            button.onClick.RemoveAllListeners();
            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }

        private RectTransform EnsurePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color, bool activateExisting)
        {
            bool existed = FindRect(parent, name) != null;
            RectTransform rect;
            if (existed)
            {
                rect = FindRect(parent, name);
                if (activateExisting && !_preferSceneLayout) rect.gameObject.SetActive(true);
                if (rect.GetComponent<Image>() == null)
                {
                    if (!_allowRuntimeUiCreation)
                    {
                        ReportMissing("Image", parent, name);
                        return null;
                    }

                    rect.gameObject.AddComponent<Image>();
                }
            }
            else
            {
                if (!_allowRuntimeUiCreation)
                {
                    ReportMissing("RectTransform", parent, name);
                    return null;
                }

                rect = RuntimeUiFactory.EnsurePanel(parent, name, anchoredPosition, size, color);
            }

            if (!_preferSceneVisuals || !existed)
            {
                Image image = rect.GetComponent<Image>();
                if (image != null) image.color = color;
            }
            PlaceIfGenerated(rect, parent, name, anchoredPosition, size, existed);
            return rect;
        }

        private static void SetDirectChildActive(Transform parent, string name, bool active)
        {
            Transform child = FindRecursive(parent, name);
            if (child != null) child.gameObject.SetActive(active);
        }

        private void PlaceIfGenerated(RectTransform rect, Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            PlaceIfGenerated(rect, parent, name, anchoredPosition, size, FindRect(parent, name) != null);
        }

        private void PlaceIfGenerated(RectTransform rect, Transform parent, string name, Vector2 anchoredPosition, Vector2 size, bool existed)
        {
            if (_preferSceneLayout && existed) return;
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
            Debug.LogError($"[LobbyScreenController] Required Lobby scene object '{name}' ({expectedType}) under '{parentName}' is missing or misconfigured. Runtime UI creation is disabled, so no replacement was generated.", this);
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

        private void EnsurePreviewPlayer()
        {
            if (_previewPlayer != null) return;

            _previewPlayer = GetComponent<LobbyPreviewPlayer>();
            if (_previewPlayer == null)
            {
                if (_allowRuntimeUiCreation)
                {
                    _previewPlayer = gameObject.AddComponent<LobbyPreviewPlayer>();
                }
                else
                {
                    Debug.LogError("[LobbyScreenController] Required LobbyPreviewPlayer component is missing. Runtime component creation is disabled.", this);
                }
            }
        }
    }
}
