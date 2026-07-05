using AO.Cameras;
using AO.Character;
using AO.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AO.Editor
{
    public static class PersistentRuntimeSceneObjectsSetup
    {
        [MenuItem("AO/Setup/Validate Scene Authored Runtime Objects")]
        public static void ValidateBuildScenes()
        {
            string originalScene = SceneManager.GetActiveScene().path;
            int totalIssues = 0;

            try
            {
                foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
                {
                    if (!buildScene.enabled || string.IsNullOrWhiteSpace(buildScene.path)) continue;

                    Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                    int issues = ValidateScene(scene);
                    totalIssues += issues;

                    if (issues == 0)
                    {
                        Debug.Log($"[AO] Scene-authored runtime object validation passed: {buildScene.path}");
                    }
                    else
                    {
                        Debug.LogError($"[AO] Scene-authored runtime object validation found {issues} issue(s): {buildScene.path}");
                    }
                }
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(originalScene))
                {
                    EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
                }
            }

            if (totalIssues == 0)
            {
                Debug.Log("[AO] All build scenes have the required authored runtime objects.");
            }
            else
            {
                Debug.LogError($"[AO] Scene-authored runtime object validation finished with {totalIssues} total issue(s). Nothing was created or overwritten.");
            }
        }

        private static int ValidateScene(Scene scene)
        {
            int issues = 0;

            foreach (ControllerUiRayPointer pointer in FindSceneObjects<ControllerUiRayPointer>(scene))
            {
                issues += ValidateLineChild(pointer.transform, "LeftUiRayLine", pointer);
                issues += ValidateLineChild(pointer.transform, "RightUiRayLine", pointer);
            }

            foreach (DVariantRiderRig riderRig in FindSceneObjects<DVariantRiderRig>(scene))
            {
                issues += ValidateChildTransform(riderRig.transform, "JudgementRig", riderRig);
                Transform judgementRig = riderRig.transform.Find("JudgementRig");
                if (judgementRig != null)
                {
                    issues += ValidateChildTransform(judgementRig, "LeftHandTarget", riderRig);
                    issues += ValidateChildTransform(judgementRig, "RightHandTarget", riderRig);
                }
                issues += ValidateChildTransform(riderRig.transform, "SeatAnchor", riderRig);
                Transform seatAnchor = riderRig.transform.Find("SeatAnchor");
                Transform characterRoot = seatAnchor != null ? seatAnchor.Find("CharacterRoot") : null;
                if (characterRoot != null)
                {
                    issues += ValidateChildTransform(characterRoot, "VisualHandTargets", riderRig);
                    Transform visualTargets = characterRoot.Find("VisualHandTargets");
                    if (visualTargets != null)
                    {
                        issues += ValidateChildTransform(visualTargets, "LeftHandTarget", riderRig);
                        issues += ValidateChildTransform(visualTargets, "RightHandTarget", riderRig);
                    }
                }
            }

            foreach (SpectatorCameraRig rig in FindSceneObjects<SpectatorCameraRig>(scene))
            {
                issues += ValidateCameraChild(rig.transform, "SpectatorCamera", rig);
            }

            foreach (LobbyScreenController lobby in FindSceneObjects<LobbyScreenController>(scene))
            {
                issues += ValidateLobbyUi(lobby);
            }

            foreach (TitleScreenController title in FindSceneObjects<TitleScreenController>(scene))
            {
                issues += ValidateTitleUi(title);
            }

            foreach (ResultScreenController result in FindSceneObjects<ResultScreenController>(scene))
            {
                issues += ValidateResultUi(result);
            }

            foreach (GameplaySettingsOverlay overlay in FindSceneObjects<GameplaySettingsOverlay>(scene))
            {
                issues += ValidateSettingsOverlay(overlay);
            }

            foreach (OxygenCriticalEffect effect in FindSceneObjects<OxygenCriticalEffect>(scene))
            {
                issues += ValidateImageChild(effect.transform, "OxygenCriticalPulse", effect);
            }

            return issues;
        }

        private static int ValidateLobbyUi(LobbyScreenController lobby)
        {
            int issues = 0;
            issues += ValidateButton(lobby.transform, "ExitButton", lobby);
            issues += ValidateText(lobby.transform, "Title", lobby);
            issues += ValidateButton(lobby.transform, "PreviousSongButton", lobby);
            issues += ValidateButton(lobby.transform, "NextSongButton", lobby);

            RectTransform contentRoot = FindRectChild(lobby.transform, "SongContentRoot");
            issues += ValidateRect(contentRoot, lobby.transform, "SongContentRoot", lobby);

            RectTransform info = FindRectChild(contentRoot, "SongInfoPanel");
            issues += ValidateRect(info, contentRoot, "SongInfoPanel", lobby);
            RectTransform thumbnailFrame = FindRectChild(info, "ThumbnailFrame");
            issues += ValidateRect(thumbnailFrame, info, "ThumbnailFrame", lobby);
            issues += ValidateImageChild(thumbnailFrame, "ThumbnailImage", lobby);
            issues += ValidateText(info, "SongIndex", lobby);
            issues += ValidateText(info, "SongName", lobby);
            issues += ValidateText(info, "SongRecord", lobby);

            RectTransform play = FindRectChild(contentRoot, "SongPlayPanel");
            issues += ValidateRect(play, contentRoot, "SongPlayPanel", lobby);
            issues += ValidateButton(play, "PreviewToggleButton", lobby);
            issues += ValidateButton(play, "NormalButton", lobby);
            issues += ValidateButton(play, "EternalButton", lobby);
            return issues;
        }

        private static int ValidateTitleUi(TitleScreenController title)
        {
            int issues = 0;
            issues += ValidateText(title.transform, "Title", title);
            issues += ValidateText(title.transform, "Subtitle", title);
            issues += ValidateButton(title.transform, "StartButton", title);
            return issues;
        }

        private static int ValidateResultUi(ResultScreenController result)
        {
            int issues = 0;
            issues += ValidateText(result.transform, "Title", result);
            issues += ValidateText(result.transform, "Rank", result);
            issues += ValidateText(result.transform, "MainStats", result);
            issues += ValidateButton(result.transform, "RetryButton", result);
            issues += ValidateButton(result.transform, "LobbyButton", result);
            return issues;
        }

        private static int ValidateSettingsOverlay(GameplaySettingsOverlay overlay)
        {
            int issues = 0;
            RectTransform panel = FindRectChild(overlay.transform, "SettingsPanel");
            issues += ValidateRect(panel, overlay.transform, "SettingsPanel", overlay);
            issues += ValidateButton(panel, "ResumeButton", overlay);
            issues += ValidateButton(panel, "LobbyButton", overlay);
            issues += ValidateComponentInChild<Slider>(panel, "BgmSlider", overlay);
            issues += ValidateComponentInChild<Slider>(panel, "SfxSlider", overlay);
            issues += ValidateComponentInChild<Slider>(panel, "NoteSpeedSlider", overlay);
            issues += ValidateButton(panel, "PlaybackSpeedDownButton", overlay);
            issues += ValidateButton(panel, "PlaybackSpeedUpButton", overlay);
            issues += ValidateComponentInChild<Slider>(panel, "OffsetSlider", overlay);
            return issues;
        }

        private static int ValidateLineChild(Transform parent, string name, Object context)
        {
            Transform child = FindRecursive(parent, name);
            if (child == null) return ReportMissing(parent, name, "LineRenderer", context);
            return child.GetComponent<LineRenderer>() == null
                ? ReportMissing(parent, name, "LineRenderer component", context)
                : 0;
        }

        private static int ValidateCameraChild(Transform parent, string name, Object context)
        {
            Transform child = FindRecursive(parent, name);
            if (child == null) return ReportMissing(parent, name, "Camera", context);
            return child.GetComponent<Camera>() == null
                ? ReportMissing(parent, name, "Camera component", context)
                : 0;
        }

        private static int ValidateChildTransform(Transform parent, string name, Object context)
        {
            return FindRecursive(parent, name) == null ? ReportMissing(parent, name, "Transform", context) : 0;
        }

        private static int ValidateImageChild(Transform parent, string name, Object context)
        {
            return ValidateComponentInChild<Image>(parent, name, context);
        }

        private static int ValidateButton(Transform parent, string name, Object context)
        {
            return ValidateComponentInChild<Button>(parent, name, context);
        }

        private static int ValidateText(Transform parent, string name, Object context)
        {
            return ValidateComponentInChild<TMP_Text>(parent, name, context);
        }

        private static int ValidateRect(RectTransform rect, Transform parent, string name, Object context)
        {
            return rect == null ? ReportMissing(parent, name, "RectTransform", context) : 0;
        }

        private static int ValidateComponentInChild<T>(Transform parent, string name, Object context) where T : Component
        {
            Transform child = FindRecursive(parent, name);
            if (child == null) return ReportMissing(parent, name, typeof(T).Name, context);

            if (child.GetComponent<T>() == null && child.GetComponentInChildren<T>(true) == null)
            {
                return ReportMissing(parent, name, $"{typeof(T).Name} component", context);
            }

            return 0;
        }

        private static int ReportMissing(Transform parent, string name, string expected, Object context)
        {
            string parentName = parent != null ? parent.name : "<missing parent>";
            Debug.LogError($"[AO] Required scene-authored object '{name}' under '{parentName}' is missing or does not have {expected}. Nothing was created or overwritten.", context);
            return 1;
        }

        private static RectTransform FindRectChild(Transform parent, string name)
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

        private static T[] FindSceneObjects<T>(Scene scene) where T : Component
        {
            T[] allObjects = Resources.FindObjectsOfTypeAll<T>();
            int count = 0;
            for (int i = 0; i < allObjects.Length; i++)
            {
                T item = allObjects[i];
                if (item != null && item.gameObject.scene == scene) count++;
            }

            T[] sceneObjects = new T[count];
            int index = 0;
            for (int i = 0; i < allObjects.Length; i++)
            {
                T item = allObjects[i];
                if (item != null && item.gameObject.scene == scene) sceneObjects[index++] = item;
            }

            return sceneObjects;
        }
    }
}
