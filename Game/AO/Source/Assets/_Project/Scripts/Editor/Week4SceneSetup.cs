using System.IO;
using AO.Character;
using AO.Audio;
using AO.Core;
using AO.Judgement;
using AO.Rhythm;
using AO.State;
using AO.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace AO.Editor
{
    public static class Week4SceneSetup
    {
        private const string TitleScenePath = "Assets/_Project/Scenes/01_Title.unity";
        private const string LobbyScenePath = "Assets/_Project/Scenes/02_Lobby.unity";
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const string ResultScenePath = "Assets/_Project/Scenes/04_Result.unity";
        private const string SongFolder = "Assets/_Project/Settings/Songs";
        private const string PreviewFolder = "Assets/_Project/Audio/BGM/Previews";
        private const string SongLibraryPath = "Assets/_Project/Settings/SongLibrary.asset";
        private const string GameSessionPath = "Assets/_Project/Settings/GameSession.asset";
        private const string JudgementConfigPath = "Assets/_Project/Settings/JudgementConfig.asset";
        private const float MenuCanvasScale = 0.0024f;
        private static readonly Vector3 MenuCameraPosition = new Vector3(0f, 1.45f, -1.1f);
        private static readonly Vector3 TitleCanvasPosition = new Vector3(0f, 1.5f, 0.9f);
        private static readonly Vector3 LobbyCanvasPosition = new Vector3(0f, 1.5f, 1.0f);
        private static readonly Vector3 ResultCanvasPosition = new Vector3(0f, 1.5f, 1.0f);
        private static readonly Vector2 TitleCanvasSize = new Vector2(1000f, 640f);
        private static readonly Vector2 LobbyCanvasSize = new Vector2(1260f, 740f);
        private static readonly Vector2 ResultCanvasSize = new Vector2(1180f, 700f);

        public static void Apply()
        {
            EnsureFolders();
            SongLibrary library = EnsureSongLibrary();
            GameSession session = EnsureGameSession();

            CreateTitleScene();
            CreateLobbyScene(library, session);
            CreateResultScene(session);
            UpdateGameplayScene(library, session);
            UpdateBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AO] Week 4 flow setup complete.");
        }

        public static void ApplyRuntimeInteractionAdditions()
        {
            ApplyUiInputToScene(TitleScenePath);
            ApplyUiInputToScene(LobbyScenePath);
            ApplyUiInputToScene(ResultScenePath);
            ApplyGameplayRuntimeAdditions();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AO] Runtime interaction additions applied.");
        }

        public static void ApplyCameraVisibilitySettings()
        {
            Debug.Log("[AO] Camera visibility overrides are retired. Camera, lighting, and volume settings are now scene-authored.");
        }

        public static void DisableCameraVisibilitySettings()
        {
            Debug.Log("[AO] Camera visibility overrides are retired. No scene camera or volume settings were changed.");
        }

        [MenuItem("AO/UI/Apply Gameplay Settings UI Preview")]
        public static void ApplyGameplaySettingsOverlayFrontOnly()
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            EnsureSettingsCanvasFront();
            BakeEditableGameplaySettingsUiInActiveScene();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AO] Gameplay settings overlay front layout applied.");
        }

        [MenuItem("AO/UI/Bake Editable Gameplay Settings UI")]
        public static void BakeEditableGameplaySettingsUi()
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            EnsureSettingsCanvasFront();
            BakeEditableGameplaySettingsUiInActiveScene();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AO] Editable gameplay settings UI baked into GamePlayScene.");
        }

        public static void ApplyMenuSceneUiLayoutOnly()
        {
            ApplyMenuSceneLayout(TitleScenePath, "TitleCanvas", TitleCanvasPosition, TitleCanvasSize);
            ApplyMenuSceneLayout(LobbyScenePath, "LobbyCanvas", LobbyCanvasPosition, LobbyCanvasSize);
            ApplyMenuSceneLayout(ResultScenePath, "ResultCanvas", ResultCanvasPosition, ResultCanvasSize);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AO] Menu scene UI layout applied to Title, Lobby, and Result scenes.");
        }

        public static void BakeEditableMenuUi()
        {
            BakeEditableMenuUiInScene(TitleScenePath);
            BakeEditableMenuUiInScene(LobbyScenePath);
            BakeEditableMenuUiInScene(ResultScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AO] Editable menu UI baked into Title, Lobby, and Result scenes.");
        }

        [MenuItem("AO/Songs/Refresh Default Song Library")]
        public static void RefreshDefaultSongLibrary()
        {
            EnsureFolders();
            EnsureSongLibrary();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AO] Default song library refreshed.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/Settings");
            EnsureFolder(SongFolder);
            EnsureFolder("Assets/_Project/Scenes");
            EnsureFolder("Assets/_Project/Audio");
            EnsureFolder("Assets/_Project/Audio/BGM");
            EnsureFolder(PreviewFolder);
        }

        private static SongLibrary EnsureSongLibrary()
        {
            SongDefinition twinkle = EnsureSong(
                "Song_Twinkle",
                "twinkle",
                "Synthion - Twinkle",
                "",
                "Assets/_Project/Audio/BGM/Twinkle_Original.wav",
                $"{PreviewFolder}/Twinkle_Preview.wav",
                "Assets/_Project/Beatmaps/Twinkle_Normal.json");

            SongDefinition twinklestar = EnsureSong(
                "Song_Twinklestar",
                "twinklestar",
                "Snail's House - Twinklestar",
                "https://www.youtube.com/watch?v=myiJB8SiIcU",
                "Assets/_Project/Audio/BGM/Twinklestar.wav",
                $"{PreviewFolder}/Twinklestar_Preview.wav",
                "Assets/_Project/Beatmaps/Twinklestar_Normal.json");

            SongDefinition utakata = EnsureSong(
                "Song_Utakata",
                "utakata",
                "Snail's House - Utakata",
                "https://www.youtube.com/watch?v=QDCe1_SzHAc",
                "Assets/_Project/Audio/BGM/Utakata.wav",
                $"{PreviewFolder}/Utakata_Preview.wav",
                "Assets/_Project/Beatmaps/Utakata_Normal.json");

            SongDefinition shinkaiShoujo = EnsureSong(
                "Song_ShinkaiShoujo",
                "shinkai_shoujo",
                "yuuyu feat. Hatsune Miku - Shinkai Shoujo",
                "https://www.youtube.com/watch?v=2CwBFr-Eoxg",
                "Assets/_Project/Audio/BGM/ShinkaiShoujo.wav",
                $"{PreviewFolder}/ShinkaiShoujo_Preview.wav",
                "Assets/_Project/Beatmaps/ShinkaiShoujo_Normal.json");

            SongLibrary library = LoadAsset<SongLibrary>(SongLibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SongLibrary>();
                AssetDatabase.CreateAsset(library, SongLibraryPath);
            }

            library.Songs = MergeDefaultSongs(library.Songs, twinkle, twinklestar, utakata, shinkaiShoujo);
            EditorUtility.SetDirty(library);
            return library;
        }

        private static SongDefinition[] MergeDefaultSongs(SongDefinition[] existingSongs, params SongDefinition[] defaultSongs)
        {
            List<SongDefinition> merged = new List<SongDefinition>();
            HashSet<string> seenIds = new HashSet<string>();

            AddSongs(merged, seenIds, defaultSongs);
            AddSongs(merged, seenIds, existingSongs);
            return merged.ToArray();
        }

        private static void AddSongs(List<SongDefinition> merged, HashSet<string> seenIds, SongDefinition[] songs)
        {
            if (songs == null) return;

            for (int i = 0; i < songs.Length; i++)
            {
                SongDefinition song = songs[i];
                if (song == null) continue;

                string id = string.IsNullOrWhiteSpace(song.SongId) ? song.name : song.SongId;
                if (!seenIds.Add(id)) continue;

                merged.Add(song);
            }
        }

        private static GameSession EnsureGameSession()
        {
            GameSession session = LoadAsset<GameSession>(GameSessionPath);
            if (session == null)
            {
                session = ScriptableObject.CreateInstance<GameSession>();
                AssetDatabase.CreateAsset(session, GameSessionPath);
            }

            if (string.IsNullOrEmpty(session.SelectedSongId)) session.SelectedSongId = "twinkle";
            if (string.IsNullOrEmpty(session.SongName)) session.SongName = "Synthion - Twinkle";
            session.PlaybackSpeed = Mathf.Approximately(session.PlaybackSpeed, 0f) ? 1f : session.PlaybackSpeed;
            session.NoteSpeed = Mathf.Approximately(session.NoteSpeed, 0f) ? 1f : session.NoteSpeed;
            EditorUtility.SetDirty(session);
            return session;
        }

        private static SongDefinition EnsureSong(string assetName, string id, string displayName, string sourceUrl, string audioPath, string previewPath, string beatmapPath)
        {
            string path = $"{SongFolder}/{assetName}.asset";
            SongDefinition song = LoadAsset<SongDefinition>(path);
            if (song == null)
            {
                song = ScriptableObject.CreateInstance<SongDefinition>();
                AssetDatabase.CreateAsset(song, path);
            }

            song.SongId = id;
            song.DisplayName = displayName;
            song.SourceUrl = sourceUrl;
            song.BgmClip = LoadAsset<AudioClip>(audioPath);
            song.PreviewClip = LoadAsset<AudioClip>(previewPath);
            song.NormalBeatmap = LoadAsset<TextAsset>(beatmapPath);
            song.DefaultPlaybackSpeed = 1f;
            song.DefaultNoteSpeed = 1f;
            song.DefaultAudioOffsetSeconds = 0f;
            EditorUtility.SetDirty(song);
            return song;
        }

        private static void CreateTitleScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateEventSystem();
            Canvas canvas = CreateWorldCanvas("TitleCanvas", TitleCanvasPosition, TitleCanvasSize);
            TitleScreenController controller = canvas.gameObject.AddComponent<TitleScreenController>();
            controller.RebuildForEditor();
            EnsureRuntimeVrUiRig(true);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), TitleScenePath);
        }

        private static void CreateLobbyScene(SongLibrary library, GameSession session)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateEventSystem();
            Canvas canvas = CreateWorldCanvas("LobbyCanvas", LobbyCanvasPosition, LobbyCanvasSize);
            var controller = canvas.gameObject.AddComponent<LobbyScreenController>();
            SetObjectReference(controller, "_songLibrary", library);
            SetObjectReference(controller, "_session", session);
            controller.Configure(library, session);
            controller.RebuildForEditor();
            MenuSceneWorld.EnsureCommonBackdrop(MenuSceneCharacterMode.LobbyLeft);
            EnsureRuntimeVrUiRig(true);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), LobbyScenePath);
        }

        private static void CreateResultScene(GameSession session)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateEventSystem();
            Canvas canvas = CreateWorldCanvas("ResultCanvas", ResultCanvasPosition, ResultCanvasSize);
            var controller = canvas.gameObject.AddComponent<ResultScreenController>();
            SetObjectReference(controller, "_session", session);
            controller.Configure(session);
            controller.RebuildForEditor();
            MenuSceneWorld.EnsureCommonBackdrop(MenuSceneCharacterMode.ResultLeft);
            EnsureRuntimeVrUiRig(true);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ResultScenePath);
        }

        private static void UpdateGameplayScene(SongLibrary library, GameSession session)
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            GameObject systems = FindOrCreateRoot("Systems");
            var stats = EnsureComponent<RunStatsTracker>(systems);
            var manager = EnsureComponent<GameStateManager>(systems);
            OxygenSystem oxygen = FindSceneObject<OxygenSystem>();
            RhythmEngine rhythm = FindSceneObject<RhythmEngine>();
            AudioManager audio = FindSceneObject<AudioManager>();
            JudgementConfig judgement = LoadAsset<JudgementConfig>(JudgementConfigPath);
            TextAsset fallbackBeatmap = LoadAsset<TextAsset>("Assets/_Project/Beatmaps/Twinkle_Normal.json");
            AudioClip fallbackClip = LoadAsset<AudioClip>("Assets/_Project/Audio/BGM/Twinkle_Original.wav");

            Canvas settingsCanvas = FindSceneObject<GameplaySettingsOverlay>()?.GetComponentInParent<Canvas>();
            if (settingsCanvas == null)
            {
                settingsCanvas = CreateWorldCanvas("SettingsCanvas", new Vector3(0f, 1.45f, 0.72f), new Vector2(760f, 520f));
                settingsCanvas.gameObject.AddComponent<GameplaySettingsOverlay>();
            }
            EnsureWorldSpaceCanvasInput(settingsCanvas);
            settingsCanvas.overrideSorting = true;
            settingsCanvas.sortingOrder = 500;

            GameplaySettingsOverlay overlay = settingsCanvas.GetComponent<GameplaySettingsOverlay>();
            EternalExitObject eternalExit = EnsureEternalExitObject();
            CharacterSetupTools.ApplyMantaRiderRigToGameplayScene();
            EnsureRuntimeVrUiRig(false);
            EnsureSpectatorCameraRig();

            SetObjectReference(manager, "_session", session);
            SetObjectReference(manager, "_songLibrary", library);
            SetObjectReference(manager, "_rhythmEngine", rhythm);
            SetObjectReference(manager, "_judgementConfig", judgement);
            SetObjectReference(manager, "_oxygenSystem", oxygen);
            SetObjectReference(manager, "_audioManager", audio);
            SetObjectReference(manager, "_settingsOverlay", overlay);
            SetObjectReference(manager, "_eternalExitObject", eternalExit);
            SetObjectReference(manager, "_fallbackBeatmap", fallbackBeatmap);
            SetObjectReference(manager, "_fallbackBgm", fallbackClip);

            Bootstrap bootstrap = FindSceneObject<Bootstrap>();
            if (bootstrap != null) SetBool(bootstrap, "_autoStartOnPlay", false);

            EditorUtility.SetDirty(stats);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        private static EternalExitObject EnsureEternalExitObject()
        {
            GameObject hitAnchor = FindGameObject("HitAnchor");
            Transform parent = hitAnchor != null ? hitAnchor.transform : null;
            GameObject go = parent != null ? FindOrCreateChild(parent, "EternalExitObject") : FindOrCreateRoot("EternalExitObject");
            go.transform.localPosition = new Vector3(0.62f, 0f, 0f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);

            BoxCollider col = EnsureComponent<BoxCollider>(go);
            col.isTrigger = true;

            Rigidbody rb = EnsureComponent<Rigidbody>(go);
            rb.useGravity = false;
            rb.isKinematic = true;

            Renderer renderer = go.GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                renderer = cube.GetComponent<Renderer>();
                cube.transform.SetParent(go.transform, false);
                cube.transform.localPosition = Vector3.zero;
                cube.transform.localRotation = Quaternion.identity;
                cube.transform.localScale = Vector3.one;
            }

            if (renderer != null) renderer.sharedMaterial = CreateMaterial("EternalExit_Mat", new Color(1f, 0.82f, 0.35f, 0.85f));
            EternalExitObject exit = EnsureComponent<EternalExitObject>(go);
            exit.SetAvailable(false);
            return exit;
        }

        private static Canvas CreateWorldCanvas(string name, Vector3 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * MenuCanvasScale;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 80f;
            scaler.referencePixelsPerUnit = 100f;

            EnsureWorldSpaceCanvasInput(canvas);
            return canvas;
        }

        private static void CreateCamera()
        {
            GameObject cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = MenuCameraPosition;
            cameraGo.transform.rotation = Quaternion.identity;
            Camera cam = cameraGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 80f;
            cam.fieldOfView = 60f;
        }

        private static void ApplyMenuSceneLayout(string scenePath, string canvasName, Vector3 canvasPosition, Vector2 canvasSize)
        {
            if (!File.Exists(scenePath)) return;

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Camera cam = Camera.main != null ? Camera.main : FindSceneObject<Camera>();
            if (cam != null)
            {
                cam.transform.position = MenuCameraPosition;
                cam.transform.rotation = Quaternion.identity;
                EditorUtility.SetDirty(cam);
            }

            GameObject canvasGo = FindGameObject(canvasName);
            if (canvasGo != null)
            {
                canvasGo.transform.position = canvasPosition;
                canvasGo.transform.rotation = Quaternion.identity;
                canvasGo.transform.localScale = Vector3.one * MenuCanvasScale;

                RectTransform rect = canvasGo.GetComponent<RectTransform>();
                if (rect != null) rect.sizeDelta = canvasSize;

                Canvas canvas = canvasGo.GetComponent<Canvas>();
                if (canvas != null) EnsureWorldSpaceCanvasInput(canvas);

                CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.dynamicPixelsPerUnit = 80f;
                    scaler.referencePixelsPerUnit = 100f;
                }

                EditorUtility.SetDirty(canvasGo);
            }

            BakeEditableUiInActiveScene();

            if (scenePath == LobbyScenePath)
            {
                MenuSceneWorld.EnsureCommonBackdrop(MenuSceneCharacterMode.LobbyLeft);
            }
            else if (scenePath == ResultScenePath)
            {
                MenuSceneWorld.EnsureCommonBackdrop(MenuSceneCharacterMode.ResultLeft);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        private static void BakeEditableMenuUiInScene(string scenePath)
        {
            if (!File.Exists(scenePath)) return;
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            BakeEditableUiInActiveScene();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        private static void BakeEditableUiInActiveScene()
        {
            TitleScreenController title = Object.FindFirstObjectByType<TitleScreenController>(FindObjectsInactive.Include);
            if (title != null)
            {
                title.RebuildForEditor();
                EditorUtility.SetDirty(title);
            }

            LobbyScreenController lobby = Object.FindFirstObjectByType<LobbyScreenController>(FindObjectsInactive.Include);
            if (lobby != null)
            {
                lobby.RebuildForEditor();
                EditorUtility.SetDirty(lobby);
            }

            ResultScreenController result = Object.FindFirstObjectByType<ResultScreenController>(FindObjectsInactive.Include);
            if (result != null)
            {
                result.RebuildForEditor();
                EditorUtility.SetDirty(result);
            }
        }

        private static void CreateEventSystem()
        {
            GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule), typeof(VrEventSystemBootstrap));
            EnsureEventSystemInput(go);
        }

        private static void ApplyUiInputToScene(string scenePath)
        {
            if (!File.Exists(scenePath)) return;

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EnsureSceneEventSystem();
            EnsureRuntimeVrUiRig(true);

            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                EnsureWorldSpaceCanvasInput(canvas);
            }

            if (scenePath == LobbyScenePath)
            {
                MenuSceneWorld.EnsureCommonBackdrop(MenuSceneCharacterMode.LobbyLeft);
            }
            else if (scenePath == ResultScenePath)
            {
                MenuSceneWorld.EnsureCommonBackdrop(MenuSceneCharacterMode.ResultLeft);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        private static void ApplyGameplayRuntimeAdditions()
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            EnsureSceneEventSystem();

            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                EnsureWorldSpaceCanvasInput(canvas);
            }

            EnsureSettingsCanvasFront();
            BakeEditableGameplaySettingsUiInActiveScene();
            CharacterSetupTools.ApplyMantaRiderRigToGameplayScene();
            EnsureRuntimeVrUiRig(false);
            EnsureSpectatorCameraRig();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        private static void BakeEditableGameplaySettingsUiInActiveScene()
        {
            GameplaySettingsOverlay overlay = Object.FindFirstObjectByType<GameplaySettingsOverlay>(FindObjectsInactive.Include);
            if (overlay == null) return;

            overlay.RebuildForEditor();
            EditorUtility.SetDirty(overlay);
            if (overlay.gameObject != null) EditorUtility.SetDirty(overlay.gameObject);
        }

        private static void EnsureSettingsCanvasFront()
        {
            GameObject settingsGo = FindGameObject("SettingsCanvas");
            if (settingsGo == null) return;

            settingsGo.transform.position = new Vector3(0f, 1.45f, 0.72f);
            settingsGo.transform.rotation = Quaternion.identity;
            settingsGo.transform.localScale = Vector3.one * 0.0024f;

            RectTransform rect = settingsGo.GetComponent<RectTransform>();
            if (rect != null) rect.sizeDelta = new Vector2(760f, 520f);

            Canvas canvas = settingsGo.GetComponent<Canvas>();
            if (canvas != null)
            {
                EnsureWorldSpaceCanvasInput(canvas);
                canvas.overrideSorting = true;
                canvas.sortingOrder = 500;
            }

            CanvasScaler scaler = settingsGo.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.dynamicPixelsPerUnit = 80f;
                scaler.referencePixelsPerUnit = 100f;
            }

            EditorUtility.SetDirty(settingsGo);
        }

        private static void EnsureSceneEventSystem()
        {
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            GameObject go = eventSystem != null ? eventSystem.gameObject : new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EnsureEventSystemInput(go);
        }

        private static void EnsureEventSystemInput(GameObject go)
        {
            if (go.GetComponent<EventSystem>() == null) go.AddComponent<EventSystem>();
            if (go.GetComponent<VrEventSystemBootstrap>() == null) go.AddComponent<VrEventSystemBootstrap>();
            if (go.GetComponent<XRUIInputModule>() == null) go.AddComponent<XRUIInputModule>();

            StandaloneInputModule standalone = go.GetComponent<StandaloneInputModule>();
            if (standalone != null) standalone.enabled = false;

            VrEventSystemBootstrap bootstrap = go.GetComponent<VrEventSystemBootstrap>();
            if (bootstrap != null) bootstrap.EnsureSetup();
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

        private static ControllerUiRayPointer EnsureRuntimeVrUiRig(bool addHmdFollowerToMainCamera)
        {
            GameObject rig = FindOrCreateRoot("RuntimeVrUiRig");
            GameObject left = FindOrCreateChild(rig.transform, "LeftUiRay");
            GameObject right = FindOrCreateChild(rig.transform, "RightUiRay");

            left.transform.localPosition = new Vector3(-0.25f, 1.15f, -0.45f);
            left.transform.localRotation = Quaternion.identity;
            right.transform.localPosition = new Vector3(0.25f, 1.15f, -0.45f);
            right.transform.localRotation = Quaternion.identity;

            ConfigurePoseFollower(EnsureComponent<XRNodePoseFollower>(left), XRNode.LeftHand);
            ConfigurePoseFollower(EnsureComponent<XRNodePoseFollower>(right), XRNode.RightHand);

            ControllerUiRayPointer pointer = EnsureComponent<ControllerUiRayPointer>(rig);
            SetObjectReference(pointer, "_leftRayOrigin", left.transform);
            SetObjectReference(pointer, "_rightRayOrigin", right.transform);
            if (Camera.main != null) SetObjectReference(pointer, "_eventCamera", Camera.main);

            if (addHmdFollowerToMainCamera && Camera.main != null)
            {
                ConfigurePoseFollower(EnsureComponent<XRNodePoseFollower>(Camera.main.gameObject), XRNode.CenterEye);
            }

            EditorUtility.SetDirty(pointer);
            return pointer;
        }

        private static void ConfigurePoseFollower(XRNodePoseFollower follower, XRNode node)
        {
            SetEnum(follower, "_xrNode", node.ToString());
            EditorUtility.SetDirty(follower);
        }

        private static AO.Cameras.SpectatorCameraRig EnsureSpectatorCameraRig()
        {
            GameObject go = FindOrCreateRoot("SpectatorCameraRig");
            AO.Cameras.SpectatorCameraRig rig = EnsureComponent<AO.Cameras.SpectatorCameraRig>(go);
            GameObject target = FindGameObject("MantaRoot");
            if (target == null) target = FindGameObject("DVariantRiderRoot");
            if (target == null) target = FindGameObject("HitAnchor");
            if (target != null) SetObjectReference(rig, "_target", target.transform);
            EditorUtility.SetDirty(rig);
            return rig;
        }

        private static void UpdateBuildSettings()
        {
            string[] paths = { TitleScenePath, LobbyScenePath, GameplayScenePath, ResultScenePath };
            var scenes = new EditorBuildSettingsScene[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                scenes[i] = new EditorBuildSettingsScene(paths[i], true);
            }

            EditorBuildSettings.scenes = scenes;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader) { name = name, color = color };
            material.SetFloat("_Surface", 1f);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static T LoadAsset<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        private static GameObject FindOrCreateRoot(string name)
        {
            GameObject existing = FindGameObject(name);
            return existing != null ? existing : new GameObject(name);
        }

        private static GameObject FindOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            if (existing != null) return existing.gameObject;
            GameObject child = new GameObject(name);
            if (parent != null) child.transform.SetParent(parent, false);
            return child;
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

        private static T FindSceneObject<T>() where T : Object => Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static void SetObjectReference(Object target, string fieldName, Object value)
        {
            if (target == null || value == null) return;
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

        private static void SetEnum(Object target, string fieldName, string enumName)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null || prop.propertyType != SerializedPropertyType.Enum) return;

            for (int i = 0; i < prop.enumNames.Length; i++)
            {
                if (prop.enumNames[i] == enumName)
                {
                    prop.enumValueIndex = i;
                    break;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
