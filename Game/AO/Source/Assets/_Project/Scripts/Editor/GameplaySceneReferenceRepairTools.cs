using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class GameplaySceneReferenceRepairTools
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const string ImportedFishFolder = "Assets/_Project/Prefabs/Environment/Imported/Fish";

        [MenuItem("AO/Setup/Validate Gameplay Scene References")]
        public static void ValidateGameplaySceneReferences()
        {
            Run(repair: false);
        }

        [MenuItem("AO/Setup/Repair Gameplay Scene References")]
        public static void RepairGameplaySceneReferences()
        {
            Run(repair: true);
        }

        private static void Run(bool repair)
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            RepairStats stats = new RepairStats();

            List<GameObject> objects = GetSceneObjects(scene);
            foreach (GameObject go in objects)
            {
                InspectMissingScripts(go, repair, ref stats);
                InspectMissingPrefab(go, ref stats);
            }

            foreach (MonoBehaviour behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (!IsEditableSceneObject(behaviour != null ? behaviour.gameObject : null, scene)) continue;
                InspectMissingObjectReferences(behaviour, ref stats);
            }

            foreach (MonoBehaviour school in FindSceneBehaviours(scene, "FastNaturalFishSchool"))
            {
                RepairFishSchool(school, repair, ref stats);
            }

            foreach (MonoBehaviour wander in FindSceneBehaviours(scene, "SchoolCenterWander"))
            {
                RepairSchoolCenterWander(wander, repair, ref stats);
            }

            if (repair)
            {
                stats.ReplacedDeepBulbs += ArtImportRepairTools.ReplaceMissingDeepBulbsInOpenScenes();
                ArtImportRepairTools.SquidAssignStats squidStats = ArtImportRepairTools.AssignSquidPrefabInOpenScenes();
                stats.AssignedSquidPrefabs += squidStats.Assigned;

                if (stats.Dirty)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    AssetDatabase.SaveAssets();
                }
            }

            string mode = repair ? "Repair" : "Validate";
            Debug.Log(
                $"[AO GameplayScene Reference {mode}] " +
                $"MissingScripts={stats.MissingScripts}, RemovedMissingScripts={stats.RemovedMissingScripts}, " +
                $"MissingPrefabs={stats.MissingPrefabs}, MissingObjectRefs={stats.MissingObjectRefs}, " +
                $"FishSchools={stats.FishSchools}, FishSchoolsFixed={stats.FishSchoolsFixed}, " +
                $"FishPrefabFallbacks={stats.FishPrefabFallbacks}, FishPrefabArraysCleaned={stats.FishPrefabArraysCleaned}, " +
                $"FishSchoolsWithoutUsablePrefab={stats.FishSchoolsWithoutUsablePrefab}, " +
                $"SchoolCentersFixed={stats.SchoolCentersFixed}, ReplacedDeepBulbs={stats.ReplacedDeepBulbs}, " +
                $"AssignedSquidPrefabs={stats.AssignedSquidPrefabs}");
        }

        private static void InspectMissingScripts(GameObject go, bool repair, ref RepairStats stats)
        {
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missing <= 0) return;

            stats.MissingScripts += missing;
            Debug.LogWarning($"[AO GameplayScene Reference] Missing script component(s): {GetPath(go.transform)} count={missing}", go);

            if (!repair) return;

            Undo.RegisterCompleteObjectUndo(go, "Remove Missing Script Components");
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            if (removed > 0)
            {
                stats.RemovedMissingScripts += removed;
                stats.Dirty = true;
                EditorUtility.SetDirty(go);
                Debug.Log($"[AO GameplayScene Reference] Removed missing script component(s): {GetPath(go.transform)} count={removed}", go);
            }
        }

        private static void InspectMissingPrefab(GameObject go, ref RepairStats stats)
        {
            bool missingPrefab = go.name.Contains("(Missing Prefab", StringComparison.Ordinal)
                || PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.MissingAsset;
            if (!missingPrefab) return;

            stats.MissingPrefabs++;
            Debug.LogWarning($"[AO GameplayScene Reference] Missing prefab instance: {GetPath(go.transform)}", go);
        }

        private static void InspectMissingObjectReferences(MonoBehaviour behaviour, ref RepairStats stats)
        {
            SerializedObject serializedObject = new SerializedObject(behaviour);
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (property.objectReferenceValue != null) continue;
                if (property.objectReferenceInstanceIDValue == 0) continue;

                stats.MissingObjectRefs++;
                Debug.LogWarning(
                    $"[AO GameplayScene Reference] Missing object reference: {GetPath(behaviour.transform)}.{property.propertyPath}",
                    behaviour);
            }
        }

        private static void RepairFishSchool(MonoBehaviour school, bool repair, ref RepairStats stats)
        {
            stats.FishSchools++;
            bool changed = false;
            List<GameObject> usablePrefabs = GetUsablePrefabs(school);
            SerializedObject serializedSchool = new SerializedObject(school);

            if (!school.enabled)
            {
                Debug.LogWarning($"[AO GameplayScene Reference] Disabled FastNaturalFishSchool: {GetPath(school.transform)}", school);
                if (repair)
                {
                    Undo.RecordObject(school, "Enable FastNaturalFishSchool");
                    school.enabled = true;
                    changed = true;
                }
            }

            SerializedProperty fishCount = serializedSchool.FindProperty("fishCount");
            if (fishCount != null && fishCount.intValue < 1)
            {
                Debug.LogWarning($"[AO GameplayScene Reference] FastNaturalFishSchool has fishCount < 1: {GetPath(school.transform)}", school);
                if (repair)
                {
                    Undo.RecordObject(school, "Repair Fish Count");
                    fishCount.intValue = 1;
                    changed = true;
                }
            }

            SerializedProperty minSpeed = serializedSchool.FindProperty("minSpeed");
            SerializedProperty maxSpeed = serializedSchool.FindProperty("maxSpeed");
            if ((minSpeed != null && minSpeed.floatValue <= 0f) || (maxSpeed != null && maxSpeed.floatValue <= 0f))
            {
                Debug.LogWarning($"[AO GameplayScene Reference] FastNaturalFishSchool has non-positive speed: {GetPath(school.transform)}", school);
                if (repair)
                {
                    Undo.RecordObject(school, "Repair Fish Speed");
                    if (minSpeed != null) minSpeed.floatValue = Mathf.Max(1f, minSpeed.floatValue);
                    if (maxSpeed != null) maxSpeed.floatValue = Mathf.Max((minSpeed != null ? minSpeed.floatValue : 1f) + 1f, maxSpeed.floatValue);
                    changed = true;
                }
            }

            if (minSpeed != null && maxSpeed != null && maxSpeed.floatValue < minSpeed.floatValue)
            {
                Debug.LogWarning($"[AO GameplayScene Reference] FastNaturalFishSchool maxSpeed < minSpeed: {GetPath(school.transform)}", school);
                if (repair)
                {
                    Undo.RecordObject(school, "Repair Fish Speed Range");
                    maxSpeed.floatValue = minSpeed.floatValue + 1f;
                    changed = true;
                }
            }

            if (usablePrefabs.Count == 0)
            {
                GameObject inferred = InferFishPrefab(school.name);
                if (inferred == null) inferred = LoadFishPrefab("Yellow_Fish_08");

                Debug.LogWarning($"[AO GameplayScene Reference] FastNaturalFishSchool has no usable fish prefab: {GetPath(school.transform)}", school);
                if (repair && inferred != null)
                {
                    Undo.RecordObject(school, "Reconnect Fish School Prefab");
                    SetObject(serializedSchool, "fishPrefab", inferred);
                    SetObjectArray(serializedSchool, "fishPrefabs", new[] { inferred });
                    usablePrefabs.Add(inferred);
                    stats.FishPrefabFallbacks++;
                    changed = true;
                    Debug.Log($"[AO GameplayScene Reference] Reconnected fish school prefab: {GetPath(school.transform)} -> {AssetDatabase.GetAssetPath(inferred)}", school);
                }
                else
                {
                    stats.FishSchoolsWithoutUsablePrefab++;
                }
            }

            SerializedProperty fishPrefab = serializedSchool.FindProperty("fishPrefab");
            if (usablePrefabs.Count > 0 && fishPrefab != null && fishPrefab.objectReferenceValue == null)
            {
                Debug.LogWarning($"[AO GameplayScene Reference] FastNaturalFishSchool fishPrefab fallback slot is empty: {GetPath(school.transform)}", school);
                if (repair)
                {
                    Undo.RecordObject(school, "Set Fish Prefab Fallback");
                    fishPrefab.objectReferenceValue = usablePrefabs[0];
                    stats.FishPrefabFallbacks++;
                    changed = true;
                }
            }

            SerializedProperty fishPrefabs = serializedSchool.FindProperty("fishPrefabs");
            if (usablePrefabs.Count > 0 && NeedsPrefabArrayCleanup(fishPrefabs, usablePrefabs))
            {
                Debug.LogWarning($"[AO GameplayScene Reference] FastNaturalFishSchool fishPrefabs array has empty/duplicate/invalid entries: {GetPath(school.transform)}", school);
                if (repair)
                {
                    Undo.RecordObject(school, "Clean Fish Prefab Array");
                    SetObjectArray(serializedSchool, "fishPrefabs", usablePrefabs);
                    stats.FishPrefabArraysCleaned++;
                    changed = true;
                }
            }

            SerializedProperty schoolCenter = serializedSchool.FindProperty("schoolCenter");
            Transform center = schoolCenter != null ? schoolCenter.objectReferenceValue as Transform : null;
            if (center != null)
            {
                MonoBehaviour wander = GetNamedBehaviour(center.gameObject, "SchoolCenterWander");
                if (wander != null)
                {
                    bool centerChanged = RepairSchoolCenterWander(wander, repair, ref stats);
                    changed |= centerChanged;
                }
            }

            if (changed)
            {
                serializedSchool.ApplyModifiedPropertiesWithoutUndo();
                stats.FishSchoolsFixed++;
                stats.Dirty = true;
                EditorUtility.SetDirty(school);
            }
        }

        private static bool RepairSchoolCenterWander(MonoBehaviour wander, bool repair, ref RepairStats stats)
        {
            bool changed = false;
            SerializedObject serializedWander = new SerializedObject(wander);

            if (!wander.enabled)
            {
                Debug.LogWarning($"[AO GameplayScene Reference] Disabled SchoolCenterWander: {GetPath(wander.transform)}", wander);
                if (repair)
                {
                    Undo.RecordObject(wander, "Enable SchoolCenterWander");
                    wander.enabled = true;
                    changed = true;
                }
            }

            SerializedProperty moveSpeed = serializedWander.FindProperty("moveSpeed");
            if (moveSpeed != null && moveSpeed.floatValue <= 0f)
            {
                Debug.LogWarning($"[AO GameplayScene Reference] SchoolCenterWander has moveSpeed <= 0: {GetPath(wander.transform)}", wander);
                if (repair)
                {
                    Undo.RecordObject(wander, "Repair School Center Move Speed");
                    moveSpeed.floatValue = 0.02f;
                    changed = true;
                }
            }

            SerializedProperty smoothTime = serializedWander.FindProperty("smoothTime");
            if (smoothTime != null && smoothTime.floatValue <= 0f)
            {
                Debug.LogWarning($"[AO GameplayScene Reference] SchoolCenterWander has smoothTime <= 0: {GetPath(wander.transform)}", wander);
                if (repair)
                {
                    Undo.RecordObject(wander, "Repair School Center Smooth Time");
                    smoothTime.floatValue = 1f;
                    changed = true;
                }
            }

            if (changed)
            {
                serializedWander.ApplyModifiedPropertiesWithoutUndo();
                stats.SchoolCentersFixed++;
                stats.Dirty = true;
                EditorUtility.SetDirty(wander);
            }

            return changed;
        }

        private static List<GameObject> GetUsablePrefabs(MonoBehaviour school)
        {
            List<GameObject> result = new List<GameObject>();
            SerializedObject serializedSchool = new SerializedObject(school);
            SerializedProperty fishPrefab = serializedSchool.FindProperty("fishPrefab");
            AddIfUsable(result, fishPrefab != null ? fishPrefab.objectReferenceValue as GameObject : null);
            SerializedProperty fishPrefabs = serializedSchool.FindProperty("fishPrefabs");
            if (fishPrefabs != null && fishPrefabs.isArray)
            {
                for (int i = 0; i < fishPrefabs.arraySize; i++)
                {
                    try
                    {
                        AddIfUsable(result, fishPrefabs.GetArrayElementAtIndex(i).objectReferenceValue as GameObject);
                    }
                    catch (MissingReferenceException)
                    {
                    }
                }
            }

            return result;
        }

        private static void AddIfUsable(List<GameObject> result, GameObject prefab)
        {
            if (!IsUsableFishPrefab(prefab)) return;
            if (result.Contains(prefab)) return;
            result.Add(prefab);
        }

        private static bool NeedsPrefabArrayCleanup(SerializedProperty current, List<GameObject> usable)
        {
            if (current == null || !current.isArray || current.arraySize != usable.Count) return true;

            for (int i = 0; i < current.arraySize; i++)
            {
                GameObject prefab = current.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (!IsUsableFishPrefab(prefab)) return true;
                if (prefab != usable[i]) return true;
            }

            return false;
        }

        private static bool IsUsableFishPrefab(GameObject prefab)
        {
            try
            {
                if (prefab == null) return false;

                string prefabName = prefab.name;
                if (string.IsNullOrWhiteSpace(prefabName)) return false;

                if (EditorUtility.IsPersistent(prefab) && string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(prefab)))
                {
                    return false;
                }

                return prefab.GetComponentInChildren<Renderer>(true) != null;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (NullReferenceException)
            {
                return false;
            }
        }

        private static GameObject InferFishPrefab(string schoolName)
        {
            string key = schoolName.ToLowerInvariant();
            if (key.Contains("yellowfin") || key.Contains("tuna")) return LoadFishPrefab("yellowfin_tuna");
            if (key.Contains("bass")) return LoadFishPrefab("giant_sea_bass");
            if (key.Contains("whale")) return LoadFishPrefab("Whale");
            if (key.Contains("dolphin")) return LoadFishPrefab("dolphin");
            if (key.Contains("snapper")) return LoadFishPrefab("yellow_snapper");
            if (key.Contains("box")) return LoadFishPrefab("yellow_boxfish");
            if (key.Contains("mullet")) return LoadFishPrefab("white_mullet");
            if (key.Contains("grunt")) return LoadFishPrefab("white_grunt");
            if (key.Contains("whitefish")) return LoadFishPrefab("whitefish");
            if (key.Contains("trumpet")) return LoadFishPrefab("trumpetfish");
            if (key.Contains("trewavas")) return LoadFishPrefab("trewavas_cichlid");
            if (key.Contains("sunfish")) return LoadFishPrefab("sunfish");
            if (key.Contains("manta") || key.Contains("ray")) return LoadFishPrefab("MANTA");
            if (key.Contains("cyan")) return LoadFishPrefab("Cyan_Fish");
            if (key.Contains("blue")) return LoadFishPrefab("Blue_Fish_03");
            if (key.Contains("violet")) return LoadFishPrefab("Violet_Fish");
            if (key.Contains("red")) return LoadFishPrefab("Red_Fish");
            if (key.Contains("pink")) return LoadFishPrefab("Pink_Fish");
            return null;
        }

        private static GameObject LoadFishPrefab(string prefabName)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{ImportedFishFolder}/{prefabName}.prefab");
        }

        private static List<GameObject> GetSceneObjects(Scene scene)
        {
            List<GameObject> result = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                AddObjectRecursive(root, result);
            }

            return result;
        }

        private static void AddObjectRecursive(GameObject go, List<GameObject> result)
        {
            result.Add(go);
            Transform transform = go.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                AddObjectRecursive(transform.GetChild(i).gameObject, result);
            }
        }

        private static bool IsEditableSceneObject(GameObject go, Scene scene)
        {
            return go != null
                && go.scene == scene
                && go.scene.IsValid()
                && go.scene.isLoaded
                && !EditorUtility.IsPersistent(go);
        }

        private static IEnumerable<MonoBehaviour> FindSceneBehaviours(Scene scene, string typeName)
        {
            foreach (MonoBehaviour behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (behaviour == null || behaviour.GetType().Name != typeName) continue;
                if (!IsEditableSceneObject(behaviour.gameObject, scene)) continue;
                yield return behaviour;
            }
        }

        private static MonoBehaviour GetNamedBehaviour(GameObject go, string typeName)
        {
            if (go == null) return null;
            MonoBehaviour[] behaviours = go.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null && behaviours[i].GetType().Name == typeName) return behaviours[i];
            }

            return null;
        }

        private static void SetObject(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = value;
        }

        private static void SetObjectArray(SerializedObject serializedObject, string propertyName, IReadOnlyList<GameObject> values)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || !property.isArray) return;

            property.arraySize = values != null ? values.Count : 0;
            for (int i = 0; values != null && i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static string GetPath(Transform transform)
        {
            if (transform == null) return "<null>";

            List<string> names = new List<string>();
            while (transform != null)
            {
                names.Add(transform.name);
                transform = transform.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private struct RepairStats
        {
            public bool Dirty;
            public int MissingScripts;
            public int RemovedMissingScripts;
            public int MissingPrefabs;
            public int MissingObjectRefs;
            public int FishSchools;
            public int FishSchoolsFixed;
            public int FishPrefabFallbacks;
            public int FishPrefabArraysCleaned;
            public int FishSchoolsWithoutUsablePrefab;
            public int SchoolCentersFixed;
            public int ReplacedDeepBulbs;
            public int AssignedSquidPrefabs;
        }
    }
}
