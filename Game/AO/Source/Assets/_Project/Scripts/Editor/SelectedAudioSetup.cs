using System;
using System.IO;
using AO.Audio;
using AO.Core;
using AO.Rhythm;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class SelectedAudioSetup
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const string TitleScenePath = "Assets/_Project/Scenes/01_Title.unity";
        private const string LobbyScenePath = "Assets/_Project/Scenes/02_Lobby.unity";
        private const string ResultScenePath = "Assets/_Project/Scenes/04_Result.unity";

        private const string MenuBgmPath = "Assets/_Project/Audio/BGM/bgm2.mp3";
        private const string FeverEnterPath = "Assets/_Project/Audio/BGM/whale.mp3";
        private const string FeverExitPath = "Assets/_Project/Audio/SFX/catfox_alex-ocean-wave-medium-236012.mp3";
        private const string OxygenCriticalPath = "Assets/_Project/Audio/SFX/freesound_community-hearbeat-71701.mp3";
        private const string PerfectPath = "Assets/_Project/Audio/SFX/pop1.wav";
        private const string GoodPath = "Assets/_Project/Audio/SFX/pop2.wav";
        private const string FeverHitPath = "Assets/_Project/Audio/SFX/pop3.wav";
        private const string MissPath = "Assets/_Project/Audio/SFX/pop7.wav";
        private const string UiClickPath = "Assets/_Project/Audio/SFX/UI touch bubble.wav";
        private const string OxygenConfigPath = "Assets/_Project/Settings/OxygenConfig.asset";

        [MenuItem("AO/Setup/Apply Selected Audio")]
        public static void ApplySelectedAudio()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            AssetDatabase.Refresh();
            ImportIfExists(FeverExitPath);
            ImportIfExists(OxygenCriticalPath);

            ConfigureOxygenConfig();
            ConfigureGameplayScene();
            ConfigureMenuScene(TitleScenePath);
            ConfigureMenuScene(LobbyScenePath);
            ConfigureMenuScene(ResultScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AO] Selected audio setup applied. Only requested BGM/SFX clips were connected.");
        }

        private static void ConfigureOxygenConfig()
        {
            OxygenConfig config = AssetDatabase.LoadAssetAtPath<OxygenConfig>(OxygenConfigPath);
            if (config == null) return;

            SerializedObject so = new SerializedObject(config);
            SerializedProperty threshold = so.FindProperty("CriticalThreshold");
            if (threshold != null) threshold.floatValue = 30f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        private static void ConfigureGameplayScene()
        {
            if (!File.Exists(GameplayScenePath)) return;

            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            AudioManager audio = UnityEngine.Object.FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
            if (audio == null)
            {
                GameObject root = EnsureRoot("AudioManager");
                audio = root.AddComponent<AudioManager>();
            }

            AudioSource bgmSource = GetObjectReference<AudioSource>(audio, "_bgmSource");
            if (bgmSource == null)
            {
                RhythmEngine rhythm = UnityEngine.Object.FindFirstObjectByType<RhythmEngine>(FindObjectsInactive.Include);
                if (rhythm != null)
                {
                    bgmSource = rhythm.GetComponent<AudioSource>();
                    if (bgmSource == null) bgmSource = rhythm.gameObject.AddComponent<AudioSource>();
                }
            }

            AudioSource feverSource = EnsureChildAudioSource(audio.transform, "FeverSource", false, 0f);
            AudioSource feverExitSource = EnsureChildAudioSource(audio.transform, "FeverExitSource", false, 0f);
            AudioSource criticalSource = EnsureChildAudioSource(audio.transform, "OxygenCriticalSource", true, 0f);
            AudioSource sfxSource = EnsureChildAudioSource(audio.transform, "SfxSource", false, 1f);

            SetObjectReference(audio, "_bgmSource", bgmSource);
            SetObjectReference(audio, "_feverSource", feverSource);
            SetObjectReference(audio, "_feverExitSource", feverExitSource);
            SetObjectReference(audio, "_criticalSource", criticalSource);
            SetObjectReference(audio, "_sfxSource", sfxSource);

            SetObjectReference(audio, "_feverClip", LoadClip(FeverEnterPath));
            SetObjectReference(audio, "_feverExitClip", LoadClip(FeverExitPath));
            SetObjectReference(audio, "_criticalClip", LoadClip(OxygenCriticalPath));
            SetObjectReference(audio, "_ambientClip", null);

            SetFloat(audio, "_crossfadeSeconds", 0.65f);
            SetFloat(audio, "_feverLayerVolume", 0.68f);
            SetFloat(audio, "_feverEntryStingerSeconds", 1.4f);
            SetFloat(audio, "_feverExitVolume", 0.55f);
            SetFloat(audio, "_criticalVolume", 0.22f);
            SetFloat(audio, "_criticalFadeSeconds", 0.45f);
            SetFloat(audio, "_bgmFeverVolumeMultiplier", 1.15f);
            SetFloat(audio, "_sfxMinInterval", 0.04f);
            SetFloat(audio, "_hitSfxMinInterval", 0.065f);
            SetBool(audio, "_useLowPassFallback", false);
            SetSfxEntries(audio);

            ConfigureUiClickObject("UiClickSfx", LoadClip(UiClickPath));

            EditorUtility.SetDirty(audio);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        private static void ConfigureMenuScene(string scenePath)
        {
            if (!File.Exists(scenePath)) return;

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            AudioClip menuBgm = LoadClip(MenuBgmPath);
            AudioClip uiClick = LoadClip(UiClickPath);

            GameObject menuAudio = EnsureRoot("MenuAudio");
            AudioSource menuSource = EnsureAudioSource(menuAudio, true, 0f);
            MenuBgmPlayer menuPlayer = EnsureComponent<MenuBgmPlayer>(menuAudio);
            SetObjectReference(menuPlayer, "_source", menuSource);
            SetObjectReference(menuPlayer, "_clip", menuBgm);
            SetFloat(menuPlayer, "_volumeScale", 0.38f);
            SetFloat(menuPlayer, "_previewDuckScale", 0.35f);
            SetFloat(menuPlayer, "_fadeSeconds", 0.65f);

            ConfigureUiClickObject("UiClickSfx", uiClick);

            EditorUtility.SetDirty(menuPlayer);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        private static void SetSfxEntries(AudioManager audio)
        {
            SfxId[] ids =
            {
                SfxId.Perfect,
                SfxId.Good,
                SfxId.Miss,
                SfxId.FeverHit,
                SfxId.UiClick
            };

            string[] paths =
            {
                PerfectPath,
                GoodPath,
                MissPath,
                FeverHitPath,
                UiClickPath
            };

            float[] volumes = { 0.26f, 0.23f, 0.16f, 0.28f, 0.18f };
            float[] startTimes = { 0f, 0f, 0f, 0f, 0f };
            float[] maxDurations = { 0.28f, 0.26f, 0.24f, 0.28f, 0.18f };

            SerializedObject so = new SerializedObject(audio);
            SerializedProperty entries = so.FindProperty("_sfxEntries");
            if (entries == null) return;

            entries.arraySize = ids.Length;
            for (int i = 0; i < ids.Length; i++)
            {
                SerializedProperty item = entries.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("Id").enumValueIndex = (int)ids[i];
                item.FindPropertyRelative("Clip").objectReferenceValue = LoadClip(paths[i]);
                item.FindPropertyRelative("Volume").floatValue = volumes[i];
                item.FindPropertyRelative("StartTime").floatValue = startTimes[i];
                item.FindPropertyRelative("MaxDuration").floatValue = maxDurations[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureUiClickObject(string name, AudioClip clip)
        {
            GameObject go = GameObject.Find(name);
            if (go == null) go = new GameObject(name);

            AudioSource source = EnsureAudioSource(go, false, 1f);
            UiClickSfxPlayer player = EnsureComponent<UiClickSfxPlayer>(go);
            SetObjectReference(player, "_source", source);
            SetObjectReference(player, "_clip", clip);
            SetFloat(player, "_volume", 0.16f);
            SetFloat(player, "_minInterval", 0.045f);
            EditorUtility.SetDirty(player);
        }

        private static AudioClip LoadClip(string path)
        {
            ImportIfExists(path);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static void ImportIfExists(string path)
        {
            if (!File.Exists(path)) return;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static AudioSource EnsureChildAudioSource(Transform parent, string name, bool loop, float volume)
        {
            Transform child = parent.Find(name);
            GameObject go = child != null ? child.gameObject : new GameObject(name);
            if (child == null) go.transform.SetParent(parent, false);
            return EnsureAudioSource(go, loop, volume);
        }

        private static AudioSource EnsureAudioSource(GameObject go, bool loop, float volume)
        {
            AudioSource source = go.GetComponent<AudioSource>();
            if (source == null) source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = volume;
            source.spatialBlend = 0f;
            EditorUtility.SetDirty(source);
            return source;
        }

        private static GameObject EnsureRoot(string name)
        {
            GameObject go = GameObject.Find(name);
            return go != null ? go : new GameObject(name);
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static T GetObjectReference<T>(UnityEngine.Object target, string propertyName) where T : UnityEngine.Object
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(propertyName);
            return prop != null ? prop.objectReferenceValue as T : null;
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null) return;
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(UnityEngine.Object target, string propertyName, float value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null) return;
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(UnityEngine.Object target, string propertyName, bool value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null) return;
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
