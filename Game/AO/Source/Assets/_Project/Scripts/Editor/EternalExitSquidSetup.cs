using AO.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class EternalExitSquidSetup
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const string SquidPrefabPath = "Assets/_Project/Prefabs/Environment/RebuiltFromGlb/squid_FromGlb.prefab";
        private const string ExitObjectName = "EternalExitObject";
        private const string VisualObjectName = "EternalExit_SquidVisual";
        private const float TargetVisualSizeMeters = 0.42f;

        [MenuItem("AO/Setup/Apply Squid Eternal Exit Object")]
        public static void ApplyToGameplayScene()
        {
            Scene scene = OpenGameplaySceneSafely();
            if (!scene.IsValid()) return;

            EternalExitObject exitObject = EnsureSquidExitObject(scene);

            if (exitObject == null)
            {
                Debug.LogError("[AO] Squid Eternal exit setup failed.");
                return;
            }

            WireGameStateManager(scene, exitObject);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AO] Squid Eternal exit object applied. It exits Eternal mode by pointer/click only, not by hand touch.");
        }

        private static Scene OpenGameplaySceneSafely()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.path == GameplayScenePath)
            {
                return activeScene;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[AO] Squid Eternal exit setup cancelled.");
                return default;
            }

            return EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }

        public static EternalExitObject EnsureSquidExitObject(Scene scene)
        {
            GameObject hitAnchor = FindSceneGameObject(scene, "HitAnchor");
            Transform parent = hitAnchor != null ? hitAnchor.transform : null;
            GameObject exitGo = parent != null ? FindOrCreateChild(parent, ExitObjectName) : FindOrCreateRoot(scene, ExitObjectName);

            if (exitGo.transform.parent == null && parent != null)
            {
                Undo.SetTransformParent(exitGo.transform, parent, "Parent Eternal Exit Object");
            }

            if (exitGo.transform.localPosition == Vector3.zero)
            {
                exitGo.transform.localPosition = new Vector3(0.62f, 0f, 0f);
            }

            exitGo.transform.localRotation = Quaternion.identity;
            exitGo.transform.localScale = Vector3.one;

            BoxCollider collider = EnsureComponent<BoxCollider>(exitGo);
            collider.isTrigger = true;
            collider.center = Vector3.zero;
            collider.size = new Vector3(0.55f, 0.55f, 0.55f);

            Rigidbody rb = EnsureComponent<Rigidbody>(exitGo);
            rb.useGravity = false;
            rb.isKinematic = true;

            exitGo.SetActive(true);
            ReplaceVisual(exitGo.transform);

            EternalExitObject exitObject = EnsureComponent<EternalExitObject>(exitGo);
            exitObject.SetAvailable(false);

            EditorUtility.SetDirty(exitGo);
            EditorUtility.SetDirty(exitObject);
            return exitObject;
        }

        private static void ReplaceVisual(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name == VisualObjectName || child.name == "Cube" || child.name == "Visual")
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SquidPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[AO] Squid prefab not found: {SquidPrefabPath}");
                return;
            }

            GameObject visual = PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene) as GameObject;
            if (visual == null)
            {
                Debug.LogWarning($"[AO] Could not instantiate squid prefab: {SquidPrefabPath}");
                return;
            }

            Undo.RegisterCreatedObjectUndo(visual, "Create Eternal Squid Visual");
            visual.name = VisualObjectName;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            ScaleAndCenterVisual(parent, visual.transform);
            EditorUtility.SetDirty(visual);
        }

        private static void ScaleAndCenterVisual(Transform exitRoot, Transform visual)
        {
            if (!TryGetRendererBounds(visual, out Bounds bounds)) return;

            float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxSize > 0.0001f)
            {
                float scale = TargetVisualSizeMeters / maxSize;
                visual.localScale *= scale;
            }

            if (TryGetRendererBounds(visual, out bounds))
            {
                visual.position += exitRoot.position - bounds.center;
            }
        }

        private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bounds = default;
            bool found = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private static void WireGameStateManager(Scene scene, EternalExitObject exitObject)
        {
            GameStateManager manager = FindSceneComponent<GameStateManager>(scene);
            if (manager == null) return;

            SerializedObject so = new SerializedObject(manager);
            SerializedProperty prop = so.FindProperty("_eternalExitObject");
            if (prop == null) return;

            prop.objectReferenceValue = exitObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }

        private static GameObject FindOrCreateChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null) return existing.gameObject;

            GameObject go = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(go, $"Create {childName}");
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject FindOrCreateRoot(Scene scene, string objectName)
        {
            GameObject existing = FindSceneGameObject(scene, objectName);
            if (existing != null) return existing;

            GameObject go = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(go, $"Create {objectName}");
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        private static GameObject FindSceneGameObject(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform match = FindChildRecursive(root.transform, objectName);
                if (match != null) return match.gameObject;
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform parent, string objectName)
        {
            if (parent.name == objectName) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform match = FindChildRecursive(parent.GetChild(i), objectName);
                if (match != null) return match;
            }

            return null;
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            foreach (T component in Resources.FindObjectsOfTypeAll<T>())
            {
                if (component == null || component.gameObject.scene != scene) continue;
                return component;
            }

            return null;
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }
    }
}
