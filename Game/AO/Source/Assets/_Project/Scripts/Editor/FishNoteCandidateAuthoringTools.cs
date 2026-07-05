using AO.Notes;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AO.Editor
{
    public static class FishNoteCandidateAuthoringTools
    {
        private const string FishNotePrefabPath = "Assets/_Project/Prefabs/Notes/FishNote_Wrapper.prefab";
        private const string ImportedFishFolder = "Assets/_Project/Prefabs/Environment/Imported/Fish";
        private const string NoteFishFolder = ImportedFishFolder + "/Note";
        private const string VisualHeadAnchorName = "FishNoteHeadAnchor";
        private static readonly string[] VisualHeadAnchorNames =
        {
            VisualHeadAnchorName,
            "HeadAnchor",
            "FishHeadAnchor",
            "TouchAnchor"
        };

        private static readonly Vector3 DefaultLocalScale = Vector3.one;
        private static readonly Vector3 DefaultHeadLocalPosition = new Vector3(0f, 0f, 0.25f);
        private const float DefaultStrokeOverlapRadius = 0.26f;
        private const float DefaultStrokeOverlapPadding = 0.08f;

        [MenuItem("AO/Notes/Add Selected Imported Fish Prefabs To FishNote")]
        public static void AddSelectedImportedFishPrefabs()
        {
            GameObject[] selectedPrefabs = Selection.GetFiltered<GameObject>(SelectionMode.Assets);
            if (selectedPrefabs == null || selectedPrefabs.Length == 0)
            {
                Debug.LogWarning("[AO] Select one or more fish prefabs in Assets/_Project/Prefabs/Environment/Imported/Fish first.");
                return;
            }

            GameObject fishNoteRoot = PrefabUtility.LoadPrefabContents(FishNotePrefabPath);
            try
            {
                FishNote fishNote = fishNoteRoot.GetComponent<FishNote>();
                if (fishNote == null)
                {
                    Debug.LogError($"[AO] FishNote component is missing from {FishNotePrefabPath}.");
                    return;
                }

                SerializedObject serializedFish = new SerializedObject(fishNote);
                SerializedProperty candidates = serializedFish.FindProperty("_visualCandidates");
                if (candidates == null)
                {
                    Debug.LogError("[AO] FishNote._visualCandidates was not found.");
                    return;
                }

                int added = 0;
                int skipped = 0;
                for (int i = 0; i < selectedPrefabs.Length; i++)
                {
                    GameObject prefab = selectedPrefabs[i];
                    string path = AssetDatabase.GetAssetPath(prefab);
                    if (!IsImportedFishPrefab(path, prefab))
                    {
                        skipped++;
                        continue;
                    }

                    if (ContainsPrefab(candidates, prefab))
                    {
                        skipped++;
                        continue;
                    }

                    int index = candidates.arraySize;
                    candidates.InsertArrayElementAtIndex(index);
                    SerializedProperty item = candidates.GetArrayElementAtIndex(index);
                    SetChildString(item, "Id", prefab.name);
                    SetChildObject(item, "Prefab", prefab);
                    SetChildVector3(item, "LocalPosition", Vector3.zero);
                    SetChildVector3(item, "LocalEulerAngles", Vector3.zero);
                    SetChildVector3(item, "LocalScale", GetDefaultLocalScale(path));
                    SetChildVector3(item, "HeadLocalPosition", DefaultHeadLocalPosition);
                    SetChildFloat(item, "StrokeOverlapRadius", DefaultStrokeOverlapRadius);
                    SetChildFloat(item, "StrokeOverlapPadding", DefaultStrokeOverlapPadding);
                    added++;
                }

                serializedFish.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(fishNote);
                PrefabUtility.SaveAsPrefabAsset(fishNoteRoot, FishNotePrefabPath);
                Debug.Log($"[AO] FishNote visual candidates updated. Added {added}, skipped {skipped}. Tune visual prefab scale/rotation in the Note folder, and use LocalScale/LocalEulerAngles only as extra multipliers or offsets on FishNote_Wrapper.prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(fishNoteRoot);
            }
        }

        [MenuItem("AO/Notes/Add Selected Imported Fish Prefabs To FishNote", true)]
        public static bool CanAddSelectedImportedFishPrefabs()
        {
            GameObject[] selectedPrefabs = Selection.GetFiltered<GameObject>(SelectionMode.Assets);
            if (selectedPrefabs == null || selectedPrefabs.Length == 0) return false;

            for (int i = 0; i < selectedPrefabs.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(selectedPrefabs[i]);
                if (IsImportedFishPrefab(path, selectedPrefabs[i])) return true;
            }

            return false;
        }

        [MenuItem("AO/Notes/Ensure Head Anchors On FishNote Candidates")]
        public static void EnsureHeadAnchorsOnFishNoteCandidates()
        {
            EnsureHeadAnchorsOnFishNoteCandidates(overwriteExisting: false);
        }

        [MenuItem("AO/Notes/Reset Head Anchors From FishNote Candidate Values")]
        public static void ResetHeadAnchorsFromFishNoteCandidateValues()
        {
            EnsureHeadAnchorsOnFishNoteCandidates(overwriteExisting: true);
        }

        [MenuItem("AO/Notes/Select FishNote Prefab")]
        public static void SelectFishNotePrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FishNotePrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[AO] FishNote prefab not found: {FishNotePrefabPath}");
                return;
            }

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        [MenuItem("AO/Notes/Select Imported Fish Folder")]
        public static void SelectImportedFishFolder()
        {
            Object folder = AssetDatabase.LoadAssetAtPath<Object>(ImportedFishFolder);
            if (folder == null)
            {
                Debug.LogError($"[AO] Imported fish folder not found: {ImportedFishFolder}");
                return;
            }

            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }

        private static void EnsureHeadAnchorsOnFishNoteCandidates(bool overwriteExisting)
        {
            GameObject fishNoteRoot = PrefabUtility.LoadPrefabContents(FishNotePrefabPath);
            int created = 0;
            int updated = 0;
            int skipped = 0;

            try
            {
                FishNote fishNote = fishNoteRoot.GetComponent<FishNote>();
                if (fishNote == null)
                {
                    Debug.LogError($"[AO] FishNote component is missing from {FishNotePrefabPath}.");
                    return;
                }

                SerializedObject serializedFish = new SerializedObject(fishNote);
                SerializedProperty candidates = serializedFish.FindProperty("_visualCandidates");
                if (candidates == null)
                {
                    Debug.LogError("[AO] FishNote._visualCandidates was not found.");
                    return;
                }

                HashSet<string> processedPrefabPaths = new HashSet<string>();
                for (int i = 0; i < candidates.arraySize; i++)
                {
                    SerializedProperty item = candidates.GetArrayElementAtIndex(i);
                    GameObject prefab = GetChildObject(item, "Prefab") as GameObject;
                    if (prefab == null)
                    {
                        skipped++;
                        continue;
                    }

                    string prefabPath = AssetDatabase.GetAssetPath(prefab);
                    if (string.IsNullOrEmpty(prefabPath) || !prefabPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                    {
                        skipped++;
                        continue;
                    }

                    if (!processedPrefabPaths.Add(prefabPath))
                    {
                        skipped++;
                        continue;
                    }

                    Vector3 localPosition = GetChildVector3(item, "LocalPosition", Vector3.zero);
                    Vector3 localEuler = GetChildVector3(item, "LocalEulerAngles", Vector3.zero);
                    Vector3 localScale = GetChildVector3(item, "LocalScale", Vector3.one);
                    Vector3 headLocalPosition = GetChildVector3(item, "HeadLocalPosition", DefaultHeadLocalPosition);
                    GameObject visualRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                    try
                    {
                        Vector3 markerLocalPosition = CandidateHeadToPrefabLocal(
                            headLocalPosition,
                            localPosition,
                            visualRoot.transform.localRotation,
                            localEuler,
                            visualRoot.transform.localScale,
                            localScale);
                        Transform marker = FindHeadAnchor(visualRoot.transform);
                        if (marker == null)
                        {
                            GameObject markerObject = new GameObject(VisualHeadAnchorName);
                            marker = markerObject.transform;
                            marker.SetParent(visualRoot.transform, false);
                            marker.localPosition = markerLocalPosition;
                            marker.localRotation = Quaternion.identity;
                            marker.localScale = Vector3.one;
                            markerObject.layer = visualRoot.layer;
                            created++;
                        }
                        else if (overwriteExisting)
                        {
                            marker.localPosition = markerLocalPosition;
                            marker.localRotation = Quaternion.identity;
                            marker.localScale = Vector3.one;
                            updated++;
                        }
                        else
                        {
                            skipped++;
                        }

                        EditorUtility.SetDirty(marker);
                        PrefabUtility.SaveAsPrefabAsset(visualRoot, prefabPath);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(visualRoot);
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(fishNoteRoot);
            }

            Debug.Log($"[AO] FishNote head anchors ensured. Created {created}, updated {updated}, skipped {skipped}. Move '{VisualHeadAnchorName}' inside each visual prefab to tune the actual FishNote head/touch point.");
        }

        private static bool IsImportedFishPrefab(string path, GameObject prefab)
        {
            if (prefab == null || string.IsNullOrEmpty(path)) return false;
            if (!path.StartsWith(ImportedFishFolder + "/", System.StringComparison.Ordinal)) return false;
            if (!path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)) return false;
            return PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.NotAPrefab;
        }

        private static Vector3 GetDefaultLocalScale(string prefabPath)
        {
            return prefabPath.StartsWith(NoteFishFolder + "/", System.StringComparison.Ordinal)
                ? Vector3.one
                : DefaultLocalScale;
        }

        private static bool ContainsPrefab(SerializedProperty candidates, Object prefab)
        {
            for (int i = 0; i < candidates.arraySize; i++)
            {
                SerializedProperty item = candidates.GetArrayElementAtIndex(i);
                SerializedProperty prefabProperty = item.FindPropertyRelative("Prefab");
                if (prefabProperty != null && prefabProperty.objectReferenceValue == prefab) return true;
            }

            return false;
        }

        private static void SetChildString(SerializedProperty parent, string name, string value)
        {
            SerializedProperty prop = parent.FindPropertyRelative(name);
            if (prop != null) prop.stringValue = value;
        }

        private static void SetChildObject(SerializedProperty parent, string name, Object value)
        {
            SerializedProperty prop = parent.FindPropertyRelative(name);
            if (prop != null) prop.objectReferenceValue = value;
        }

        private static Object GetChildObject(SerializedProperty parent, string name)
        {
            SerializedProperty prop = parent.FindPropertyRelative(name);
            return prop != null ? prop.objectReferenceValue : null;
        }

        private static void SetChildFloat(SerializedProperty parent, string name, float value)
        {
            SerializedProperty prop = parent.FindPropertyRelative(name);
            if (prop != null) prop.floatValue = value;
        }

        private static void SetChildVector3(SerializedProperty parent, string name, Vector3 value)
        {
            SerializedProperty prop = parent.FindPropertyRelative(name);
            if (prop != null) prop.vector3Value = value;
        }

        private static Vector3 GetChildVector3(SerializedProperty parent, string name, Vector3 fallback)
        {
            SerializedProperty prop = parent.FindPropertyRelative(name);
            return prop != null ? prop.vector3Value : fallback;
        }

        private static Vector3 CandidateHeadToPrefabLocal(
            Vector3 headLocalPosition,
            Vector3 localPosition,
            Quaternion authoredLocalRotation,
            Vector3 localEulerAngles,
            Vector3 authoredLocalScale,
            Vector3 localScale)
        {
            Quaternion rotation = authoredLocalRotation * Quaternion.Euler(localEulerAngles);
            Vector3 finalScale = Vector3.Scale(authoredLocalScale, localScale);
            Vector3 unrotated = Quaternion.Inverse(rotation) * (headLocalPosition - localPosition);
            return new Vector3(
                SafeDivide(unrotated.x, finalScale.x),
                SafeDivide(unrotated.y, finalScale.y),
                SafeDivide(unrotated.z, finalScale.z));
        }

        private static float SafeDivide(float value, float scale)
        {
            return Mathf.Abs(scale) > 0.0001f ? value / scale : value;
        }

        private static Transform FindHeadAnchor(Transform root)
        {
            if (root == null) return null;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int nameIndex = 0; nameIndex < VisualHeadAnchorNames.Length; nameIndex++)
            {
                string anchorName = VisualHeadAnchorNames[nameIndex];
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform candidate = transforms[i];
                    if (candidate == root) continue;
                    if (string.Equals(candidate.name, anchorName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }
    }
}
