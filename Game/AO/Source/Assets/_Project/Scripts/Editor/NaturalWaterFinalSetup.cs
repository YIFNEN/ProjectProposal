using System;
using System.Collections.Generic;
using System.IO;
using AO.Character;
using AO.Notes;
using AO.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AO.Editor
{
    public static class NaturalWaterFinalSetup
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const string BubbleNotePrefabPath = "Assets/_Project/Prefabs/Notes/BubbleNote.prefab";
        private const string HudPrefabFolder = "Assets/_Project/Prefabs/HUD";
        private const string UiMaterialFolder = "Assets/_Project/Art/UI/Materials";

        private const string OxygenFillPath = "Assets/_Project/Art/UI/GamePlayScene/OxygenGauge_C02/Sprites/T_AO_UI_HUD_OxygenGauge_Fill_Alpha_1024x3072_C02.png";
        private const string OxygenFramePath = "Assets/_Project/Art/UI/GamePlayScene/OxygenGauge_C02/Sprites/T_AO_UI_HUD_OxygenGauge_Frame_Alpha_1024x3072_C02.png";
        private const string FeverOuterGlowPath = "Assets/_Project/Art/UI/GamePlayScene/FeverGauge_C01/Sprites/T_AO_UI_HUD_FeverGauge_OuterGlow_Mint_Alpha_1024x3072_C01.png";
        private const string FeverThinOutlinePath = "Assets/_Project/Art/UI/GamePlayScene/FeverGauge_C01/Sprites/T_AO_UI_HUD_FeverGauge_ThinOutline_White_Alpha_1024x3072_C01.png";
        private const string FeverLeadingSparkPath = "Assets/_Project/Art/UI/GamePlayScene/FeverGauge_C01/Sprites/T_AO_UI_HUD_FeverGauge_LeadingSpark_WhiteMint_Alpha_512_C01.png";

        private const string BubblePerfectAtlasPath = "Assets/_Project/Art/UI/Note/BubblePop_C02/Atlas/T_AO_VFX_BubblePop_Perfect_Atlas_4x4_4096_C02.png";
        private const string BubbleGoodAtlasPath = "Assets/_Project/Art/UI/Note/BubblePop_C02/Atlas/T_AO_VFX_BubblePop_Good_Atlas_4x4_4096_C02.png";
        private const string BubbleMissAtlasPath = "Assets/_Project/Art/UI/Note/BubblePop_C02/Atlas/T_AO_VFX_BubblePop_Miss_Atlas_4x4_4096_C02.png";
        private const string FeverActivationAtlasPath = "Assets/_Project/Art/UI/VFX/FeverActivation_C01/Atlas/T_AO_VFX_FeverActivationBurst_Atlas_4x4_4096_C01.png";
        private const string CriticalFramesFolder = "Assets/_Project/Art/UI/VFX/OxygenCriticalWarning_C03/Frames";

        private const string BubblePerfectPrefabPath = HudPrefabFolder + "/PF_AO_VFX_BubblePop_Perfect.prefab";
        private const string BubbleGoodPrefabPath = HudPrefabFolder + "/PF_AO_VFX_BubblePop_Good.prefab";
        private const string BubbleMissPrefabPath = HudPrefabFolder + "/PF_AO_VFX_BubblePop_Miss.prefab";
        private const string FeverActivationPrefabPath = HudPrefabFolder + "/PF_AO_VFX_FeverActivationBurst.prefab";

        [MenuItem("AO/Setup/Apply NaturalWater Final UI And VFX")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            ConfigureImports();

            GameObject perfectPrefab = CreateParticlePrefab(
                "PF_AO_VFX_BubblePop_Perfect",
                BubblePerfectPrefabPath,
                BubblePerfectAtlasPath,
                "M_AO_VFX_BubblePop_Perfect",
                Color.white,
                Color.white,
                0.36f,
                0.62f,
                additive: false);

            GameObject goodPrefab = CreateParticlePrefab(
                "PF_AO_VFX_BubblePop_Good",
                BubbleGoodPrefabPath,
                BubbleGoodAtlasPath,
                "M_AO_VFX_BubblePop_Good",
                Color.white,
                Color.white,
                0.34f,
                0.56f,
                additive: false);

            CreateParticlePrefab(
                "PF_AO_VFX_BubblePop_Miss",
                BubbleMissPrefabPath,
                BubbleMissAtlasPath,
                "M_AO_VFX_BubblePop_Miss",
                Color.white,
                Color.white,
                0.32f,
                0.58f,
                additive: false);

            GameObject feverActivationPrefab = CreateParticlePrefab(
                "PF_AO_VFX_FeverActivationBurst",
                FeverActivationPrefabPath,
                FeverActivationAtlasPath,
                "M_AO_VFX_FeverActivationBurst",
                Color.white,
                Color.white,
                0.48f,
                1.25f,
                additive: true);

            ConfigureBubbleNotePrefab(
                perfectPrefab,
                goodPrefab,
                feverActivationPrefab);
            ConfigureGameplayScene(
                perfectPrefab,
                goodPrefab,
                feverActivationPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AO] NaturalWater final Oxygen/Fever/Critical UI and particle prefabs applied.");
        }

        public static void ApplyFromCommandLine()
        {
            Apply();
        }

        private static void ConfigureImports()
        {
            string[] uiSprites =
            {
                OxygenFillPath,
                OxygenFramePath,
                FeverOuterGlowPath,
                FeverThinOutlinePath,
                FeverLeadingSparkPath,
            };

            foreach (string path in uiSprites)
            {
                ConfigureSpriteImport(path, 4096);
            }

            foreach (string path in CriticalFramePaths())
            {
                ConfigureSpriteImport(path, 2048);
            }

            ConfigureParticleAtlasImport(BubblePerfectAtlasPath);
            ConfigureParticleAtlasImport(BubbleGoodAtlasPath);
            ConfigureParticleAtlasImport(BubbleMissAtlasPath);
            ConfigureParticleAtlasImport(FeverActivationAtlasPath);
        }

        private static void ConfigureSpriteImport(string path, int maxSize)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[AO] Missing sprite texture: {path}");
                return;
            }

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }
            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }
            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                changed = true;
            }
            if (importer.filterMode != FilterMode.Bilinear)
            {
                importer.filterMode = FilterMode.Bilinear;
                changed = true;
            }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }
            if (importer.maxTextureSize < maxSize)
            {
                importer.maxTextureSize = maxSize;
                changed = true;
            }

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                changed = true;
            }

            changed |= ConfigurePlatform(importer, null, maxSize);
            changed |= ConfigurePlatform(importer, "Standalone", maxSize);
            changed |= ConfigurePlatform(importer, "Android", maxSize);

            if (changed) importer.SaveAndReimport();
        }

        private static void ConfigureParticleAtlasImport(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[AO] Missing particle atlas: {path}");
                return;
            }

            bool changed = false;
            if (importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                changed = true;
            }
            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }
            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                changed = true;
            }
            if (importer.filterMode != FilterMode.Bilinear)
            {
                importer.filterMode = FilterMode.Bilinear;
                changed = true;
            }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }
            if (importer.maxTextureSize < 4096)
            {
                importer.maxTextureSize = 4096;
                changed = true;
            }

            changed |= ConfigurePlatform(importer, null, 4096);
            changed |= ConfigurePlatform(importer, "Standalone", 4096);
            changed |= ConfigurePlatform(importer, "Android", 4096);

            if (changed) importer.SaveAndReimport();
        }

        private static bool ConfigurePlatform(TextureImporter importer, string platformName, int maxSize)
        {
            TextureImporterPlatformSettings settings = string.IsNullOrEmpty(platformName)
                ? importer.GetDefaultPlatformTextureSettings()
                : importer.GetPlatformTextureSettings(platformName);

            bool changed = false;
            if (!string.IsNullOrEmpty(platformName) && !settings.overridden)
            {
                settings.overridden = true;
                changed = true;
            }
            if (settings.maxTextureSize != maxSize)
            {
                settings.maxTextureSize = maxSize;
                changed = true;
            }
            if (settings.textureCompression != TextureImporterCompression.Uncompressed)
            {
                settings.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (!changed) return false;

            if (string.IsNullOrEmpty(platformName)) importer.SetPlatformTextureSettings(settings);
            else importer.SetPlatformTextureSettings(settings);
            return true;
        }

        private static GameObject CreateParticlePrefab(
            string prefabName,
            string prefabPath,
            string atlasPath,
            string materialName,
            Color particleColor,
            Color materialColor,
            float duration,
            float startSize,
            bool additive)
        {
            EnsureFolder(HudPrefabFolder);
            EnsureFolder(UiMaterialFolder);

            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            if (atlas == null)
            {
                throw new InvalidOperationException($"Missing particle atlas: {atlasPath}");
            }

            Material material = EnsureParticleMaterial($"{UiMaterialFolder}/{materialName}.mat", atlas, materialColor, additive);

            GameObject go = new GameObject(prefabName);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            ConfigureOneShotParticle(particles, duration, startSize, particleColor);

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 80;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        private static void ConfigureOneShotParticle(ParticleSystem particles, float duration, float startSize, Color color)
        {
            ParticleSystem.MainModule main = particles.main;
            main.duration = duration;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = duration;
            main.startSpeed = 0f;
            main.startSize = startSize;
            main.startColor = color;
            main.maxParticles = 1;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;

            ParticleSystem.TextureSheetAnimationModule sheet = particles.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 4;
            sheet.numTilesY = 4;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            sheet.cycleCount = 1;
        }

        private static Material EnsureParticleMaterial(string materialPath, Texture2D texture, Color color, bool additive)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Standard");
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(materialPath) };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.mainTexture = texture;
            SetTextureIfExists(material, "_BaseMap", texture);
            SetTextureIfExists(material, "_MainTex", texture);
            SetColorIfExists(material, "_BaseColor", color);
            SetColorIfExists(material, "_Color", color);
            ConfigureTransparentMaterial(material, additive);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureTransparentMaterial(Material material, bool additive)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            SetFloatIfExists(material, "_Surface", 1f);
            SetFloatIfExists(material, "_Blend", additive ? 2f : 0f);
            SetFloatIfExists(material, "_AlphaClip", 0f);
            SetFloatIfExists(material, "_ZWrite", 0f);
            SetFloatIfExists(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloatIfExists(material, "_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void ConfigureBubbleNotePrefab(GameObject judgementPerfectPrefab, GameObject judgementGoodPrefab, GameObject feverHitPrefab)
        {
            GameObject bubblePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BubbleNotePrefabPath);
            if (bubblePrefab == null)
            {
                Debug.LogWarning($"[AO] BubbleNote prefab not found: {BubbleNotePrefabPath}");
                return;
            }

            BubbleNote note = bubblePrefab.GetComponent<BubbleNote>();
            if (note == null)
            {
                Debug.LogWarning("[AO] BubbleNote component not found on BubbleNote prefab.");
                return;
            }

            SetObjectReference(note, "_perfectVfxPrefab", judgementPerfectPrefab);
            SetObjectReference(note, "_goodVfxPrefab", judgementGoodPrefab);
            SetObjectReference(note, "_feverVfxPrefab", feverHitPrefab);
            SetObjectReference(note, "_hitVfxPrefab", null);
            SetObjectReference(note, "_missVfxPrefab", null);
            EditorUtility.SetDirty(note);
            EditorUtility.SetDirty(bubblePrefab);
            PrefabUtility.SavePrefabAsset(bubblePrefab);
        }

        private static void ConfigureGameplayScene(
            GameObject perfectPrefab,
            GameObject goodPrefab,
            GameObject feverHitPrefab)
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            GameObject hud = FindSceneGameObject("HUDCanvas");
            if (hud == null) throw new InvalidOperationException("HUDCanvas was not found in GamePlayScene.");

            ConfigureOxygenAndFeverGauge(hud);
            ConfigureOxygenCriticalWarning(hud);
            DisableFeverActivationSpawner();
            DisableLegacyFeverHandEffect();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            VerifySceneReferences(perfectPrefab, goodPrefab, feverHitPrefab);
        }

        private static void ConfigureOxygenAndFeverGauge(GameObject hud)
        {
            Sprite oxygenFill = LoadRequired<Sprite>(OxygenFillPath);
            Sprite oxygenFrame = LoadRequired<Sprite>(OxygenFramePath);
            Sprite feverGlowSprite = LoadRequired<Sprite>(FeverOuterGlowPath);
            Sprite feverFillSprite = LoadRequired<Sprite>(FeverThinOutlinePath);
            Sprite leadingSparkSprite = LoadRequired<Sprite>(FeverLeadingSparkPath);

            RectTransform hudRect = hud.transform as RectTransform;
            RectTransform oxygen = EnsureRectChild(hudRect, "OxygenBar");
            SetRectIfInvalid(oxygen, new Vector2(-390f, -18f), new Vector2(80f, 240f));
            ApplyTransparentImage(oxygen.gameObject);

            RectTransform fillMask = EnsureRectChild(oxygen, "FillMask");
            Stretch(fillMask, new Vector2(4f, 10f), new Vector2(-4f, -10f));
            RectMask2D mask = fillMask.GetComponent<RectMask2D>();
            if (mask == null) mask = fillMask.gameObject.AddComponent<RectMask2D>();

            RectTransform water = EnsureImageChild(fillMask, "WaterBody_Image", oxygenFill, Color.white);
            Stretch(water, Vector2.zero, Vector2.zero);
            Image waterImage = water.GetComponent<Image>();
            waterImage.type = Image.Type.Filled;
            waterImage.fillMethod = Image.FillMethod.Vertical;
            waterImage.fillOrigin = (int)Image.OriginVertical.Bottom;
            waterImage.fillAmount = 1f;
            waterImage.preserveAspect = false;
            waterImage.raycastTarget = false;

            SetChildActive(oxygen, "Fill", false);
            SetChildActive(fillMask, "WaveHighlight_Image", false);
            SetChildActive(fillMask, "BubbleParticleSystem", false);

            OxygenGaugeVisual visual = oxygen.GetComponent<OxygenGaugeVisual>();
            if (visual != null)
            {
                visual.enabled = false;
                EditorUtility.SetDirty(visual);
            }

            OxygenBar bar = oxygen.GetComponent<OxygenBar>();
            if (bar == null) bar = oxygen.gameObject.AddComponent<OxygenBar>();
            SetObjectReference(bar, "_fillImage", waterImage);
            SetBool(bar, "_resizeFillRectToRatio", false);

            RectTransform feverGlow = EnsureImageChild(oxygen, "FeverFrameGlow_Image", feverGlowSprite, new Color(1f, 1f, 1f, 0.2f));
            ConfigureFeverOutlineImage(feverGlow, 1.45f);

            RectTransform feverFill = EnsureImageChild(oxygen, "FeverFrameFill_Image", feverFillSprite, new Color(1f, 1f, 1f, 0.55f));
            ConfigureFeverOutlineImage(feverFill, 1.28f);

            RectTransform frame = EnsureImageChild(oxygen, "Frame_Image", oxygenFrame, Color.white);
            Stretch(frame, Vector2.zero, Vector2.zero);
            frame.localScale = Vector3.one;
            Image frameImage = frame.GetComponent<Image>();
            frameImage.type = Image.Type.Simple;
            frameImage.preserveAspect = false;
            frameImage.raycastTarget = false;

            RectTransform spark = EnsureImageChild(oxygen, "FeverLeadingSpark_Image", leadingSparkSprite, Color.white);
            spark.anchorMin = new Vector2(0.5f, 0.5f);
            spark.anchorMax = new Vector2(0.5f, 0.5f);
            spark.pivot = new Vector2(0.5f, 0.5f);
            spark.sizeDelta = new Vector2(34f, 34f);
            spark.anchoredPosition = new Vector2(0f, 150f);
            Image sparkImage = spark.GetComponent<Image>();
            sparkImage.type = Image.Type.Simple;
            sparkImage.preserveAspect = true;
            sparkImage.raycastTarget = false;
            spark.gameObject.SetActive(false);

            OrderOxygenChildren(oxygen);

            RectTransform feverRoot = EnsureRectChild(hudRect, "FeverGauge");
            CopyRect(oxygen, feverRoot);
            ApplyTransparentImage(feverRoot.gameObject);
            SetChildActive(feverRoot, "Fill", false);
            SetChildActive(feverRoot, "Marker", false);
            SetChildActive(feverRoot, "Label", false);

            FeverGauge gauge = feverRoot.GetComponent<FeverGauge>();
            if (gauge == null) gauge = feverRoot.gameObject.AddComponent<FeverGauge>();
            SetObjectReference(gauge, "_fillImage", feverFill.GetComponent<Image>());
            SetObjectReference(gauge, "_glowImage", feverGlow.GetComponent<Image>());
            SetObjectReference(gauge, "_marker", null);
            SetObjectReference(gauge, "_label", null);
            SetObjectReference(gauge, "_leadingSpark", spark);
            SetBool(gauge, "_useRadialOutline", true);
            SetBool(gauge, "_transparentOverlay", false);
            SetBool(gauge, "_showLabel", false);
            SetBool(gauge, "_showLeadingSpark", true);
            SetFloat(gauge, "_leadingSparkRadiusPadding", 5f);
            EditorUtility.SetDirty(gauge);
        }

        private static void ConfigureFeverOutlineImage(RectTransform rect, float scale)
        {
            Stretch(rect, Vector2.zero, Vector2.zero);
            rect.localScale = Vector3.one * scale;

            Image image = rect.GetComponent<Image>();
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.fillAmount = 0f;
            image.preserveAspect = false;
            image.raycastTarget = false;
        }

        private static void ConfigureOxygenCriticalWarning(GameObject hud)
        {
            Sprite[] frames = LoadCriticalFrames();
            if (frames.Length == 0)
            {
                throw new InvalidOperationException("OxygenCriticalWarning C03 frames were not found.");
            }

            RectTransform hudRect = hud.transform as RectTransform;
            OxygenCriticalEffect effect = hud.GetComponent<OxygenCriticalEffect>();
            if (effect == null) effect = hud.AddComponent<OxygenCriticalEffect>();

            RectTransform pulse = EnsureImageChild(hud.transform, "OxygenCriticalPulse", frames[0], Color.white);
            Stretch(pulse, Vector2.zero, Vector2.zero);
            pulse.SetAsLastSibling();
            pulse.gameObject.SetActive(false);

            Image pulseImage = pulse.GetComponent<Image>();
            pulseImage.sprite = frames[0];
            pulseImage.type = Image.Type.Simple;
            pulseImage.preserveAspect = false;
            pulseImage.raycastTarget = false;
            pulseImage.color = new Color(1f, 1f, 1f, 0f);

            SetObjectReference(effect, "_targetRoot", hudRect);
            SetObjectReference(effect, "_pulseImage", pulseImage);
            SetSpriteArray(effect, "_pulseFrames", frames);
            SetFloat(effect, "_frameRate", 18f);
            SetFloat(effect, "_pulseHz", 1.55f);
            SetFloat(effect, "_maxAlpha", 0.72f);
            SetFloat(effect, "_minAlpha", 0.16f);
            SetBool(effect, "_forceFullTargetRect", true);
            SetFloat(effect, "_viewWidthOverscan", 1.6f);
            SetFloat(effect, "_viewHeightOverscan", 2.25f);
            SetBool(effect, "_forceStretchedImage", true);
            SetBool(effect, "_useSpriteNativeColor", true);
            EditorUtility.SetDirty(effect);
        }

        private static void DisableFeverActivationSpawner()
        {
            GameObject root = FindSceneGameObject("NaturalWaterVfxController");
            if (root == null) return;

            FeverActivationVfx vfx = root.GetComponent<FeverActivationVfx>();
            if (vfx == null) return;

            SetObjectReference(vfx, "_activationPrefab", null);
            vfx.enabled = false;
            EditorUtility.SetDirty(vfx);
        }

        private static void DisableLegacyFeverHandEffect()
        {
            foreach (FeverHandEffect effect in UnityEngine.Object.FindObjectsByType<FeverHandEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (effect == null || !effect.gameObject.scene.isLoaded) continue;
                if (effect.GetComponent<DVariantRiderRig>() != null || effect.gameObject.name == "MantaRoot") continue;
                if (!effect.enabled) continue;

                effect.enabled = false;
                EditorUtility.SetDirty(effect);
            }
        }

        private static void VerifySceneReferences(
            GameObject perfectPrefab,
            GameObject goodPrefab,
            GameObject feverHitPrefab)
        {
            GameObject hud = FindSceneGameObject("HUDCanvas");
            GameObject oxygen = hud != null ? FindDirect(hud.transform, "OxygenBar") : null;
            GameObject fever = hud != null ? FindDirect(hud.transform, "FeverGauge") : null;

            List<string> missing = new List<string>();
            if (perfectPrefab == null) missing.Add("BubblePop Perfect prefab");
            if (goodPrefab == null) missing.Add("BubblePop Good prefab");
            if (feverHitPrefab == null) missing.Add("BubblePop Fever hit prefab");
            if (oxygen == null) missing.Add("HUDCanvas/OxygenBar");
            if (fever == null) missing.Add("HUDCanvas/FeverGauge");
            if (hud == null || hud.GetComponent<OxygenCriticalEffect>() == null) missing.Add("HUDCanvas/OxygenCriticalEffect");

            if (missing.Count > 0)
            {
                throw new InvalidOperationException("[AO] NaturalWater final verification failed: " + string.Join(", ", missing));
            }

            Debug.Log("[AO] NaturalWater final verification passed: Oxygen/Fever/Critical UI and Bubble/Fever-hit prefabs are present.");
        }

        private static Sprite[] LoadCriticalFrames()
        {
            List<Sprite> frames = new List<Sprite>();
            foreach (string path in CriticalFramePaths())
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) frames.Add(sprite);
            }
            return frames.ToArray();
        }

        private static string[] CriticalFramePaths()
        {
            List<string> paths = new List<string>();
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { CriticalFramesFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) paths.Add(path);
            }
            paths.Sort(StringComparer.Ordinal);
            return paths.ToArray();
        }

        private static RectTransform EnsureRectChild(Transform parent, string name)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            if (existing == null && parent != null) go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static RectTransform EnsureImageChild(Transform parent, string name, Sprite sprite, Color color)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            if (existing == null && parent != null) go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            Image image = go.GetComponent<Image>();
            if (image == null) image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static void ApplyTransparentImage(GameObject go)
        {
            Image image = go.GetComponent<Image>();
            if (image == null) image = go.AddComponent<Image>();
            image.sprite = null;
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localRotation = Quaternion.identity;
        }

        private static void SetRectIfInvalid(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            float expectedAspect = size.x / Mathf.Max(0.001f, size.y);
            float currentAspect = rect.sizeDelta.x / Mathf.Max(0.001f, rect.sizeDelta.y);
            bool hasUsableSize = rect.sizeDelta.sqrMagnitude > 0.01f;
            bool hasExpectedAspect = Mathf.Abs(currentAspect - expectedAspect) < 0.02f;
            if (hasUsableSize && hasExpectedAspect) return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private static void CopyRect(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        private static void SetChildActive(Transform parent, string name, bool active)
        {
            Transform child = parent != null ? parent.Find(name) : null;
            if (child != null) child.gameObject.SetActive(active);
        }

        private static void OrderOxygenChildren(RectTransform oxygen)
        {
            string[] order =
            {
                "FillMask",
                "FeverFrameGlow_Image",
                "FeverFrameFill_Image",
                "Frame_Image",
                "FeverLeadingSpark_Image",
            };

            int index = 0;
            foreach (string name in order)
            {
                Transform child = oxygen.Find(name);
                if (child != null) child.SetSiblingIndex(index++);
            }
        }

        private static GameObject FindSceneGameObject(string name)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.scene == activeScene && go.name == name) return go;
            }
            return null;
        }

        private static GameObject FindDirect(Transform parent, string name)
        {
            Transform child = parent != null ? parent.Find(name) : null;
            return child != null ? child.gameObject : null;
        }

        private static T LoadRequired<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException($"Missing required asset: {path}");
            return asset;
        }

        private static void SetObjectReference(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) return;
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetSpriteArray(UnityEngine.Object target, string fieldName, Sprite[] sprites)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) return;
            prop.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetBool(UnityEngine.Object target, string fieldName, bool value)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) return;
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetFloat(UnityEngine.Object target, string fieldName, float value)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) return;
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetVector3(UnityEngine.Object target, string fieldName, Vector3 value)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) return;
            prop.vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetTextureIfExists(Material material, string property, Texture texture)
        {
            if (material.HasProperty(property)) material.SetTexture(property, texture);
        }

        private static void SetColorIfExists(Material material, string property, Color color)
        {
            if (material.HasProperty(property)) material.SetColor(property, color);
        }

        private static void SetFloatIfExists(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
