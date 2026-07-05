using AO.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AO.UI
{
    public class ResultScreenController : MonoBehaviour
    {
        [SerializeField] private GameSession _session;
        [SerializeField] private bool _preferSceneLayout = true;
        [SerializeField] private bool _preferSceneVisuals = true;
        [SerializeField] private bool _allowRuntimeUiCreation = false;

        private void Start()
        {
            MenuSceneWorld.EnsureCommonBackdrop(MenuSceneCharacterMode.ResultLeft);
            Build();
        }

        public void Configure(GameSession session)
        {
            _session = session;
        }

        public void RebuildForEditor()
        {
            if (!_allowRuntimeUiCreation)
            {
                Debug.LogError("[ResultScreenController] RebuildForEditor is disabled because runtime UI creation is off. Edit the Result scene objects directly.", this);
                return;
            }

            RuntimeUiFactory.ClearChildren(transform);
            Build();
        }

        private void Build()
        {
            SessionResult result = _session != null ? _session.LastResult : default;
            RefreshDecorations(result);
            if (!_preferSceneLayout) AlignStatPanelFrame(transform);
            bool eternal = result.Mode == AO.Core.PlayMode.Eternal;
            bool cleared = result.Status == SessionResultStatus.Cleared;
            string title = eternal ? "Eternal Result" : (cleared ? "Clear" : "Result");

            TMP_Text titleText = GetText(transform, "Title", title, new Vector2(0f, 272f), new Vector2(820f, 72f), 52f, TextAlignmentOptions.Center);
            if (titleText != null) PlaceIfGenerated(titleText.rectTransform, transform, "Title", new Vector2(0f, 272f), new Vector2(820f, 72f));

            TMP_Text songText = GetOptionalText(transform, "SongName", string.IsNullOrWhiteSpace(result.SongName) ? "No song result" : result.SongName, new Vector2(0f, 218f), new Vector2(820f, 34f), 24f, TextAlignmentOptions.Center);
            if (songText != null) PlaceIfGenerated(songText.rectTransform, transform, "SongName", new Vector2(0f, 218f), new Vector2(820f, 34f));

            if (eternal)
            {
                RuntimeUiFactory.SetDirectChildActive(transform, "Rank", false);
                RuntimeUiFactory.SetDirectChildActive(transform, "Unlock", false);
                string detailStats = BuildDetailStats(result);
                bool appendDetailsToMainStats = !HasDetailTextTargets(transform);
                string eternalStats =
                    $"Score<pos=150>{result.Score:N0}\n" +
                    $"Combo<pos=150>{result.MaxCombo}\n" +
                    $"Accuracy<pos=150>{result.WeightedAccuracy * 100f:F1}%\n" +
                    $"Time<pos=150>{FormatTime(result.DurationSeconds)}";
                if (appendDetailsToMainStats) eternalStats += $"\n{detailStats}";
                TMP_Text statsText = GetText(transform, "MainStats", eternalStats, new Vector2(-30f, 38f), new Vector2(540f, 210f), 32f, TextAlignmentOptions.Left);
                if (statsText != null)
                {
                    PlaceIfGenerated(statsText.rectTransform, transform, "MainStats", new Vector2(-30f, 38f), new Vector2(540f, 210f));
                    if (!_preferSceneVisuals) ConfigureDenseStatsText(statsText, 25f, 14f, TextAlignmentOptions.Left);
                    if (!_preferSceneLayout) AlignMainStats(statsText.rectTransform);
                }
            }
            else
            {
                string rank = FormatRank(result.Rank);
                TMP_Text rankText = GetText(transform, "Rank", rank, new Vector2(-312f, 28f), new Vector2(210f, 210f), 128f, TextAlignmentOptions.Center);
                if (rankText != null)
                {
                    PlaceIfGenerated(rankText.rectTransform, transform, "Rank", new Vector2(-312f, 28f), new Vector2(210f, 210f));
                    if (!_preferSceneVisuals)
                    {
                        rankText.enableAutoSizing = true;
                        rankText.fontSize = 112f;
                        rankText.fontSizeMin = 60f;
                        rankText.fontSizeMax = 112f;
                    }
                    if (!_preferSceneLayout) AlignRankDisplay(rankText.rectTransform);
                }

                string detailStats = BuildDetailStats(result);
                bool appendDetailsToMainStats = !HasDetailTextTargets(transform);
                string normalStats =
                    $"Score<pos=150>{result.Score:N0}\n" +
                    $"Rank<pos=150>{rank}\n" +
                    $"Combo<pos=150>{result.MaxCombo}\n" +
                    $"Accuracy<pos=150>{result.WeightedAccuracy * 100f:F1}%";
                if (appendDetailsToMainStats) normalStats += $"\n{detailStats}";
                TMP_Text statsText = GetText(transform, "MainStats", normalStats, new Vector2(130f, 52f), new Vector2(520f, 190f), 32f, TextAlignmentOptions.Left);
                if (statsText != null)
                {
                    PlaceIfGenerated(statsText.rectTransform, transform, "MainStats", new Vector2(130f, 52f), new Vector2(520f, 190f));
                    if (!_preferSceneVisuals) ConfigureDenseStatsText(statsText, 25f, 14f, TextAlignmentOptions.Left);
                    if (!_preferSceneLayout) AlignMainStats(statsText.rectTransform);
                }
            }

            string judgementBreakdown = BuildJudgementBreakdown(result);
            bool hasJudgementBreakdown = FindRect(transform, "JudgementBreakdown") != null;

            TMP_Text breakdownText = GetOptionalText(transform, "JudgementBreakdown", judgementBreakdown, new Vector2(0f, -126f), new Vector2(760f, 100f), 24f, TextAlignmentOptions.Left);
            if (breakdownText != null)
            {
                PlaceIfGenerated(breakdownText.rectTransform, transform, "JudgementBreakdown", new Vector2(0f, -126f), new Vector2(760f, 100f));
                if (!_preferSceneVisuals) ConfigureDenseStatsText(breakdownText, 25f, 14f, TextAlignmentOptions.Left);
                if (!_preferSceneLayout)
                {
                    breakdownText.rectTransform.anchoredPosition = new Vector2(260f, -51f);
                    breakdownText.rectTransform.sizeDelta = new Vector2(440f, breakdownText.rectTransform.sizeDelta.y);
                }
            }

            RuntimeUiFactory.SetDirectChildActive(transform, "HandRangeStats", false);

            RuntimeUiFactory.SetDirectChildActive(transform, "UnlockBadgeFrame", false);
            RuntimeUiFactory.SetDirectChildActive(transform, "Unlock", false);

            Button retry = GetButton(transform, "RetryButton", "RETRY", new Vector2(-120f, -264f), new Vector2(210f, 64f), SceneTransition.GoToGameplay);
            if (retry != null) PlaceIfGenerated((RectTransform)retry.transform, transform, "RetryButton", new Vector2(-120f, -264f), new Vector2(210f, 64f));

            Button lobby = GetButton(transform, "LobbyButton", "LOBBY", new Vector2(120f, -264f), new Vector2(210f, 64f), SceneTransition.GoToLobby);
            if (lobby != null) PlaceIfGenerated((RectTransform)lobby.transform, transform, "LobbyButton", new Vector2(120f, -264f), new Vector2(210f, 64f));

            if (!_preferSceneLayout || !_preferSceneVisuals) MatchRetryToLobby(retry, lobby);
        }

        private static void AlignMainStats(RectTransform rect)
        {
            if (rect == null) return;

            rect.anchoredPosition = new Vector2(260f, 53f);
            rect.sizeDelta = new Vector2(440f, rect.sizeDelta.y);
        }

        private static void AlignStatPanelFrame(Transform parent)
        {
            RectTransform frameRect = FindRect(parent, "StatPanelFrame");
            if (frameRect == null) return;

            frameRect.anchoredPosition = new Vector2(172f, 15f);
            frameRect.sizeDelta = new Vector2(590f, 390f);
        }

        private static void AlignRankDisplay(RectTransform rankTextRect)
        {
            Vector2 center = new Vector2(-275f, 15f);
            Vector2 size = new Vector2(370f, 370f);

            if (rankTextRect != null)
            {
                rankTextRect.anchoredPosition = center;
                rankTextRect.sizeDelta = size;
            }

            RectTransform ringRect = FindRect(rankTextRect != null ? rankTextRect.parent : null, "RankRingImage");
            if (ringRect == null) return;

            ringRect.anchoredPosition = center;
            ringRect.sizeDelta = size;

            RectTransform clearGlowRect = FindRect(rankTextRect != null ? rankTextRect.parent : null, "ClearGlow");
            if (clearGlowRect != null) clearGlowRect.anchoredPosition = center;
        }

        private static void MatchRetryToLobby(Button retry, Button lobby)
        {
            if (retry == null || lobby == null) return;

            RectTransform retryRect = retry.transform as RectTransform;
            RectTransform lobbyRect = lobby.transform as RectTransform;
            if (retryRect != null && lobbyRect != null)
            {
                if (lobbyRect.sizeDelta.y < 72f) lobbyRect.sizeDelta = new Vector2(lobbyRect.sizeDelta.x, 72f);
                retryRect.sizeDelta = lobbyRect.sizeDelta;
                retryRect.anchoredPosition = new Vector2(-Mathf.Abs(lobbyRect.anchoredPosition.x), lobbyRect.anchoredPosition.y);
            }

            Image retryImage = retry.GetComponent<Image>();
            Image lobbyImage = lobby.GetComponent<Image>();
            if (retryImage != null && lobbyImage != null)
            {
                retryImage.sprite = lobbyImage.sprite;
                retryImage.type = lobbyImage.type;
                retryImage.preserveAspect = lobbyImage.preserveAspect;
                retryImage.color = lobbyImage.color;
                retryImage.material = lobbyImage.material;
            }

            retry.transition = lobby.transition;
            retry.colors = lobby.colors;
            retry.spriteState = lobby.spriteState;
        }

        private void RefreshDecorations(SessionResult result)
        {
            bool cleared = result.Status == SessionResultStatus.Cleared;

            RuntimeUiFactory.SetDirectChildActive(transform, "ClearGlow", cleared);
            RuntimeUiFactory.SetDirectChildActive(transform, "GameOverMist", false);
            RuntimeUiFactory.SetDirectChildActive(transform, "RankRingImage", result.Mode != AO.Core.PlayMode.Eternal);
        }

        private static string FormatTime(double seconds)
        {
            int whole = Mathf.Max(0, Mathf.RoundToInt((float)seconds));
            return $"{whole / 60:00}:{whole % 60:00}";
        }

        private static string FormatRank(string rank)
        {
            return string.IsNullOrWhiteSpace(rank) ? "-" : rank;
        }

        private static bool HasDetailTextTargets(Transform parent)
        {
            return FindRect(parent, "JudgementBreakdown") != null;
        }

        private static string BuildDetailStats(SessionResult result)
        {
            return BuildJudgementBreakdown(result);
        }

        private static string BuildJudgementBreakdown(SessionResult result)
        {
            return $"P/G/M<pos=150>{result.PerfectCount} / {result.GoodCount} / {result.MissCount}\n" +
                   $"Fish/Fever<pos=150>{result.FishStrokeSuccessCount}/{result.FishStrokeFailCount} / {result.FeverActivations}";
        }

        private static string BuildHandRangeStats(SessionResult result)
        {
            string space = result.HandRangeIsLocal
                ? $"{result.HandRangeReferenceName} local"
                : "world";
            return $"Hand range ({space})\n" +
                   $"L {FormatHandRange(result.LeftHandRange)}\n" +
                   $"R {FormatHandRange(result.RightHandRange)}";
        }

        private static string FormatHandRange(HandRangeStats range)
        {
            if (!range.HasSamples) return "no samples";
            return $"min {FormatVector(range.Min)}  max {FormatVector(range.Max)}";
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:F2},{value.y:F2},{value.z:F2})";
        }

        private static void ConfigureDenseStatsText(TMP_Text text, float fontSizeMax = 22f, float fontSizeMin = 12f, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            if (text == null) return;

            text.enableAutoSizing = true;
            text.fontSizeMax = fontSizeMax;
            text.fontSizeMin = fontSizeMin;
            text.alignment = alignment;
            text.overflowMode = TextOverflowModes.Ellipsis;
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

            rect.gameObject.SetActive(true);
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

            rect.gameObject.SetActive(true);
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

        private TMP_Text GetOptionalText(Transform parent, string name, string value, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            RectTransform rect = FindRect(parent, name);
            if (rect == null)
            {
                return _allowRuntimeUiCreation
                    ? RuntimeUiFactory.EnsureText(parent, name, value, anchoredPosition, size, fontSize, alignment)
                    : null;
            }

            rect.gameObject.SetActive(true);
            TMP_Text text = rect.GetComponent<TMP_Text>();
            if (text == null) text = rect.GetComponentInChildren<TMP_Text>(true);
            if (text == null)
            {
                if (!_allowRuntimeUiCreation)
                {
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

        private RectTransform GetPanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            RectTransform rect = FindRect(parent, name);
            if (rect == null)
            {
                if (!_allowRuntimeUiCreation)
                {
                    ReportMissing("RectTransform", parent, name);
                    return null;
                }

                return RuntimeUiFactory.EnsurePanel(parent, name, anchoredPosition, size, color);
            }

            rect.gameObject.SetActive(true);
            if (!_preferSceneVisuals)
            {
                Image image = rect.GetComponent<Image>();
                if (image != null) image.color = color;
            }

            return rect;
        }

        private RectTransform GetOptionalPanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            RectTransform rect = FindRect(parent, name);
            if (rect == null)
            {
                return _allowRuntimeUiCreation
                    ? RuntimeUiFactory.EnsurePanel(parent, name, anchoredPosition, size, color)
                    : null;
            }

            rect.gameObject.SetActive(true);
            if (!_preferSceneVisuals)
            {
                Image image = rect.GetComponent<Image>();
                if (image != null) image.color = color;
            }

            return rect;
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
            Debug.LogError($"[ResultScreenController] Required Result scene object '{name}' ({expectedType}) under '{parentName}' is missing or misconfigured. Runtime UI creation is disabled, so no replacement was generated.", this);
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
    }
}
