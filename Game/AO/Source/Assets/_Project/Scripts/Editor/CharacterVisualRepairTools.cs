using System.Collections.Generic;
using System.IO;
using AO.Character;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class CharacterVisualRepairTools
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const string CharacterPrefabPath = "Assets/_Project/Prefabs/Player/PF_AO_Character_VRM10.prefab";
        private const string ClothesMaterialPath = "Assets/_Project/Art/Characters/Materials/M_AO_Character_Clothes_URP.mat";
        private const string ClothesAlbedoPath = "Assets/_Project/Art/Characters/Models/VRM1.0/AO_Model_Test_1_Clothes_AlbedoTransparency.png";
        private const string ClothesNormalPath = "Assets/_Project/Art/Characters/Models/VRM1.0/AO_Model_Test_1_Clothes_Normal.png";
        private const string ClothesMetallicPath = "Assets/_Project/Art/Characters/Models/VRM1.0/AO_Model_Test_1_Clothes_MetallicSmoothness.png";

        private const float InteractionRootBelowHit = 0.28f;

        [MenuItem("AO/Character/Fix Character Clothes Material Only")]
        public static void FixCharacterClothesMaterialOnly()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ApplyInternal();
        }

        public static void ApplyFromCommandLine()
        {
            ApplyInternal();
        }

        [MenuItem("AO/Character/Align Rider To Current Camera Interaction (Fixed Runtime)")]
        public static void AlignRiderToCameraInteraction()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ApplyInteractionAlignmentInternal();
        }

        public static void AlignRiderToCameraInteractionFromCommandLine()
        {
            ApplyInteractionAlignmentInternal();
        }

        private static void ApplyInternal()
        {
            AssetDatabase.Refresh();

            Material clothesMaterial = EnsureClothesMaterial();
            if (clothesMaterial == null)
            {
                Debug.LogError("[AO] Character visual repair aborted because the clothes material could not be created.");
                return;
            }

            int prefabAssignments = ApplyClothesMaterialToPrefab(clothesMaterial);
            int sceneAssignments = 0;

            if (File.Exists(GameplayScenePath))
            {
                Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
                Transform visualRig = FindSceneTransform("VisualRig");
                if (visualRig != null)
                {
                    sceneAssignments = ApplyClothesMaterialToHierarchy(visualRig.gameObject, clothesMaterial);
                }
                else
                {
                    Debug.LogWarning("[AO] VisualRig was not found in GamePlayScene. Clothes material was only applied to the prefab asset.");
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            else
            {
                Debug.LogWarning($"[AO] Gameplay scene not found: {GameplayScenePath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[AO] Character visual repair complete.\n" +
                $"Clothes material: {ClothesMaterialPath}\n" +
                $"Prefab clothes assignments: {prefabAssignments}\n" +
                $"Scene clothes assignments: {sceneAssignments}\n" +
                "Scene-authored CharacterRoot/VisualRig/MANTA positions were not changed.");
        }

        private static void ApplyInteractionAlignmentInternal()
        {
            if (!File.Exists(GameplayScenePath))
            {
                Debug.LogError($"[AO] Gameplay scene not found: {GameplayScenePath}");
                return;
            }

            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            Transform hitAnchor = FindSceneTransform("HitAnchor");
            Transform mantaRoot = FindSceneTransform("MantaRoot");
            Transform characterRoot = FindSceneTransform("CharacterRoot");
            Transform camera = Camera.main != null ? Camera.main.transform : FindSceneTransform("Main Camera");
            DVariantRiderRig riderRig = mantaRoot != null ? mantaRoot.GetComponent<DVariantRiderRig>() : null;

            if (hitAnchor == null || mantaRoot == null || characterRoot == null || riderRig == null)
            {
                Debug.LogError("[AO] Interaction alignment needs HitAnchor, MantaRoot, CharacterRoot, and DVariantRiderRig in GamePlayScene.");
                return;
            }

            Vector3 hitLocal = new Vector3(0f, InteractionRootBelowHit, 0f);
            SetSerialized(riderRig, "_hitAnchorLocal", hitLocal);
            SetSerialized(riderRig, "_distanceInFrontOfHmd", camera != null ? DistanceInFrontOfCamera(camera, hitAnchor) : 1f);
            SetSerialized(riderRig, "_heightOffsetFromHmd", camera != null ? mantaRoot.position.y - camera.position.y : -InteractionRootBelowHit);

            EditorUtility.SetDirty(riderRig);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[AO] Rider aligned to camera interaction.\n" +
                $"HitAnchor world: {hitAnchor.position}\n" +
                $"MantaRoot world: {mantaRoot.position}\n" +
                $"CharacterRoot local: {characterRoot.localPosition}\n" +
                "Scene-authored MantaRoot/CharacterRoot transforms preserved.\n" +
                $"Follow HMD at runtime: false\n" +
                $"Drive HitAnchor To Rig: false\n" +
                $"HitAnchor local offset from rig: {hitLocal}");
        }

        private static float DistanceInFrontOfCamera(Transform camera, Transform hitAnchor)
        {
            Vector3 forward = camera.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return 1f;

            forward.Normalize();
            Vector3 cameraToHit = hitAnchor.position - camera.position;
            cameraToHit.y = 0f;
            return Mathf.Max(0.2f, Vector3.Dot(cameraToHit, forward));
        }

        private static Material EnsureClothesMaterial()
        {
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder("Assets/_Project/Art/Characters");
            EnsureFolder("Assets/_Project/Art/Characters/Materials");

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(ClothesAlbedoPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(ClothesNormalPath);
            Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(ClothesMetallicPath);

            if (albedo == null)
            {
                Debug.LogError($"[AO] Missing clothes albedo texture: {ClothesAlbedoPath}");
                return null;
            }

            ConfigureTextureImporter(ClothesAlbedoPath, TextureImporterType.Default, true);
            ConfigureTextureImporter(ClothesNormalPath, TextureImporterType.NormalMap, false);
            ConfigureTextureImporter(ClothesMetallicPath, TextureImporterType.Default, false);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[AO] Universal Render Pipeline/Lit shader was not found.");
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(ClothesMaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = Path.GetFileNameWithoutExtension(ClothesMaterialPath)
                };
                AssetDatabase.CreateAsset(material, ClothesMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_MainTex", albedo);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.28f);
            material.SetFloat("_Cull", 0f);
            material.doubleSidedGI = true;

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }

            if (metallic != null)
            {
                material.SetTexture("_MetallicGlossMap", metallic);
                material.SetFloat("_GlossMapScale", 0.35f);
            }

            // The source map carries transparency in the alpha channel, so keep cutout available for cloth planes.
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.25f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            material.EnableKeyword("_ALPHATEST_ON");

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureTextureImporter(string path, TextureImporterType textureType, bool srgb)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool changed = false;
            if (importer.textureType != textureType)
            {
                importer.textureType = textureType;
                changed = true;
            }

            if (importer.sRGBTexture != srgb)
            {
                importer.sRGBTexture = srgb;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (changed) importer.SaveAndReimport();
        }

        private static int ApplyClothesMaterialToPrefab(Material clothesMaterial)
        {
            if (!File.Exists(ToProjectFullPath(CharacterPrefabPath)))
            {
                Debug.LogWarning($"[AO] Character prefab missing: {CharacterPrefabPath}");
                return 0;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(CharacterPrefabPath);
            int assignments = ApplyClothesMaterialToHierarchy(root, clothesMaterial);
            PrefabUtility.SaveAsPrefabAsset(root, CharacterPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.ImportAsset(CharacterPrefabPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return assignments;
        }

        private static int ApplyClothesMaterialToHierarchy(GameObject root, Material clothesMaterial)
        {
            int assignments = 0;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (!IsClothesSlot(renderer, materials[i])) continue;

                    materials[i] = clothesMaterial;
                    assignments++;
                    changed = true;
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                    EditorUtility.SetDirty(renderer);
                }
            }

            if (assignments == 0)
            {
                Debug.LogWarning($"[AO] No clothes material slots were detected under {root.name}. Expected material/renderer names containing Clothes, Outfit, Skirt, Dress, or Korean plane mesh names.");
            }

            return assignments;
        }

        private static bool IsClothesSlot(Renderer renderer, Material material)
        {
            string rendererName = renderer != null ? renderer.name.ToLowerInvariant() : string.Empty;
            string materialName = material != null ? material.name.ToLowerInvariant() : string.Empty;

            if (materialName.Contains("clothes")) return true;
            if (materialName.Contains("outfit")) return true;
            if (materialName.Contains("dress")) return true;
            if (materialName.Contains("skirt")) return true;
            if (rendererName.Contains("clothes")) return true;
            if (rendererName.Contains("outfit")) return true;
            if (rendererName.Contains("dress")) return true;
            if (rendererName.Contains("skirt")) return true;
            if (renderer != null && renderer.name.Contains("평면")) return true;

            return false;
        }

        private static Transform FindSceneTransform(string objectName)
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || !go.scene.IsValid() || !go.scene.isLoaded) continue;
                if (go.name == objectName) return go.transform;
            }

            return null;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static string ToProjectFullPath(string assetPath)
        {
            string relative = assetPath.StartsWith("Assets/") ? assetPath.Substring("Assets/".Length) : assetPath;
            return Path.Combine(Application.dataPath, relative);
        }

        private static void SetSerialized(Object target, string propertyName, bool value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) property.boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerialized(Object target, string propertyName, float value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) property.floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerialized(Object target, string propertyName, Vector3 value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) property.vector3Value = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
