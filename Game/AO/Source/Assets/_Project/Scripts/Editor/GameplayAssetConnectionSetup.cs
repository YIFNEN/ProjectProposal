using System.IO;
using AO.Audio;
using AO.Character;
using AO.Core;
using AO.Notes;
using AO.Rhythm;
using AO.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class GameplayAssetConnectionSetup
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const string SongFolder = "Assets/_Project/Settings/Songs";
        private const string TwinklePreviewPath = "Assets/_Project/Audio/BGM/Previews/Twinkle_Preview.wav";
        private const string BubblePrefabPath = "Assets/_Project/Prefabs/Notes/BubbleNote.prefab";
        private const string FishWrapperPrefabPath = "Assets/_Project/Prefabs/Notes/FishNote_Wrapper.prefab";
        private const string JudgementPerfectFxPath = "Assets/_Project/Prefabs/HUD/PF_AO_VFX_BubblePop_Perfect.prefab";
        private const string JudgementGoodFxPath = "Assets/_Project/Prefabs/HUD/PF_AO_VFX_BubblePop_Good.prefab";
        private const string FeverHitFxPath = "Assets/_Project/Prefabs/HUD/PF_AO_VFX_FeverActivationBurst.prefab";
        private const string StrokeFxPath = "Assets/_Project/Prefabs/HUD/FX_Stroke_Heart.prefab";
        private const string LaneMaterialPath = "Assets/_Project/Art/VFX/LanePathGuide_Mat.mat";
        private const string AmbientPath = "Assets/_Project/Audio/Ambient/Amb_Underwater.wav";
        private const float HandHorizontalScale = 1f;
        private const float HandVerticalScale = 1.15f;
        private const float HandDepthScale = 1.05f;
        private static readonly Vector3 DebugHandWorkspacePositiveLocal = new Vector3(0.78f, 0.85f, 0.82f);
        private static readonly Vector3 DebugHandWorkspaceNegativeLocal = new Vector3(0.78f, 0.62f, 0.70f);

        public static void Apply()
        {
            AssetDatabase.Refresh();
            ConfigureSongAssets();
            ConfigureNotePrefabs();
            ConfigureGameplayScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AO] Connected gameplay audio, note VFX, thumbnails, oxygen effect, lane guide cleanup, and disabled fever hand hearts.");
        }

        public static void ApplyFromCommandLine()
        {
            Apply();
        }

        private static void ConfigureSongAssets()
        {
            SetSongMedia("Song_Twinkle", "Assets/_Project/Art/UI/SongThumb_Twinkle.png", TwinklePreviewPath, "Assets/_Project/Audio/BGM/Twinkle_Original.wav", "Assets/_Project/Beatmaps/Twinkle_Normal.json");
            SetSongMedia("Song_Twinklestar", "Assets/_Project/Art/UI/SongThumb_Twinklestar.png", "Assets/_Project/Audio/BGM/Previews/Twinklestar_Preview.wav", "Assets/_Project/Audio/BGM/Twinklestar.wav", "Assets/_Project/Beatmaps/Twinklestar_Normal.json");
            SetSongMedia("Song_Utakata", "Assets/_Project/Art/UI/SongThumb_Utakata.png", "Assets/_Project/Audio/BGM/Previews/Utakata_Preview.wav", "Assets/_Project/Audio/BGM/Utakata.wav", "Assets/_Project/Beatmaps/Utakata_Normal.json");
            SetSongMedia("Song_ShinkaiShoujo", "Assets/_Project/Art/UI/SongThumb_ShinkaiShoujo.png", "Assets/_Project/Audio/BGM/Previews/ShinkaiShoujo_Preview.wav", "Assets/_Project/Audio/BGM/ShinkaiShoujo.wav", "Assets/_Project/Beatmaps/ShinkaiShoujo_Normal.json");
        }

        private static void SetSongMedia(string assetName, string thumbnailPath, string previewPath, string bgmPath, string beatmapPath)
        {
            SongDefinition song = AssetDatabase.LoadAssetAtPath<SongDefinition>($"{SongFolder}/{assetName}.asset");
            if (song == null) return;

            Sprite thumbnail = LoadSprite(thumbnailPath);
            AudioClip preview = LoadAssetIfExists<AudioClip>(previewPath);
            AudioClip bgm = LoadAssetIfExists<AudioClip>(bgmPath);
            TextAsset beatmap = LoadAssetIfExists<TextAsset>(beatmapPath);
            if (thumbnail != null) song.Thumbnail = thumbnail;
            if (preview != null) song.PreviewClip = preview;
            if (bgm != null) song.BgmClip = bgm;
            if (beatmap != null) song.NormalBeatmap = beatmap;
            EditorUtility.SetDirty(song);
        }

        private static void ConfigureNotePrefabs()
        {
            GameObject perfectFx = LoadAssetIfExists<GameObject>(JudgementPerfectFxPath);
            GameObject goodFx = LoadAssetIfExists<GameObject>(JudgementGoodFxPath);
            GameObject feverHitFx = LoadAssetIfExists<GameObject>(FeverHitFxPath);
            GameObject strokeFx = LoadAssetIfExists<GameObject>(StrokeFxPath);

            GameObject bubblePrefab = LoadAssetIfExists<GameObject>(BubblePrefabPath);
            if (bubblePrefab != null)
            {
                BubbleNote bubble = bubblePrefab.GetComponent<BubbleNote>();
                SetObjectReference(bubble, "_perfectVfxPrefab", perfectFx);
                SetObjectReference(bubble, "_goodVfxPrefab", goodFx);
                SetObjectReference(bubble, "_feverVfxPrefab", feverHitFx);
                SetObjectReference(bubble, "_hitVfxPrefab", null);
                SetObjectReference(bubble, "_missVfxPrefab", null);
                EditorUtility.SetDirty(bubblePrefab);
                PrefabUtility.SavePrefabAsset(bubblePrefab);
            }

            GameObject fishPrefab = LoadAssetIfExists<GameObject>(FishWrapperPrefabPath);
            if (fishPrefab != null)
            {
                FishNote fish = fishPrefab.GetComponent<FishNote>();
                SetObjectReference(fish, "_successVfxPrefab", strokeFx);
                SetObjectReference(fish, "_approachHeartPrefab", strokeFx);
                SetBool(fish, "_successVfxFollowHeadAnchor", true);
                SetFloat(fish, "_successVfxWorldUpOffset", 0.24f);
                SetFloat(fish, "_successVfxLifetime", 1.8f);
                SetBool(fish, "_emitApproachHearts", true);
                SetFloat(fish, "_approachHeartInterval", 0.32f);
                SetBool(fish, "_heartProgressFollowHeadAnchor", true);
                SetFloat(fish, "_heartProgressWorldUpOffset", 0.32f);
                SetBool(fish, "_playVisualPrefabAnimations", true);
                SetBool(fish, "_scaleStrokeOverlapWithVisualScale", true);
                SetFloat(fish, "_minimumStrokeOverlapScale", 1f);
                EditorUtility.SetDirty(fishPrefab);
                PrefabUtility.SavePrefabAsset(fishPrefab);
            }
        }

        private static void ConfigureGameplayScene()
        {
            if (!File.Exists(GameplayScenePath)) return;

            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            ConfigureAudioManager();
            ConfigureNotePoolWrapper();
            RemoveLaneGuide();
            ConfigureOxygenCriticalEffect();
            DisableFeverHandEffect();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        private static void ConfigureAudioManager()
        {
            AudioManager audio = Object.FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
            if (audio == null) return;

            AudioSource sfxSource = GetObjectReference<AudioSource>(audio, "_sfxSource");
            if (sfxSource == null)
            {
                sfxSource = EnsureChildAudioSource(audio.transform, "SfxSource", loop: false, volume: 1f);
                SetObjectReference(audio, "_sfxSource", sfxSource);
            }

            AudioSource ambientSource = GetObjectReference<AudioSource>(audio, "_ambientSource");
            if (ambientSource == null)
            {
                ambientSource = EnsureChildAudioSource(audio.transform, "AmbientSource", loop: true, volume: 0.35f);
                SetObjectReference(audio, "_ambientSource", ambientSource);
            }

            SetObjectReference(audio, "_ambientClip", LoadAssetIfExists<AudioClip>(AmbientPath));
            SetFloat(audio, "_ambientVolume", 0.28f);
            SetSfxEntries(audio);
            EditorUtility.SetDirty(audio);
        }

        private static void SetSfxEntries(AudioManager audio)
        {
            SfxId[] ids =
            {
                SfxId.Perfect,
                SfxId.Good,
                SfxId.Miss,
                SfxId.FishStroke,
                SfxId.OxygenCritical,
                SfxId.FeverStart,
                SfxId.FeverEnd,
                SfxId.GameOver,
                SfxId.UiClick,
                SfxId.UiConfirm
            };

            string[] paths =
            {
                "Assets/_Project/Audio/SFX/Sfx_Perfect.wav",
                "Assets/_Project/Audio/SFX/Sfx_Good.wav",
                "Assets/_Project/Audio/SFX/Sfx_Miss.wav",
                "Assets/_Project/Audio/SFX/Sfx_FishStroke.wav",
                "Assets/_Project/Audio/SFX/Sfx_OxygenCritical.wav",
                "Assets/_Project/Audio/SFX/Sfx_FeverStart.wav",
                "Assets/_Project/Audio/SFX/Sfx_FeverEnd.wav",
                "Assets/_Project/Audio/SFX/Sfx_GameOver.wav",
                "Assets/_Project/Audio/SFX/Sfx_UiClick.wav",
                "Assets/_Project/Audio/SFX/Sfx_UiConfirm.wav"
            };

            float[] volumes = { 0.92f, 0.78f, 0.62f, 0.68f, 0.52f, 0.72f, 0.48f, 0.74f, 0.48f, 0.62f };

            SerializedObject so = new SerializedObject(audio);
            SerializedProperty entries = so.FindProperty("_sfxEntries");
            if (entries == null) return;

            entries.arraySize = ids.Length;
            for (int i = 0; i < ids.Length; i++)
            {
                SerializedProperty item = entries.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("Id").enumValueIndex = (int)ids[i];
                item.FindPropertyRelative("Clip").objectReferenceValue = LoadAssetIfExists<AudioClip>(paths[i]);
                item.FindPropertyRelative("Volume").floatValue = volumes[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveLaneGuide()
        {
            NoteSpawner spawner = Object.FindFirstObjectByType<NoteSpawner>(FindObjectsInactive.Include);
            if (spawner == null) return;

            LanePathGuide guide = spawner.GetComponent<LanePathGuide>();
            Transform lines = spawner.transform.Find("LanePathLines");
            if (lines != null) Object.DestroyImmediate(lines.gameObject, true);
            if (guide != null) Object.DestroyImmediate(guide, true);
            EditorUtility.SetDirty(spawner);
        }

        private static void ConfigureNotePoolWrapper()
        {
            NotePool pool = Object.FindFirstObjectByType<NotePool>(FindObjectsInactive.Include);
            if (pool == null) return;

            FishNote wrapper = LoadPrefabComponent<FishNote>(FishWrapperPrefabPath);
            SetObjectReference(pool, "_fishWrapperPrefab", wrapper);
            SetInt(pool, "_fishPrewarm", 8);
            EditorUtility.SetDirty(pool);
        }

        private static void ConfigureOxygenCriticalEffect()
        {
            HUDController hud = Object.FindFirstObjectByType<HUDController>(FindObjectsInactive.Include);
            if (hud == null) return;

            OxygenCriticalEffect effect = EnsureComponent<OxygenCriticalEffect>(hud.gameObject);
            SetObjectReference(effect, "_targetRoot", hud.transform as RectTransform);
            SetBool(effect, "_forceFullTargetRect", true);
            SetFloat(effect, "_viewWidthOverscan", 1.6f);
            SetFloat(effect, "_viewHeightOverscan", 2.25f);
            SetBool(effect, "_forceStretchedImage", true);
            EditorUtility.SetDirty(effect);
        }

        private static void DisableFeverHandEffect()
        {
            DVariantRiderRig riderRig = Object.FindFirstObjectByType<DVariantRiderRig>(FindObjectsInactive.Include);
            if (riderRig == null) return;

            riderRig.enabled = true;
            SetBool(riderRig, "_readControllerPoseDirectlyFromXRNodes", true);
            SetBool(riderRig, "_enableKeyboardMouseDebugInput", true);
            SetFloat(riderRig, "_horizontalScale", HandHorizontalScale);
            SetFloat(riderRig, "_verticalScale", HandVerticalScale);
            SetFloat(riderRig, "_depthScale", HandDepthScale);
            SetBool(riderRig, "_useCommonPlayfieldHorizontalMapping", true);
            SetFloat(riderRig, "_minimumCommonPlayfieldControllerSpan", 0.12f);
            SetBool(riderRig, "_limitHandWorkspace", false);
            SetVector3(riderRig, "_handWorkspacePositiveLocal", DebugHandWorkspacePositiveLocal);
            SetVector3(riderRig, "_handWorkspaceNegativeLocal", DebugHandWorkspaceNegativeLocal);
            EditorUtility.SetDirty(riderRig);

            FeverHandEffect effect = riderRig.GetComponent<FeverHandEffect>();
            if (effect == null) return;

            effect.enabled = false;
            SetObjectReference(effect, "_riderRig", riderRig);
            SetObjectReference(effect, "_effectPrefab", null);
            SetBool(effect, "_spawnHandEffectOnFever", false);
            SetFloat(effect, "_spawnInterval", 0.18f);
            SetFloat(effect, "_effectLifetime", 1.6f);
            SetFloat(effect, "_followSmoothSpeed", 18f);
            SetBool(effect, "_usePersistentTrails", true);
            SetBool(effect, "_followVisualHandTargets", true);
            SetBool(effect, "_parentTrailToHand", false);
            EditorUtility.SetDirty(effect);
        }

        private static AudioSource EnsureChildAudioSource(Transform parent, string name, bool loop, float volume)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            GameObject go = existing != null ? existing.gameObject : new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);

            AudioSource source = EnsureComponent<AudioSource>(go);
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = volume;
            EditorUtility.SetDirty(source);
            return source;
        }

        private static Sprite LoadSprite(string path)
        {
            if (!File.Exists(path)) return null;

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Material EnsureLaneMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(LaneMaterialPath);
            if (material != null) return material;

            EnsureFolder("Assets/_Project/Art/VFX");
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader)
            {
                name = "LanePathGuide_Mat",
                color = new Color(0.25f, 0.95f, 1f, 0.58f)
            };

            AssetDatabase.CreateAsset(material, LaneMaterialPath);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject FindGameObject(string name)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.scene == activeScene && go.name == name) return go;
            }

            return null;
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static T LoadAssetIfExists<T>(string path) where T : Object
        {
            return File.Exists(path) ? AssetDatabase.LoadAssetAtPath<T>(path) : null;
        }

        private static T LoadPrefabComponent<T>(string path) where T : Component
        {
            GameObject prefab = LoadAssetIfExists<GameObject>(path);
            return prefab != null ? prefab.GetComponent<T>() : null;
        }

        private static T GetObjectReference<T>(Object target, string fieldName) where T : Object
        {
            if (target == null) return null;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            return prop != null ? prop.objectReferenceValue as T : null;
        }

        private static void SetObjectReference(Object target, string fieldName, Object value)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) return;
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetFloat(Object target, string fieldName, float value)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) return;
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetInt(Object target, string fieldName, int value)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) return;
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetBool(Object target, string fieldName, bool value)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) return;
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetVector3(Object target, string fieldName, Vector3 value)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) return;
            prop.vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
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
