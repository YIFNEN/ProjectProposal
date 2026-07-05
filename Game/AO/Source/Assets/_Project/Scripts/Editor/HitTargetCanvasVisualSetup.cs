using AO.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AO.Editor
{
    public static class HitTargetCanvasVisualSetup
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const string ShaderPath = "Assets/_Project/Art/Shaders/UI/AO_UI_AlwaysOnTop.shader";
        private const string MaterialPath = "Assets/_Project/Art/UI/Materials/M_HitTargetCanvas_AlwaysOnTop.mat";
        private const string CanvasName = "HitTargetCanvas";
        private const int SortingOrder = 120;

        [MenuItem("AO/UI/Apply Hit Target Canvas Always On Top")]
        public static void ApplyToGameplayScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            GameObject canvasObject = FindSceneGameObject(scene, CanvasName);
            if (canvasObject == null)
            {
                Debug.LogError($"[AO] {CanvasName} was not found in {GameplayScenePath}.");
                return;
            }

            Material material = EnsureAlwaysOnTopMaterial();
            if (material == null) return;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = SortingOrder;
                EditorUtility.SetDirty(canvas);
            }

            HitTargetCanvasAlwaysOnTop alwaysOnTop = canvasObject.GetComponent<HitTargetCanvasAlwaysOnTop>();
            if (alwaysOnTop == null) alwaysOnTop = Undo.AddComponent<HitTargetCanvasAlwaysOnTop>(canvasObject);

            SerializedObject serializedObject = new SerializedObject(alwaysOnTop);
            serializedObject.FindProperty("_alwaysOnTopMaterial").objectReferenceValue = material;
            serializedObject.FindProperty("_sortingOrder").intValue = SortingOrder;
            serializedObject.FindProperty("_overrideCanvasSorting").boolValue = true;
            serializedObject.FindProperty("_applyToInactiveChildren").boolValue = true;
            serializedObject.FindProperty("_disableRaycastTargets").boolValue = true;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            Graphic[] graphics = canvasObject.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null) continue;
                graphic.material = material;
                graphic.raycastTarget = false;
                EditorUtility.SetDirty(graphic);
            }

            EditorUtility.SetDirty(alwaysOnTop);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AO] Applied always-on-top UI material to {CanvasName}. Graphics updated: {graphics.Length}.", canvasObject);
        }

        public static Material EnsureAlwaysOnTopMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null) shader = Shader.Find("AO/UI/Always On Top");
            if (shader == null)
            {
                Debug.LogError($"[AO] Always-on-top UI shader was not found at {ShaderPath}.");
                return null;
            }

            material = new Material(shader)
            {
                name = "M_HitTargetCanvas_AlwaysOnTop",
                renderQueue = 4000
            };

            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static GameObject FindSceneGameObject(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform found = FindDeepChild(roots[i].transform, objectName);
                if (found != null) return found.gameObject;
            }

            return null;
        }

        private static Transform FindDeepChild(Transform root, string childName)
        {
            if (root.name == childName) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeepChild(root.GetChild(i), childName);
                if (found != null) return found;
            }

            return null;
        }
    }
}
