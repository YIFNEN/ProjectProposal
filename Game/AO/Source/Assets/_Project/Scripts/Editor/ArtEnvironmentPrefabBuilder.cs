using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.EditorTools
{
    internal static class ArtEnvironmentPrefabBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Art_Test.unity";
        private const string PrefabPath = "Assets/_Project/Prefabs/Environment/AO_ArtEnvironment_Final.prefab";
        private const string ArtSceneFolder = "Assets/_Project/Prefabs/Environment/ArtScene";

        private static readonly string[] ObjectNames =
        {
            "FX_OxygenBubbleRise",
            "MixedReefFishSchool_Near",
            "MixedReefFishCenter_Near",
            "YellowSnapperSchool_FarLeft",
            "YellowSnapperCenter_FarLeft",
            "WhiteMulletSchool_FarRight",
            "WhiteMulletCenter_FarRight",
            "YellowfinTunaSchool_Back",
            "YellowfinTunaCenter_Back",
            "GiantSeaBassSchool_FarBack",
            "GiantSeaBassCenter_FarBack",
            "SunfishSingle_SlowRight",
            "SunfishCenter_SlowRight",
            "MantaSingle_DeepBack",
            "MantaCenter_DeepBack",
            "WhaleSingle_FarSilhouette",
            "WhaleCenter_FarSilhouette",
            "FishProjectBlueSchool_NearLeft",
            "FishProjectBlueCenter_NearLeft",
            "FishProjectYellowSchool_NearCenter",
            "FishProjectYellowCenter_NearCenter",
            "FishProjectColorSchool_NearRight",
            "FishProjectColorCenter_NearRight"
        };

        [MenuItem("AO/Art/Create Final Art Environment Prefab")]
        public static void BuildAll()
        {
            Build();
            BuildPlacementPrefab(
                "AO_SeabedTerrain_Placement",
                new[] { "^ground$", "^SM_Seabed_", "^planeCaustics_Plane" });
            BuildPlacementPrefab(
                "AO_Rocks_Placement",
                new[] { "^Rock" });
            BuildPlacementPrefab(
                "AO_SeaweedCoral_Placement",
                new[] { "^Seaweed", "^deep_bulbs" });
            BuildPlacementPrefab(
                "AO_WaterLighting_Placement",
                new[]
                {
                    "^UnderwaterVolume$",
                    "^Global Volume$",
                    "^Reflection Probe$",
                    "^GodRay$",
                    "^godray$",
                    "^Decal Projector$",
                    "^WaterCausticsTexGen$"
                });
        }

        [MenuItem("AO/Art/Create Fish And Bubble Environment Prefab")]
        public static void Build()
        {
            Scene sourceScene = GetOrOpenScene();
            if (!sourceScene.IsValid())
            {
                Debug.LogError($"Could not open art scene at {ScenePath}.");
                return;
            }

            GameObject root = new GameObject("AO_ArtEnvironment_Final");
            Dictionary<string, Transform> clonedTransforms = new Dictionary<string, Transform>();

            foreach (string objectName in ObjectNames)
            {
                GameObject source = FindInScene(sourceScene, objectName);
                if (source == null)
                {
                    Debug.LogWarning($"AO art prefab skipped missing object: {objectName}");
                    continue;
                }

                GameObject clone = Object.Instantiate(source, root.transform);
                clone.name = source.name;
                clone.transform.SetLocalPositionAndRotation(source.transform.position, source.transform.rotation);
                clone.transform.localScale = source.transform.lossyScale;
                clonedTransforms[clone.name] = clone.transform;
            }

            RelinkSchoolCenters(root, clonedTransforms);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Created final art environment prefab: {PrefabPath}");
        }

        private static void BuildPlacementPrefab(string rootName, string[] namePatterns)
        {
            Scene sourceScene = GetOrOpenScene();
            if (!sourceScene.IsValid())
            {
                Debug.LogError($"Could not open art scene at {ScenePath}.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(ArtSceneFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs/Environment", "ArtScene");
            }

            GameObject root = new GameObject(rootName);
            foreach (GameObject sceneRoot in sourceScene.GetRootGameObjects())
            {
                if (!MatchesAny(sceneRoot.name, namePatterns))
                {
                    continue;
                }

                GameObject clone = Object.Instantiate(sceneRoot, root.transform);
                clone.name = sceneRoot.name;
                clone.transform.SetLocalPositionAndRotation(sceneRoot.transform.position, sceneRoot.transform.rotation);
                clone.transform.localScale = sceneRoot.transform.lossyScale;
            }

            string prefabPath = $"{ArtSceneFolder}/{rootName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"Created art scene placement prefab: {prefabPath}");
        }

        private static bool MatchesAny(string objectName, IEnumerable<string> patterns)
        {
            foreach (string pattern in patterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(objectName, pattern))
                {
                    return true;
                }
            }

            return false;
        }

        private static Scene GetOrOpenScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.path == ScenePath)
                {
                    return scene;
                }
            }

            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        private static GameObject FindInScene(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GameObject found = FindInChildren(root.transform, objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static GameObject FindInChildren(Transform parent, string objectName)
        {
            if (parent.name == objectName)
            {
                return parent.gameObject;
            }

            foreach (Transform child in parent)
            {
                GameObject found = FindInChildren(child, objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void RelinkSchoolCenters(GameObject root, IReadOnlyDictionary<string, Transform> clonedTransforms)
        {
            foreach (MonoBehaviour school in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (school == null || school.GetType().Name != "FastNaturalFishSchool") continue;
                string centerName = GetCenterName(school.name);
                if (!clonedTransforms.TryGetValue(centerName, out Transform center))
                {
                    continue;
                }

                SerializedObject serializedSchool = new SerializedObject(school);
                SerializedProperty centerProperty = serializedSchool.FindProperty("schoolCenter");
                centerProperty.objectReferenceValue = center;
                serializedSchool.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static string GetCenterName(string schoolName)
        {
            return schoolName switch
            {
                "MixedReefFishSchool_Near" => "MixedReefFishCenter_Near",
                "YellowSnapperSchool_FarLeft" => "YellowSnapperCenter_FarLeft",
                "WhiteMulletSchool_FarRight" => "WhiteMulletCenter_FarRight",
                "YellowfinTunaSchool_Back" => "YellowfinTunaCenter_Back",
                "GiantSeaBassSchool_FarBack" => "GiantSeaBassCenter_FarBack",
                "SunfishSingle_SlowRight" => "SunfishCenter_SlowRight",
                "MantaSingle_DeepBack" => "MantaCenter_DeepBack",
                "WhaleSingle_FarSilhouette" => "WhaleCenter_FarSilhouette",
                "FishProjectBlueSchool_NearLeft" => "FishProjectBlueCenter_NearLeft",
                "FishProjectYellowSchool_NearCenter" => "FishProjectYellowCenter_NearCenter",
                "FishProjectColorSchool_NearRight" => "FishProjectColorCenter_NearRight",
                _ => string.Empty
            };
        }
    }
}

