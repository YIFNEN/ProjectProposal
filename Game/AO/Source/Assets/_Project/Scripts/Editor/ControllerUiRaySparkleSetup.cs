using System.Collections.Generic;
using System.IO;
using AO.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class ControllerUiRaySparkleSetup
    {
        private static readonly string[] UiScenes =
        {
            "Assets/_Project/Scenes/01_Title.unity",
            "Assets/_Project/Scenes/02_Lobby.unity",
            "Assets/_Project/Scenes/GamePlayScene.unity",
            "Assets/_Project/Scenes/04_Result.unity"
        };

        private const string LineTexturePath = "Assets/_Project/Art/UI/Textures/T_AO_UI_ControllerRayLine_SparkleStick_Transparent_1024x128.png";
        private const string SparkTexturePath = "Assets/_Project/Art/UI/Textures/T_AO_VFX_SoftStarSpark_Transparent_256.png";
        private const string LineMaterialPath = "Assets/_Project/Art/UI/Materials/M_AO_UI_ControllerRayLine_SparkleStick.mat";
        private const string SparkMaterialPath = "Assets/_Project/Art/UI/Materials/M_AO_VFX_SoftStarSpark.mat";
        private const string SparkleBurstPrefabPath = "Assets/_Project/Prefabs/HUD/PF_AO_VFX_SoftStarSparkleBurst.prefab";

        [MenuItem("AO/Setup/Apply Controller UI Ray Sparkle")]
        public static void Apply()
        {
            ConfigureTextureImport(LineTexturePath, 2048);
            ConfigureTextureImport(SparkTexturePath, 512);

            Texture2D lineTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(LineTexturePath);
            Texture2D sparkTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(SparkTexturePath);
            if (lineTexture == null || sparkTexture == null)
            {
                Debug.LogError("[AO] Controller UI ray sparkle textures are missing. Copy the final PNGs into Assets/_Project/Art/UI/Textures first.");
                return;
            }

            Material lineMaterial = EnsureMaterial(LineMaterialPath, lineTexture, false, new Color(0.78f, 1f, 0.95f, 1f));
            Material sparkMaterial = EnsureMaterial(SparkMaterialPath, sparkTexture, true, new Color(0.9f, 1f, 0.96f, 1f));
            ApplyToScenes(lineMaterial, sparkMaterial);
            EnsureReusableSparkleBurstPrefab(sparkMaterial);
            ControllerUiRaySparklePrefabize.Apply();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AO] Controller UI ray sparkle applied: stick material and separate sparkle particles are connected.");
        }

        public static void ApplyFromCommandLine()
        {
            Apply();
        }

        private static void ConfigureTextureImport(string path, int maxSize)
        {
            if (!File.Exists(path)) return;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = maxSize;
            importer.SaveAndReimport();
        }

        private static Material EnsureMaterial(string path, Texture texture, bool additive, Color tint)
        {
            EnsureFolderForAsset(path);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(FindRayShader())
                {
                    name = Path.GetFileNameWithoutExtension(path)
                };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = FindRayShader();
            }

            ConfigureMaterial(material, texture, additive, tint);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Shader FindRayShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            return shader;
        }

        private static void ConfigureMaterial(Material material, Texture texture, bool additive, Color tint)
        {
            if (material == null) return;

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", additive ? 1f : 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_SrcBlendAlpha")) material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (material.HasProperty("_DstBlendAlpha")) material.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);

            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_NORMALMAP");
        }

        private static void ApplyToScenes(Material lineMaterial, Material sparkMaterial)
        {
            foreach (string scenePath in UiScenes)
            {
                if (!File.Exists(scenePath)) continue;

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                foreach (ControllerUiRayPointer pointer in Object.FindObjectsByType<ControllerUiRayPointer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (pointer == null || !pointer.gameObject.scene.isLoaded) continue;

                    SerializedObject so = new SerializedObject(pointer);
                    SetObject(so, "_lineMaterial", lineMaterial);
                    SetBool(so, "_showRaySparkles", true);
                    SetBool(so, "_configureRaySparklesAtRuntime", false);
                    SetObject(so, "_sparkleMaterial", sparkMaterial);
                    SetFloat(so, "_sparkleRate", 11f);
                    SetFloat(so, "_sparkleHitRateMultiplier", 1.7f);
                    SetFloat(so, "_sparkleBaseSize", 0.026f);
                    SetFloat(so, "_sparkleHitSizeMultiplier", 1.25f);
                    SetFloat(so, "_sparkleCrossSection", 0.055f);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(pointer);

                    ApplyToLine(pointer.transform.Find("LeftUiRayLine"), lineMaterial);
                    ApplyToLine(pointer.transform.Find("RightUiRayLine"), lineMaterial);
                    EnsureSparkleChild(pointer.transform, "LeftUiRaySparkles", sparkMaterial);
                    EnsureSparkleChild(pointer.transform, "RightUiRaySparkles", sparkMaterial);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        private static void ApplyToLine(Transform lineTransform, Material material)
        {
            if (lineTransform == null) return;

            LineRenderer line = lineTransform.GetComponent<LineRenderer>();
            if (line == null) return;

            line.sharedMaterial = material;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = Mathf.Max(line.numCapVertices, 8);
            line.numCornerVertices = Mathf.Max(line.numCornerVertices, 2);
            EditorUtility.SetDirty(line);
        }

        private static void EnsureSparkleChild(Transform parent, string name, Material material)
        {
            if (parent == null) return;

            Transform child = parent.Find(name);
            if (child == null)
            {
                GameObject go = new GameObject(name);
                go.transform.SetParent(parent, false);
                child = go.transform;
            }

            ParticleSystem sparkles = child.GetComponent<ParticleSystem>();
            if (sparkles == null) sparkles = child.gameObject.AddComponent<ParticleSystem>();

            ConfigureSparkles(sparkles, material);
            EditorUtility.SetDirty(child.gameObject);
        }

        private static void ConfigureSparkles(ParticleSystem sparkles, Material material)
        {
            ParticleSystem.MainModule main = sparkles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 96;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.58f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.015f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.017f, 0.035f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 1f, 0.92f, 0.65f), Color.white);

            ParticleSystem.EmissionModule emission = sparkles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = sparkles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = Vector3.one;
            shape.randomDirectionAmount = 0.16f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = sparkles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.74f, 1f, 0.94f, 1f), 0f),
                    new GradientColorKey(Color.white, 0.4f),
                    new GradientColorKey(new Color(0.58f, 1f, 0.92f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.16f),
                    new GradientAlphaKey(0.78f, 0.52f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = sparkles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.32f, 1f),
                new Keyframe(1f, 0.12f)));

            ParticleSystemRenderer renderer = sparkles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = material;

            sparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void EnsureReusableSparkleBurstPrefab(Material material)
        {
            EnsureFolderForAsset(SparkleBurstPrefabPath);

            GameObject root = new GameObject("PF_AO_VFX_SoftStarSparkleBurst");
            ParticleSystem sparkles = root.AddComponent<ParticleSystem>();
            ConfigureBurstSparkles(sparkles, material);
            PrefabUtility.SaveAsPrefabAsset(root, SparkleBurstPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void ConfigureBurstSparkles(ParticleSystem sparkles, Material material)
        {
            ParticleSystem.MainModule main = sparkles.main;
            main.loop = false;
            main.playOnAwake = true;
            main.duration = 0.75f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.78f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.045f, 0.18f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.032f, 0.085f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.72f, 1f, 0.92f, 0.86f), Color.white);

            ParticleSystem.EmissionModule emission = sparkles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 20, 28),
                new ParticleSystem.Burst(0.14f, 8, 14)
            });

            ParticleSystem.ShapeModule shape = sparkles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.075f;
            shape.randomDirectionAmount = 0.35f;

            ParticleSystem.VelocityOverLifetimeModule velocity = sparkles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = sparkles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.72f, 1f, 0.92f, 1f), 0f),
                    new GradientColorKey(Color.white, 0.22f),
                    new GradientColorKey(new Color(0.58f, 1f, 0.92f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.82f, 0.48f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = sparkles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.2f),
                new Keyframe(0.2f, 1f),
                new Keyframe(1f, 0.08f)));

            ParticleSystemRenderer renderer = sparkles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = material;
        }

        private static void SetObject(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = value;
        }

        private static void SetBool(SerializedObject so, string propertyName, bool value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null) property.boolValue = value;
        }

        private static void SetFloat(SerializedObject so, string propertyName, float value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null) property.floatValue = value;
        }

        private static void EnsureFolderForAsset(string assetPath)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(assetPath));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        }
    }

    public static class ControllerUiRaySparklePrefabize
    {
        private static readonly string[] UiScenes =
        {
            "Assets/_Project/Scenes/01_Title.unity",
            "Assets/_Project/Scenes/02_Lobby.unity",
            "Assets/_Project/Scenes/GamePlayScene.unity",
            "Assets/_Project/Scenes/04_Result.unity"
        };

        private const string SourcePrefabPath = "Assets/_Project/Art/UI/VFX/LeftUiRaySparkles.prefab";
        private const string RaySparklesPrefabPath = "Assets/_Project/Art/UI/VFX/PF_AO_UI_RaySparkles.prefab";
        private const string SparkMaterialPath = "Assets/_Project/Art/UI/Materials/M_AO_VFX_RaySpark_CoreGlow.mat";

        [MenuItem("AO/Setup/Prefabize Controller UI Ray Sparkles")]
        public static void Apply()
        {
            GameObject raySparklesPrefab = EnsureRaySparklesPrefab();
            if (raySparklesPrefab == null)
            {
                Debug.LogError("[AO] Could not create or load PF_AO_UI_RaySparkles.prefab.");
                return;
            }

            foreach (string scenePath in UiScenes)
            {
                if (!File.Exists(scenePath))
                {
                    Debug.LogWarning($"[AO] Scene not found: {scenePath}");
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                ControllerUiRayPointer[] pointers = Object.FindObjectsByType<ControllerUiRayPointer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (ControllerUiRayPointer pointer in pointers)
                {
                    if (pointer == null || pointer.gameObject.scene != scene) continue;

                    SerializedObject so = new SerializedObject(pointer);
                    SetBool(so, "_showRaySparkles", true);
                    SetBool(so, "_configureRaySparklesAtRuntime", false);
                    SetObject(so, "_sparkleMaterial", AssetDatabase.LoadAssetAtPath<Material>(SparkMaterialPath));
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(pointer);

                    ReplaceSparklesChild(pointer.transform, "LeftUiRaySparkles", raySparklesPrefab);
                    ReplaceSparklesChild(pointer.transform, "RightUiRaySparkles", raySparklesPrefab);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[AO] Prefabized controller UI ray sparkles in {scenePath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AO] Controller UI ray sparkles now use {RaySparklesPrefabPath}");
        }

        public static void ApplyFromCommandLine()
        {
            Apply();
        }

        private static GameObject EnsureRaySparklesPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RaySparklesPrefabPath);
            if (prefab != null) return prefab;

            EnsureFolderForAsset(RaySparklesPrefabPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath) != null)
            {
                if (!AssetDatabase.CopyAsset(SourcePrefabPath, RaySparklesPrefabPath))
                {
                    Debug.LogError($"[AO] Failed to copy {SourcePrefabPath} to {RaySparklesPrefabPath}");
                    return null;
                }

                GameObject contents = PrefabUtility.LoadPrefabContents(RaySparklesPrefabPath);
                contents.name = "PF_AO_UI_RaySparkles";
                PrefabUtility.SaveAsPrefabAsset(contents, RaySparklesPrefabPath);
                PrefabUtility.UnloadPrefabContents(contents);
                return AssetDatabase.LoadAssetAtPath<GameObject>(RaySparklesPrefabPath);
            }

            GameObject root = new GameObject("PF_AO_UI_RaySparkles");
            ParticleSystem sparkles = root.AddComponent<ParticleSystem>();
            ConfigureFallbackSparkles(sparkles);
            PrefabUtility.SaveAsPrefabAsset(root, RaySparklesPrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(RaySparklesPrefabPath);
        }

        private static void ReplaceSparklesChild(Transform parent, string childName, GameObject prefab)
        {
            if (parent == null || prefab == null) return;

            Transform existing = parent.Find(childName);
            bool active = existing == null || existing.gameObject.activeSelf;
            int siblingIndex = existing != null ? existing.GetSiblingIndex() : parent.childCount;
            Vector3 localPosition = existing != null ? existing.localPosition : Vector3.zero;
            Quaternion localRotation = existing != null ? existing.localRotation : Quaternion.identity;
            Vector3 localScale = existing != null ? existing.localScale : Vector3.one;

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                Debug.LogError($"[AO] Failed to instantiate {RaySparklesPrefabPath} under {parent.name}.");
                return;
            }

            instance.name = childName;
            Transform instanceTransform = instance.transform;
            instanceTransform.localPosition = localPosition;
            instanceTransform.localRotation = localRotation;
            instanceTransform.localScale = localScale;
            instance.SetActive(active);

            if (existing != null)
            {
                List<Transform> oldChildren = new List<Transform>();
                for (int i = 0; i < existing.childCount; i++)
                {
                    oldChildren.Add(existing.GetChild(i));
                }

                foreach (Transform oldChild in oldChildren)
                {
                    oldChild.SetParent(instanceTransform, true);
                }

                Object.DestroyImmediate(existing.gameObject);
            }

            instanceTransform.SetSiblingIndex(Mathf.Min(siblingIndex, parent.childCount - 1));
            EditorUtility.SetDirty(instance);
        }

        private static void ConfigureFallbackSparkles(ParticleSystem sparkles)
        {
            ParticleSystem.MainModule main = sparkles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 96;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.58f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.015f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.017f, 0.035f);
            main.startColor = Color.white;

            ParticleSystem.EmissionModule emission = sparkles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = sparkles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = Vector3.one;
            shape.randomDirectionAmount = 0.16f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = sparkles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.16f),
                    new GradientAlphaKey(0.78f, 0.52f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = sparkles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.32f, 1f),
                new Keyframe(1f, 0.12f)));

            ParticleSystemRenderer renderer = sparkles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SparkMaterialPath);
        }

        private static void SetObject(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = value;
        }

        private static void SetBool(SerializedObject so, string propertyName, bool value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null) property.boolValue = value;
        }

        private static void EnsureFolderForAsset(string assetPath)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(assetPath));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        }
    }
}
