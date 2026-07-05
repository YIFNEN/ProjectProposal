using AO.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class QuestInputProbeSetup
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const string ProbeName = "QuestInputProbe";

        [MenuItem("AO/Debug/Apply Quest Input Probe To Gameplay Scene")]
        public static void ApplyToGameplayScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            GameObject probeObject = FindSceneGameObject(scene, ProbeName);
            if (probeObject == null)
            {
                probeObject = new GameObject(ProbeName);
                Undo.RegisterCreatedObjectUndo(probeObject, "Create Quest Input Probe");
            }

            QuestInputProbe probe = probeObject.GetComponent<QuestInputProbe>();
            if (probe == null) probe = Undo.AddComponent<QuestInputProbe>(probeObject);
            probe.enabled = true;

            SerializedObject serialized = new SerializedObject(probe);
            SetBool(serialized, "_showOnGui", true);
            SetBool(serialized, "_logOnEnable", true);
            SetBool(serialized, "_logPeriodically", false);
            SetBool(serialized, "_includeEnumeratedXrFeatures", true);
            SetBool(serialized, "_readLegacyXrDevices", true);
            SetBool(serialized, "_readInputSystemDevices", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(probeObject);
            EditorUtility.SetDirty(probe);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[AO] Quest input probe applied. Enter Play Mode with Quest Link or a Quest build to inspect controller grip/pointer/tracking/touch values.", probeObject);
        }

        [MenuItem("AO/Debug/Disable Quest Input Probe In Gameplay Scene")]
        public static void DisableInGameplayScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            GameObject probeObject = FindSceneGameObject(scene, ProbeName);
            if (probeObject == null)
            {
                Debug.Log("[AO] Quest input probe was not found.");
                return;
            }

            QuestInputProbe probe = probeObject.GetComponent<QuestInputProbe>();
            if (probe != null)
            {
                probe.enabled = false;
                EditorUtility.SetDirty(probe);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[AO] Quest input probe disabled.", probeObject);
        }

        public static void ApplyToGameplaySceneFromCommandLine()
        {
            ApplyToGameplayScene();
        }

        private static void SetBool(SerializedObject serialized, string propertyName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null) property.boolValue = value;
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
