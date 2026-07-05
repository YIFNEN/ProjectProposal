using UnityEditor;
using UnityEngine;

namespace AO.Editor
{
    internal static class TexturedGodRayPlacement
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Environment/PF_AO_GodRay_Texture.prefab";

        [MenuItem("AO/Art/Place Textured God Ray In Scene")]
        private static void PlaceInScene()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[AO] Textured god ray prefab was not found: {PrefabPath}");
                return;
            }

            Transform parent = Selection.activeTransform;
            if (parent == null)
            {
                GameObject artEnvironmentRoot = GameObject.Find("ArtEnvironmentRoot");
                parent = artEnvironmentRoot != null ? artEnvironmentRoot.transform : null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Place AO Textured God Ray");
            Undo.SetTransformParent(instance.transform, parent, "Parent AO Textured God Ray");

            if (parent != null)
            {
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
            }
            else
            {
                instance.transform.position = Vector3.zero;
            }

            Selection.activeGameObject = instance;
            EditorGUIUtility.PingObject(instance);
        }

        [MenuItem("AO/Art/Place Textured God Ray In Scene", true)]
        private static bool ValidatePlaceInScene()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        }
    }
}
