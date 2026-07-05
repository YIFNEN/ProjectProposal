using AO.Debugging;
using AO.Rhythm;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class GameplayHitDebugVisualizerSetup
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const string VisualizerName = "GameplayHitDebugVisualizer";

        [MenuItem("AO/Debug/Apply Gameplay Hit Debug Visualizer")]
        public static void ApplyToGameplayScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            GameObject visualizerObject = FindSceneGameObject(scene, VisualizerName);
            if (visualizerObject == null)
            {
                visualizerObject = new GameObject(VisualizerName);
                Undo.RegisterCreatedObjectUndo(visualizerObject, "Create Gameplay Hit Debug Visualizer");
            }

            GameplayHitDebugVisualizer visualizer = visualizerObject.GetComponent<GameplayHitDebugVisualizer>();
            if (visualizer == null) visualizer = Undo.AddComponent<GameplayHitDebugVisualizer>(visualizerObject);
            visualizer.enabled = true;

            Transform hitAnchor = FindSceneTransform(scene, "HitAnchor");
            Transform judgementRig = FindSceneTransform(scene, "JudgementRig");
            Transform visualHandTargets = FindSceneTransform(scene, "VisualHandTargets");
            NoteSpawner noteSpawner = FindSceneComponent<NoteSpawner>(scene);

            SerializedObject serialized = new SerializedObject(visualizer);
            SetObject(serialized, "_hitAnchor", hitAnchor);
            SetObject(serialized, "_noteSpawner", noteSpawner);
            SetObject(serialized, "_leftJudgementHand", judgementRig != null ? judgementRig.Find("LeftHandTarget") : null);
            SetObject(serialized, "_rightJudgementHand", judgementRig != null ? judgementRig.Find("RightHandTarget") : null);
            SetObject(serialized, "_leftVisualHand", visualHandTargets != null ? visualHandTargets.Find("LeftHandTarget") : null);
            SetObject(serialized, "_rightVisualHand", visualHandTargets != null ? visualHandTargets.Find("RightHandTarget") : null);
            SetBool(serialized, "_showJudgementHands", true);
            SetBool(serialized, "_showVisualHands", true);
            SetBool(serialized, "_showLaneHitPoints", true);
            SetBool(serialized, "_showActiveBubbleOverlap", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(visualizerObject);
            EditorUtility.SetDirty(visualizer);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[AO] Gameplay hit debug visualizer applied. In Play Mode it shows JudgementRig hand colliders, VisualHandTargets, lane hit points, and active Bubble overlap radii.", visualizerObject);
        }

        [MenuItem("AO/Debug/Disable Gameplay Hit Debug Visualizer")]
        public static void DisableInGameplayScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            GameObject visualizerObject = FindSceneGameObject(scene, VisualizerName);
            if (visualizerObject == null)
            {
                Debug.Log("[AO] Gameplay hit debug visualizer was not found.");
                return;
            }

            GameplayHitDebugVisualizer visualizer = visualizerObject.GetComponent<GameplayHitDebugVisualizer>();
            if (visualizer != null)
            {
                visualizer.enabled = false;
                EditorUtility.SetDirty(visualizer);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[AO] Gameplay hit debug visualizer disabled.", visualizerObject);
        }

        public static void ApplyToGameplaySceneFromCommandLine()
        {
            ApplyToGameplayScene();
        }

        private static void SetObject(SerializedObject serialized, string propertyName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = value;
        }

        private static void SetBool(SerializedObject serialized, string propertyName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null) property.boolValue = value;
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }

            return null;
        }

        private static Transform FindSceneTransform(Scene scene, string name)
        {
            GameObject gameObject = FindSceneGameObject(scene, name);
            return gameObject != null ? gameObject.transform : null;
        }

        private static GameObject FindSceneGameObject(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform match = FindRecursive(root.transform, name);
                if (match != null) return match.gameObject;
            }

            return null;
        }

        private static Transform FindRecursive(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform match = FindRecursive(child, name);
                if (match != null) return match;
            }

            return null;
        }
    }
}
