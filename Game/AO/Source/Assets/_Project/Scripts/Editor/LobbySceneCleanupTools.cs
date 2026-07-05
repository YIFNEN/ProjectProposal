using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AO.Editor
{
    public static class LobbySceneCleanupTools
    {
        [MenuItem("AO/Setup/Clean Deprecated Lobby ThumbnailFrame Images")]
        public static void CleanDeprecatedThumbnailFrameImages()
        {
            Scene originalScene = SceneManager.GetActiveScene();
            string originalScenePath = originalScene.path;
            int removed = 0;

            try
            {
                foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
                {
                    if (!buildScene.enabled || string.IsNullOrWhiteSpace(buildScene.path)) continue;
                    if (!buildScene.path.EndsWith("02_Lobby.unity", System.StringComparison.OrdinalIgnoreCase)) continue;

                    Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                    removed += CleanScene(scene);
                    if (removed > 0)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                }
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(originalScenePath))
                {
                    EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                }
            }

            Debug.Log($"[AO] Removed {removed} deprecated Image/CanvasRenderer component(s) from Lobby ThumbnailFrame containers.");
        }

        private static int CleanScene(Scene scene)
        {
            int removed = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform frame in root.GetComponentsInChildren<Transform>(true))
                {
                    if (frame.name != "ThumbnailFrame") continue;

                    Image image = frame.GetComponent<Image>();
                    if (image != null)
                    {
                        Object.DestroyImmediate(image, true);
                        removed++;
                    }

                    CanvasRenderer renderer = frame.GetComponent<CanvasRenderer>();
                    if (renderer != null && frame.GetComponent<Graphic>() == null)
                    {
                        Object.DestroyImmediate(renderer, true);
                        removed++;
                    }
                }
            }

            return removed;
        }
    }
}
