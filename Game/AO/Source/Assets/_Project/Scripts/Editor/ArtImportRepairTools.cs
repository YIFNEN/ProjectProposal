using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class ArtImportRepairTools
    {
        private const string DeepBulbPrefabFolder = "Assets/_Project/Prefabs/Environment/Imported";
        private const string SquidPrefabPath = "Assets/_Project/Prefabs/Environment/Imported/squid 1.prefab";
        private const string GlbSourceFolder = "Assets/_Project/Prefabs/HUD/Imported";
        private const string RebuiltPrefabFolder = "Assets/_Project/Prefabs/Environment/RebuiltFromGlb";
        private const string DeepBulbsGlbPath = GlbSourceFolder + "/deep_bulbs.glb";
        private const string SquidGlbPath = GlbSourceFolder + "/squid.glb";
        private const string RebuiltDeepBulbsPrefabPath = RebuiltPrefabFolder + "/deep_bulbs_FromGlb.prefab";
        private const string RebuiltSquidPrefabPath = RebuiltPrefabFolder + "/squid_FromGlb.prefab";

        [MenuItem("AO/Art/Repair Imported Art Scene References")]
        public static void RepairImportedArtSceneReferences()
        {
            ClearSelection();
            int bulbs = ReplaceMissingDeepBulbsInOpenScenes();
            Debug.Log($"[AO Art Repair] Replaced deep_bulbs: {bulbs}. Squid prefab assignment is intentionally separate because imported GLB animation references must be verified first.");
        }

        [MenuItem("AO/Art/Replace Missing Deep Bulbs In Open Scenes")]
        public static void ReplaceMissingDeepBulbsInOpenScenesMenu()
        {
            int count = ReplaceMissingDeepBulbsInOpenScenes();
            Debug.Log($"[AO Art Repair] Replace Missing Deep Bulbs complete. Replaced={count}, OpenScenes={GetOpenSceneSummary()}");
        }

        [MenuItem("AO/Art/Clean Replaced Deep Bulb Names In Open Scenes")]
        public static void CleanReplacedDeepBulbNamesInOpenScenesMenu()
        {
            int count = CleanReplacedDeepBulbNamesInOpenScenes();
            Debug.Log($"[AO Art Repair] Clean Replaced Deep Bulb Names complete. Renamed={count}, OpenScenes={GetOpenSceneSummary()}");
        }

        [MenuItem("AO/Art/Clear Editor Selection")]
        public static void ClearEditorSelectionMenu()
        {
            ClearSelection();
            Debug.Log("[AO Art Repair] Cleared editor selection.");
        }

        public static int CleanReplacedDeepBulbNamesInOpenScenes()
        {
            ClearSelection();
            int count = 0;

            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!IsEditableSceneObject(go)
                    || !go.name.StartsWith("deep_bulbs", StringComparison.Ordinal)
                    || !go.name.Contains("(Missing Prefab", StringComparison.Ordinal))
                {
                    continue;
                }

                if (PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.MissingAsset)
                {
                    continue;
                }

                Undo.RecordObject(go, "Clean Deep Bulb Name");
                go.name = GetDeepBulbPrefabName(go.name);
                EditorUtility.SetDirty(go);
                EditorSceneManager.MarkSceneDirty(go.scene);
                count++;
            }

            return count;
        }

        public static int ReplaceMissingDeepBulbsInOpenScenes()
        {
            ClearSelection();
            var replacements = new List<GameObject>();
            int namedDeepBulbs = 0;

            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!IsEditableSceneObject(go))
                {
                    continue;
                }

                if (!go.name.StartsWith("deep_bulbs"))
                {
                    continue;
                }

                namedDeepBulbs++;

                if (!IsMissingPrefabObject(go))
                {
                    continue;
                }

                replacements.Add(go);
            }

            int count = 0;
            foreach (GameObject missing in replacements)
            {
                string prefabName = GetDeepBulbPrefabName(missing.name);
                GameObject prefab = LoadDeepBulbReplacement(prefabName, out string prefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[AO Art Repair] No baked prefab found for {missing.name}: {prefabPath}");
                    continue;
                }

                ReplaceMissingPrefabInstance(missing, prefab);
                count++;
            }

            AssetDatabase.SaveAssets();
            if (count == 0)
            {
                Debug.Log($"[AO Art Repair] No missing deep_bulbs replaced. NamedDeepBulbsInOpenScenes={namedDeepBulbs}, MissingCandidates={replacements.Count}, OpenScenes={GetOpenSceneSummary()}");
            }

            return count;
        }

        private static string GetOpenSceneSummary()
        {
            var sceneNames = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded)
                {
                    sceneNames.Add(scene.name);
                }
            }

            return sceneNames.Count == 0 ? "(none)" : string.Join(", ", sceneNames);
        }

        private static string GetTransformPath(Transform transform)
        {
            var names = new List<string>();
            while (transform != null)
            {
                names.Add(transform.name);
                transform = transform.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static bool IsMissingPrefabObject(GameObject go)
        {
            if (go.name.Contains("(Missing Prefab", StringComparison.Ordinal))
            {
                return true;
            }

            return PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.MissingAsset;
        }

        private static string GetDeepBulbPrefabName(string missingObjectName)
        {
            int missingSuffixIndex = missingObjectName.IndexOf(" (Missing Prefab", StringComparison.Ordinal);
            if (missingSuffixIndex >= 0)
            {
                return missingObjectName.Substring(0, missingSuffixIndex);
            }

            return missingObjectName;
        }

        private static GameObject LoadDeepBulbReplacement(string prefabName, out string prefabPath)
        {
            GameObject rebuilt = AssetDatabase.LoadAssetAtPath<GameObject>(RebuiltDeepBulbsPrefabPath);
            if (rebuilt != null)
            {
                prefabPath = RebuiltDeepBulbsPrefabPath;
                return rebuilt;
            }

            prefabPath = $"{DeepBulbPrefabFolder}/{prefabName}.prefab";
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        [MenuItem("AO/Art/Assign Squid Baked Prefab In Open Scenes")]
        public static void AssignSquidPrefabInOpenScenesMenu()
        {
            SquidAssignStats stats = AssignSquidPrefabInOpenScenes();
            Debug.Log($"[AO Art Repair] Assign Squid Baked Prefab complete. Assigned={stats.Assigned}, AlreadyAssigned={stats.AlreadyAssigned}, SquidSchools={stats.SquidSchools}, OpenScenes={GetOpenSceneSummary()}");
        }

        public static SquidAssignStats AssignSquidPrefabInOpenScenes()
        {
            ClearSelection();
            string squidPrefabPath = AssetDatabase.LoadAssetAtPath<GameObject>(RebuiltSquidPrefabPath) != null
                ? RebuiltSquidPrefabPath
                : SquidPrefabPath;

            GameObject squidPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(squidPrefabPath);
            if (squidPrefab == null)
            {
                Debug.LogWarning($"[AO Art Repair] Squid prefab not found: {squidPrefabPath}");
                return new SquidAssignStats();
            }

            if (!HasValidRenderableMesh(squidPrefab))
            {
                Debug.LogWarning($"[AO Art Repair] Squid prefab has no valid mesh references. Rebuild it from the AO-imported squid.glb before assigning: {squidPrefabPath}");
                return new SquidAssignStats();
            }

            Animation animation = squidPrefab.GetComponentInChildren<Animation>(true);
            if (animation != null && animation.clip == null && animation.GetClipCount() == 0)
            {
                Debug.LogWarning($"[AO Art Repair] Squid prefab has an Animation component but no valid clips. Rebuild it from the AO-imported squid.glb before assigning: {squidPrefabPath}");
                return new SquidAssignStats();
            }

            SquidAssignStats stats = new SquidAssignStats();
            foreach (MonoBehaviour behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (behaviour == null || !IsEditableSceneObject(behaviour.gameObject))
                {
                    continue;
                }

                if (behaviour.GetType().Name != "SquidPulseSchool")
                {
                    continue;
                }

                stats.SquidSchools++;
                var serialized = new SerializedObject(behaviour);
                serialized.UpdateIfRequiredOrScript();
                SerializedProperty creaturePrefab = serialized.FindProperty("creaturePrefab");
                if (creaturePrefab == null)
                {
                    Debug.LogWarning($"[AO Art Repair] SquidPulseSchool has no creaturePrefab field: {behaviour.name}");
                    continue;
                }

                if (creaturePrefab.objectReferenceValue == squidPrefab)
                {
                    stats.AlreadyAssigned++;
                    continue;
                }

                Undo.RecordObject(behaviour, "Assign Squid Baked Prefab");
                creaturePrefab.objectReferenceValue = squidPrefab;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(behaviour);
                EditorSceneManager.MarkSceneDirty(behaviour.gameObject.scene);
                stats.Assigned++;
                Debug.Log($"[AO Art Repair] Assigned squid prefab to {GetTransformPath(behaviour.transform)} -> {squidPrefabPath}");
            }

            AssetDatabase.SaveAssets();
            if (stats.SquidSchools == 0)
            {
                Debug.LogWarning("[AO Art Repair] No SquidPulseSchool components found in open editable scenes.");
            }

            return stats;
        }

        public struct SquidAssignStats
        {
            public int SquidSchools;
            public int Assigned;
            public int AlreadyAssigned;
        }

        [MenuItem("AO/Art/Rebuild GLB Prefabs From AO Importer")]
        public static void RebuildGlbPrefabsFromAoImporter()
        {
            EnsureFolder("Assets/_Project/Prefabs/Environment", "RebuiltFromGlb");

            bool deepBulbsOk = RebuildPrefabFromGlb(
                DeepBulbsGlbPath,
                RebuiltDeepBulbsPrefabPath,
                removeImportedCamerasAndLights: true);

            bool squidOk = RebuildPrefabFromGlb(
                SquidGlbPath,
                RebuiltSquidPrefabPath,
                removeImportedCamerasAndLights: true);

            Debug.Log($"[AO Art Repair] GLB prefab rebuild complete. deep_bulbs={deepBulbsOk}, squid={squidOk}");
        }

        private static void ReplaceMissingPrefabInstance(GameObject missing, GameObject prefab)
        {
            Scene scene = missing.scene;
            Transform oldTransform = missing.transform;
            Transform oldParent = oldTransform.parent;
            int siblingIndex = oldTransform.GetSiblingIndex();
            bool activeSelf = missing.activeSelf;

            Vector3 localPosition = oldTransform.localPosition;
            Quaternion localRotation = oldTransform.localRotation;
            Vector3 localScale = oldTransform.localScale;
            string name = GetDeepBulbPrefabName(missing.name);

            GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(replacement, "Replace Missing Deep Bulb");
            replacement.name = name;
            replacement.SetActive(activeSelf);

            Transform newTransform = replacement.transform;
            Undo.SetTransformParent(newTransform, oldParent, "Parent Replaced Deep Bulb");
            newTransform.SetSiblingIndex(siblingIndex);
            newTransform.localPosition = localPosition;
            newTransform.localRotation = localRotation;
            newTransform.localScale = localScale;

            Undo.DestroyObjectImmediate(missing);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void ClearSelection()
        {
            Selection.objects = new UnityEngine.Object[0];
        }

        private static bool IsEditableSceneObject(GameObject go)
        {
            return go != null
                   && go.scene.IsValid()
                   && go.scene.isLoaded
                   && !EditorUtility.IsPersistent(go);
        }

        private static bool RebuildPrefabFromGlb(string glbPath, string outputPrefabPath, bool removeImportedCamerasAndLights)
        {
            ClearSelection();
            AssetDatabase.ImportAsset(glbPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            if (source == null)
            {
                Debug.LogWarning($"[AO Art Repair] GLB did not import as a GameObject. Check importer in Inspector: {glbPath}");
                return false;
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(glbPath);
            int meshCount = assets.OfType<Mesh>().Count();
            int clipCount = assets.OfType<AnimationClip>().Count(clip => !clip.name.StartsWith("__", StringComparison.Ordinal));

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            GameObject instance = null;

            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(source, previewScene);
                if (instance == null)
                {
                    instance = UnityEngine.Object.Instantiate(source);
                    SceneManager.MoveGameObjectToScene(instance, previewScene);
                }

                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.name = System.IO.Path.GetFileNameWithoutExtension(outputPrefabPath);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                if (removeImportedCamerasAndLights)
                {
                    RemoveImportedCamerasAndLights(instance);
                }

                bool hasRenderable = HasValidRenderableMesh(instance);
                if (!hasRenderable)
                {
                    Debug.LogWarning($"[AO Art Repair] Rebuilt instance has no valid renderable mesh. source={glbPath}, meshes={meshCount}, clips={clipCount}");
                    return false;
                }

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, outputPrefabPath);

                if (prefab == null)
                {
                    Debug.LogWarning($"[AO Art Repair] Failed to save rebuilt prefab: {outputPrefabPath}");
                    return false;
                }

                Debug.Log($"[AO Art Repair] Rebuilt prefab: {outputPrefabPath} (source meshes={meshCount}, animation clips={clipCount})");
                return true;
            }
            finally
            {
                ClearSelection();
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }

                EditorSceneManager.ClosePreviewScene(previewScene);
                EditorApplication.delayCall += ClearSelection;
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void RemoveImportedCamerasAndLights(GameObject root)
        {
            var toRemove = new List<GameObject>();
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            {
                toRemove.Add(camera.gameObject);
            }

            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                if (!toRemove.Contains(light.gameObject))
                {
                    toRemove.Add(light.gameObject);
                }
            }

            foreach (GameObject go in toRemove)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static bool HasValidRenderableMesh(GameObject prefab)
        {
            foreach (MeshFilter meshFilter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh != null)
                {
                    return true;
                }
            }

            foreach (SkinnedMeshRenderer renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
