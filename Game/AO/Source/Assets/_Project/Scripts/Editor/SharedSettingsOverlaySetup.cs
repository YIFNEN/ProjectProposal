using System.IO;
using AO.Audio;
using AO.Core;
using AO.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace AO.Editor
{
    public static class SharedSettingsOverlaySetup
    {
        private const string TitleScenePath = "Assets/_Project/Scenes/01_Title.unity";
        private const string LobbyScenePath = "Assets/_Project/Scenes/02_Lobby.unity";
        private const string ResultScenePath = "Assets/_Project/Scenes/04_Result.unity";
        private const string SongLibraryPath = "Assets/_Project/Settings/SongLibrary.asset";
        private const string GameSessionPath = "Assets/_Project/Settings/GameSession.asset";
        private const string MenuBgmPath = "Assets/_Project/Audio/BGM/bgm2.mp3";
        private const string UiClickPath = "Assets/_Project/Audio/SFX/UI touch bubble.wav";
        private const float MenuCanvasScale = 0.0024f;

        private static readonly Vector3 SettingsCanvasPosition = new Vector3(0f, 1.45f, 0.72f);
        private static readonly Vector2 SettingsCanvasSize = new Vector2(760f, 520f);

        [MenuItem("AO/Setup/Apply Shared Settings Overlay To Menu Scenes")]
        public static void ApplyToMenuScenes()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            AssetDatabase.Refresh();
            ConfigureMenuScene(TitleScenePath);
            ConfigureMenuScene(LobbyScenePath);
            ConfigureMenuScene(ResultScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AO] Shared settings overlay applied to Title, Lobby, and Result scenes.");
        }

        private static void ConfigureMenuScene(string scenePath)
        {
            if (!File.Exists(scenePath)) return;

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            EnsureSceneEventSystem();
            EnsureMenuBgm();
            EnsureUiClickSfx();

            Canvas canvas = EnsureSettingsCanvas();
            GameplaySettingsOverlay overlay = EnsureComponent<GameplaySettingsOverlay>(canvas.gameObject);
            Transform panel = canvas.transform.Find("SettingsPanel");
            SongLibrary library = AssetDatabase.LoadAssetAtPath<SongLibrary>(SongLibraryPath);
            GameSession session = AssetDatabase.LoadAssetAtPath<GameSession>(GameSessionPath);

            SetObjectReference(overlay, "_songLibrary", library);
            SetObjectReference(overlay, "_session", session);
            SetBool(overlay, "_showGameplayControls", true);
            SetBool(overlay, "_placeInFrontOfCameraOnOpen", true);
            SetBool(overlay, "_preferSceneLayout", true);
            SetFloat(overlay, "_cameraDistance", 0.85f);
            SetInt(overlay, "_sortingOrderWhenOpen", 600);

            if (panel == null)
            {
                SetBool(overlay, "_allowRuntimeUiCreation", true);
                overlay.RebuildForEditor();
                panel = canvas.transform.Find("SettingsPanel");
            }
            else
            {
                overlay.RefreshSceneObjectsForEditor();
            }

            if (panel != null)
            {
                SetObjectReference(overlay, "_panel", panel.gameObject);
                panel.gameObject.SetActive(false);
                EditorUtility.SetDirty(panel.gameObject);
            }

            SetBool(overlay, "_allowRuntimeUiCreation", false);
            EditorUtility.SetDirty(overlay);
            EditorUtility.SetDirty(canvas.gameObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        private static Canvas EnsureSettingsCanvas()
        {
            GameObject go = FindGameObject("SettingsCanvas");
            if (go == null)
            {
                go = new GameObject("SettingsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            }

            go.transform.position = SettingsCanvasPosition;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * MenuCanvasScale;

            RectTransform rect = go.GetComponent<RectTransform>();
            if (rect != null) rect.sizeDelta = SettingsCanvasSize;

            Canvas canvas = EnsureComponent<Canvas>(go);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 600;

            CanvasScaler scaler = EnsureComponent<CanvasScaler>(go);
            scaler.dynamicPixelsPerUnit = 80f;
            scaler.referencePixelsPerUnit = 100f;

            EnsureWorldSpaceCanvasInput(canvas);
            return canvas;
        }

        private static void EnsureMenuBgm()
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MenuBgmPath);
            GameObject go = FindOrCreateRoot("MenuAudio");
            AudioSource source = EnsureAudioSource(go, true, 0f);
            MenuBgmPlayer player = EnsureComponent<MenuBgmPlayer>(go);
            SetObjectReference(player, "_source", source);
            SetObjectReference(player, "_clip", clip);
            SetFloat(player, "_volumeScale", 0.38f);
            SetFloat(player, "_previewDuckScale", 0.35f);
            SetFloat(player, "_fadeSeconds", 0.65f);
            EditorUtility.SetDirty(player);
        }

        private static void EnsureUiClickSfx()
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(UiClickPath);
            GameObject go = FindOrCreateRoot("UiClickSfx");
            AudioSource source = EnsureAudioSource(go, false, 1f);
            UiClickSfxPlayer player = EnsureComponent<UiClickSfxPlayer>(go);
            SetObjectReference(player, "_source", source);
            SetObjectReference(player, "_clip", clip);
            SetFloat(player, "_volume", 0.16f);
            SetFloat(player, "_minInterval", 0.045f);
            EditorUtility.SetDirty(player);
        }

        private static void EnsureSceneEventSystem()
        {
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            GameObject go = eventSystem != null ? eventSystem.gameObject : new GameObject("EventSystem", typeof(EventSystem));

            if (go.GetComponent<VrEventSystemBootstrap>() == null) go.AddComponent<VrEventSystemBootstrap>();
            if (go.GetComponent<XRUIInputModule>() == null) go.AddComponent<XRUIInputModule>();

            StandaloneInputModule standalone = go.GetComponent<StandaloneInputModule>();
            if (standalone != null) standalone.enabled = false;

            VrEventSystemBootstrap bootstrap = go.GetComponent<VrEventSystemBootstrap>();
            if (bootstrap != null) bootstrap.EnsureSetup();
            EditorUtility.SetDirty(go);
        }

        private static void EnsureWorldSpaceCanvasInput(Canvas canvas)
        {
            if (canvas == null) return;
            canvas.renderMode = RenderMode.WorldSpace;
            if (canvas.worldCamera == null) canvas.worldCamera = Camera.main;
            if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
            if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null) canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            if (canvas.GetComponent<WorldSpaceVrCanvas>() == null) canvas.gameObject.AddComponent<WorldSpaceVrCanvas>();
        }

        private static AudioSource EnsureAudioSource(GameObject go, bool loop, float volume)
        {
            AudioSource source = EnsureComponent<AudioSource>(go);
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = volume;
            source.spatialBlend = 0f;
            EditorUtility.SetDirty(source);
            return source;
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            GameObject existing = FindGameObject(name);
            return existing != null ? existing : new GameObject(name);
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
    }
}
