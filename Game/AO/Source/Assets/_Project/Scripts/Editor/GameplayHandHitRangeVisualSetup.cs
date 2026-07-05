using AO.Judgement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class GameplayHandHitRangeVisualSetup
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const string BubbleSpherePrefabPath = "Assets/_Project/Art/UI/Note/BubbleNote_C01/Sphere.prefab";

        private static readonly Color LeftBaseColor = new Color(0.18f, 0.95f, 1f, 0.26f);
        private static readonly Color LeftInnerColor = new Color(0.45f, 0.85f, 1f, 1f);
        private static readonly Color LeftRimColor = new Color(0.8f, 1f, 1f, 1f);
        private static readonly Color RightBaseColor = new Color(1f, 0.34f, 0.92f, 0.24f);
        private static readonly Color RightInnerColor = new Color(1f, 0.56f, 0.95f, 1f);
        private static readonly Color RightRimColor = new Color(1f, 0.82f, 1f, 1f);

        [MenuItem("AO/Setup/Apply Gameplay Hand Hit Range Visuals")]
        public static void ApplyToGameplayScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            int configured = ApplyToScene(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AO] Gameplay hand hit range visuals applied. Configured hand targets: {configured}.");
        }

        public static void ApplyToGameplaySceneFromCommandLine()
        {
            ApplyToGameplayScene();
        }

        public static int ApplyToScene(Scene scene)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BubbleSpherePrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[AO] Bubble hand range visual prefab was not found: {BubbleSpherePrefabPath}");
                return 0;
            }

            Transform judgementRig = FindSceneTransform(scene, "JudgementRig");
            if (judgementRig == null)
            {
                Debug.LogError("[AO] JudgementRig was not found. Gameplay hand hit range visuals were not applied.");
                return 0;
            }

            int configured = 0;
            configured += ConfigureHand(judgementRig.Find("LeftHandTarget"), prefab, LeftBaseColor, LeftInnerColor, LeftRimColor);
            configured += ConfigureHand(judgementRig.Find("RightHandTarget"), prefab, RightBaseColor, RightInnerColor, RightRimColor);
            return configured;
        }

        private static int ConfigureHand(Transform handTarget, GameObject prefab, Color baseColor, Color innerColor, Color rimColor)
        {
            if (handTarget == null) return 0;

            GameplayHandHitRangeVisual visual = handTarget.GetComponent<GameplayHandHitRangeVisual>();
            if (visual == null) visual = Undo.AddComponent<GameplayHandHitRangeVisual>(handTarget.gameObject);

            SerializedObject serialized = new SerializedObject(visual);
            SetObject(serialized, "_visualPrefab", prefab);
            SetBool(serialized, "_visible", true);
            SetFloat(serialized, "_radiusMultiplier", 1f);
            SetFloat(serialized, "_fallbackRadius", 0.1125f);
            SetColor(serialized, "_baseColor", baseColor);
            SetColor(serialized, "_innerColor", innerColor);
            SetColor(serialized, "_rimColor", rimColor);
            SetFloat(serialized, "_baseAlpha", baseColor.a);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(visual);
            EditorUtility.SetDirty(handTarget.gameObject);
            return 1;
        }

        private static Transform FindSceneTransform(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform match = FindRecursive(root.transform, name);
                if (match != null) return match;
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

        private static void SetFloat(SerializedObject serialized, string propertyName, float value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null) property.floatValue = value;
        }

        private static void SetColor(SerializedObject serialized, string propertyName, Color value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null) property.colorValue = value;
        }
    }
}
