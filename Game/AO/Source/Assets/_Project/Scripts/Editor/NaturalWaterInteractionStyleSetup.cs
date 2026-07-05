using AO.Rhythm;
using AO.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AO.Editor
{
    public static class NaturalWaterInteractionStyleSetup
    {
        private static readonly string[] UiScenes =
        {
            "Assets/_Project/Scenes/01_Title.unity",
            "Assets/_Project/Scenes/02_Lobby.unity",
            "Assets/_Project/Scenes/GamePlayScene.unity",
            "Assets/_Project/Scenes/04_Result.unity"
        };

        private const string LobbyScenePath = "Assets/_Project/Scenes/02_Lobby.unity";
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";

        [MenuItem("AO/Setup/Apply NaturalWater Interaction Lines And Cleanup")]
        public static void Apply()
        {
            foreach (string scenePath in UiScenes)
            {
                if (!System.IO.File.Exists(scenePath)) continue;

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                ConfigureUiRayLines();

                if (scenePath == LobbyScenePath)
                {
                    RemoveSongPlayPanelBackground(scene);
                }

                if (scenePath == GameplayScenePath)
                {
                    ConfigureGameplayRails();
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AO] NaturalWater interaction lines styled and SongPlayPanel background cleanup applied.");
        }

        public static void ApplyFromCommandLine()
        {
            Apply();
        }

        private static void ConfigureUiRayLines()
        {
            foreach (ControllerUiRayPointer pointer in Object.FindObjectsByType<ControllerUiRayPointer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (pointer == null || !pointer.gameObject.scene.isLoaded) continue;

                SerializedObject so = new SerializedObject(pointer);
                SetBool(so, "_showRayLines", true);
                SetFloat(so, "_lineWidth", 0.02f);
                SetColor(so, "_lineColor", new Color(0.25f, 0.94f, 1f, 0.68f));
                SetColor(so, "_lineHitColor", new Color(0.72f, 1f, 0.88f, 0.95f));
                SetFloat(so, "_lineEndAlphaMultiplier", 0.12f);
                SetFloat(so, "_hitLineWidthMultiplier", 1.45f);
                SetFloat(so, "_linePulseHz", 1.6f);
                SetFloat(so, "_linePulseAmount", 0.16f);
                SetInt(so, "_lineCapVertices", 8);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pointer);
            }
        }

        private static void ConfigureGameplayRails()
        {
            foreach (LanePathGuide guide in Object.FindObjectsByType<LanePathGuide>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (guide == null || !guide.gameObject.scene.isLoaded) continue;

                Transform lineRoot = guide.transform.Find("LanePathLines");
                if (lineRoot != null) Object.DestroyImmediate(lineRoot.gameObject, true);
                Object.DestroyImmediate(guide, true);
            }
        }

        private static void RemoveSongPlayPanelBackground(Scene scene)
        {
            GameObject panel = FindInScene(scene, "SongPlayPanel");
            if (panel == null) return;

            Image image = panel.GetComponent<Image>();
            if (image != null) Object.DestroyImmediate(image, true);

            CanvasRenderer renderer = panel.GetComponent<CanvasRenderer>();
            if (renderer != null && panel.GetComponent<Graphic>() == null)
            {
                Object.DestroyImmediate(renderer, true);
            }

            EditorUtility.SetDirty(panel);
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GameObject found = FindRecursive(root.transform, name);
                if (found != null) return found;
            }

            return null;
        }

        private static GameObject FindRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent.gameObject;

            for (int i = 0; i < parent.childCount; i++)
            {
                GameObject found = FindRecursive(parent.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        private static void SetBool(SerializedObject so, string propertyName, bool value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null) property.boolValue = value;
        }

        private static void SetInt(SerializedObject so, string propertyName, int value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null) property.intValue = value;
        }

        private static void SetFloat(SerializedObject so, string propertyName, float value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null) property.floatValue = value;
        }

        private static void SetColor(SerializedObject so, string propertyName, Color value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null) property.colorValue = value;
        }
    }
}
