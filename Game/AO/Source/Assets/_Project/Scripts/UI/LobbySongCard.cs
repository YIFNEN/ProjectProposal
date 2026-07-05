using AO.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AO.UI
{
    [DisallowMultipleComponent]
    public class LobbySongCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private SongDefinition _song;
        private GameSession _session;
        private LobbyPreviewPlayer _previewPlayer;
        private RectTransform _rect;
        private Vector3 _baseScale = Vector3.one;
        [SerializeField] private bool _preferSceneVisuals = true;
        [SerializeField] private bool _allowRuntimeUiCreation = false;

        public void Configure(SongDefinition song, GameSession session, LobbyPreviewPlayer previewPlayer)
        {
            _song = song;
            _session = session;
            _previewPlayer = previewPlayer;
            _rect = transform as RectTransform;
            _baseScale = transform.localScale;
            Build();
        }

        private void Build()
        {
            if (_allowRuntimeUiCreation)
            {
                RuntimeUiFactory.ClearChildren(transform);
            }

            string name = _song != null ? _song.DisplayName : "Empty Slot";
            GetText("SongName", name, new Vector2(0f, 118f), new Vector2(268f, 62f), 27f, TextAlignmentOptions.Center);

            if (_song == null)
            {
                GetText("Empty", "No song", new Vector2(0f, 20f), new Vector2(244f, 46f), 22f, TextAlignmentOptions.Center);
                return;
            }

            NormalSongRecord normal = PlayerProgress.GetNormalRecord(_song.SongId);
            EternalSongRecord eternal = PlayerProgress.GetEternalRecord(_song.SongId);
            bool eternalUnlocked = PlayerProgress.IsEternalUnlocked(_song.SongId);

            string record =
                $"NORMAL\n" +
                $"Best {normal.BestScore:N0}\n" +
                $"Combo {normal.BestCombo}\n" +
                $"Acc {normal.BestAccuracy * 100f:F1}% / {normal.BestRank}";
            GetText("NormalRecord", record, new Vector2(0f, 42f), new Vector2(260f, 96f), 18f, TextAlignmentOptions.Center);

            string eternalText = eternalUnlocked
                ? $"ETERNAL READY\nBest {eternal.BestScore:N0}"
                : "ETERNAL LOCKED\nNormal S required";
            GetText("EternalRecord", eternalText, new Vector2(0f, -40f), new Vector2(260f, 54f), 16f, TextAlignmentOptions.Center);

            Button normalButton = GetButton("NormalButton", _song.IsPlayable ? "NORMAL" : "MISSING", new Vector2(-67f, -122f), new Vector2(124f, 46f), () => StartSong(AO.Core.PlayMode.Normal));
            if (normalButton != null) normalButton.interactable = _song.IsPlayable;

            Button eternalButton = GetButton("EternalButton", eternalUnlocked ? "ETERNAL" : "LOCKED", new Vector2(67f, -122f), new Vector2(124f, 46f), () => StartSong(AO.Core.PlayMode.Eternal));
            if (eternalButton != null) eternalButton.interactable = _song.IsPlayable && eternalUnlocked;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHover(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHover(false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            SetHover(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetHover(false);
        }

        private void SetHover(bool hovering)
        {
            transform.localScale = _baseScale * (hovering ? 1.04f : 1f);
            Image image = GetComponent<Image>();
            if (image != null)
            {
                image.color = hovering
                    ? new Color(0.07f, 0.24f, 0.3f, 0.92f)
                    : new Color(0.04f, 0.13f, 0.18f, 0.78f);
            }
        }

        private void StartSong(AO.Core.PlayMode mode)
        {
            if (_song == null || _session == null || !_song.IsPlayable) return;
            if (_previewPlayer != null) _previewPlayer.StopPreview(immediate: true);
            _session.SetMode(mode);
            _session.SelectSong(_song.SongId, _song.DisplayName);
            _session.SetSpeed(PlayerProgress.GetPlaybackSpeed(_song), PlayerProgress.GetNoteSpeed(_song));
            _session.SetAudioOffset(PlayerProgress.GetAudioOffset(_song));
            SceneTransition.GoToGameplay();
        }

        private TMP_Text GetText(string name, string value, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            RectTransform rect = FindRect(transform, name);
            if (rect == null)
            {
                if (!_allowRuntimeUiCreation)
                {
                    ReportMissing(name, "TMP_Text");
                    return null;
                }

                return RuntimeUiFactory.Text(transform, name, value, position, size, fontSize, alignment);
            }

            TMP_Text text = rect.GetComponent<TMP_Text>();
            if (text == null) text = rect.GetComponentInChildren<TMP_Text>(true);
            if (text == null)
            {
                if (!_allowRuntimeUiCreation)
                {
                    ReportMissing(name, "TMP_Text");
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

        private Button GetButton(string name, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform rect = FindRect(transform, name);
            if (rect == null)
            {
                if (!_allowRuntimeUiCreation)
                {
                    ReportMissing(name, "Button");
                    return null;
                }

                return RuntimeUiFactory.Button(transform, name, label, position, size, onClick);
            }

            Button button = rect.GetComponent<Button>();
            if (button == null)
            {
                if (!_allowRuntimeUiCreation)
                {
                    ReportMissing(name, "Button");
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

        private void ReportMissing(string name, string expectedType)
        {
            Debug.LogError($"[LobbySongCard] Required scene-authored card object '{name}' ({expectedType}) is missing or misconfigured. Runtime UI creation is disabled, so no replacement was generated.", this);
        }
    }
}
