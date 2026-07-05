using System.IO;
using UnityEditor;
using UnityEngine;

namespace AO.EditorTools
{
    internal static class FishProjectAssetReimporter
    {
        private const string ModelPath = "Assets/_Project/Art/Environment/Models/Fish-Project";
        private const string MaterialPath = "Assets/_Project/Art/Environment/Materials/Fish-Project";
        private const string TexturePath = "Assets/_Project/Art/Environment/Textures/Fish-Project";
        private const string PrefabPath = "Assets/_Project/Prefabs/Environment/Fish";

        [MenuItem("AO/Art/Reimport Fish Project Assets")]
        private static void ReimportFishProjectAssets()
        {
            const ImportAssetOptions options =
                ImportAssetOptions.ForceUpdate |
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ImportRecursive;

            AssetDatabase.ImportAsset(ModelPath, options);
            AssetDatabase.ImportAsset(MaterialPath, options);
            AssetDatabase.ImportAsset(TexturePath, options);
            AssetDatabase.ImportAsset(PrefabPath, options);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [MenuItem("AO/Art/Rebuild Fish Project Prefabs")]
        public static void RebuildFishProjectPrefabs()
        {
            ReimportFishProjectAssets();

            int savedCount = 0;
            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { ModelPath });
            foreach (string guid in modelGuids)
            {
                string modelAssetPath = AssetDatabase.GUIDToAssetPath(guid);
                string prefabName = Path.GetFileNameWithoutExtension(modelAssetPath);
                string prefabAssetPath = $"{PrefabPath}/{prefabName}.prefab";

                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetPath);
                if (model == null)
                {
                    Debug.LogWarning($"Could not load fish model: {modelAssetPath}");
                    continue;
                }

                GameObject instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
                if (instance == null)
                {
                    instance = Object.Instantiate(model);
                }

                instance.name = prefabName;
                AssignMatchingMaterial(instance, prefabName);
                PrefabUtility.SaveAsPrefabAsset(instance, prefabAssetPath);
                Object.DestroyImmediate(instance);
                savedCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"Rebuilt {savedCount} Fish-Project prefabs from visible model assets.");
        }

        [MenuItem("AO/Art/Repair Fish Project Materials")]
        public static void RepairFishProjectMaterials()
        {
            int materialCount = RepairDoubleSidedMaterials();
            int prefabCount = RepairPrefabMaterialOverrides();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"Repaired {materialCount} Fish-Project materials and {prefabCount} prefabs.");
        }

        private static void AssignMatchingMaterial(GameObject instance, string prefabName)
        {
            string materialName = prefabName == "Yellow_Fish_06" ? "Yellow_Fish_06_" : prefabName;
            string materialPath = $"{MaterialPath}/{materialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                return;
            }

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                AssignMaterialToAllSlots(renderer, material);
            }
        }

        private static int RepairDoubleSidedMaterials()
        {
            int count = 0;
            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { MaterialPath });
            foreach (string guid in materialGuids)
            {
                string materialAssetPath = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty("_Cull"))
                {
                    material.SetFloat("_Cull", 0f);
                }

                material.doubleSidedGI = true;
                EditorUtility.SetDirty(material);
                count++;
            }

            return count;
        }

        private static int RepairPrefabMaterialOverrides()
        {
            int count = 0;
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabPath });
            foreach (string guid in prefabGuids)
            {
                string prefabAssetPath = AssetDatabase.GUIDToAssetPath(guid);
                string prefabName = Path.GetFileNameWithoutExtension(prefabAssetPath);
                if (!File.Exists($"{ModelPath}/{prefabName}.FBX"))
                {
                    continue;
                }

                string materialName = prefabName == "Yellow_Fish_06" ? "Yellow_Fish_06_" : prefabName;
                Material material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialPath}/{materialName}.mat");
                if (material == null)
                {
                    Debug.LogWarning($"Could not find Fish-Project material for prefab: {prefabName}");
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabAssetPath);
                Renderer[] renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    AssignMaterialToAllSlots(renderer, material);
                    EditorUtility.SetDirty(renderer);
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabAssetPath);
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                count++;
            }

            return count;
        }

        private static void AssignMaterialToAllSlots(Renderer renderer, Material material)
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                renderer.sharedMaterial = material;
                return;
            }

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            renderer.sharedMaterials = materials;
        }
    }
}
