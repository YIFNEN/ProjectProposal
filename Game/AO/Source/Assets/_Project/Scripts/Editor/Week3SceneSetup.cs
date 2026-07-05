using System;
using System.Linq;
using AO.Audio;
using AO.Core;
using AO.Judgement;
using AO.Player;
using AO.Rhythm;
using AO.State;
using AO.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

namespace AO.Editor
{
    public static class Week3SceneSetup
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const string HudPrefabPath = "Assets/_Project/Prefabs/HUD/HUDCanvas.prefab";
        private const float HandTriggerRadius = 0.1125f;

        public static void Apply()
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            var judgement = LoadAsset<JudgementConfig>("Assets/_Project/Settings/JudgementConfig.asset");
            var comboConfig = LoadAsset<ComboConfig>("Assets/_Project/Settings/ComboConfig.asset");
            var oxygenConfig = LoadAsset<OxygenConfig>("Assets/_Project/Settings/OxygenConfig.asset");
            var feverConfig = LoadAsset<FeverConfig>("Assets/_Project/Settings/FeverConfig.asset");

            GameObject systems = FindOrCreateRoot("Systems");
            var combo = EnsureComponent<ComboSystem>(systems);
            var oxygen = EnsureComponent<OxygenSystem>(systems);
            var score = EnsureComponent<ScoreSystem>(systems);
            var fever = EnsureComponent<FeverSystem>(systems);
            var eventLogger = EnsureComponent<EventLogger>(systems);

            score.enabled = true;
            SetObjectReference(combo, "_config", comboConfig);
            SetObjectReference(oxygen, "_config", oxygenConfig);
            SetObjectReference(score, "_judgementConfig", judgement);
            SetObjectReference(score, "_comboSystem", combo);
            SetFloat(score, "_feverMultiplier", feverConfig != null ? feverConfig.ScoreMultiplier : 1.2f);
            SetObjectReference(fever, "_config", feverConfig);
            SetObjectReference(fever, "_comboSystem", combo);
            eventLogger.enabled = false;

            RhythmEngine rhythm = FindSceneObject<RhythmEngine>();
            if (rhythm != null) SetObjectReference(rhythm, "_judgementConfig", judgement);

            NoteSpawner spawner = FindSceneObject<NoteSpawner>();
            if (spawner != null) SetObjectReference(spawner, "_judgementConfig", judgement);
            ApplyNoteSpawnerLaneOffsets();

            SetupAudioManager(rhythm, feverConfig);
            SetupHands();
            SetupJudgementFrame();
            SetAllDebugHitPadsActive(false);
            SetupHudCanvas();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AO] Week 3 gameplay scene setup complete.");
        }

        public static void AddJudgementFrameOnly()
        {
            if (SceneManager.GetActiveScene().path != GameplayScenePath)
            {
                EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            }

            SetupJudgementFrame();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AO] Judgement frame setup complete.");
        }

        public static void AddDebugHitPadsOnly()
        {
            if (SceneManager.GetActiveScene().path != GameplayScenePath)
            {
                EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            }

            EnsureTag("Hand");
            SetupDebugHitPads();
            SetAllDebugHitPadsActive(false);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AO] Debug hit pads setup complete.");
        }

        public static void ApplyDiagonalLaneLayout()
        {
            if (SceneManager.GetActiveScene().path != GameplayScenePath)
            {
                EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            }

            ApplyNoteSpawnerLaneOffsets();
            SetupJudgementFrame(forceLaneOffsets: true);
            SetAllDebugHitPadsActive(false);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AO] Diagonal lane layout applied.");
        }

        public static void ApplyGameplayHudLayoutOnly()
        {
            if (SceneManager.GetActiveScene().path != GameplayScenePath)
            {
                EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            }

            SetupHudCanvas();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AO] Gameplay HUD layout setup complete.");
        }

        [MenuItem("AO/Test/Enable Gameplay Test Mode")]
        public static void EnableGameplayTestMode()
        {
            EnsureGameplaySceneOpen();

            SetupRuntimeAudioSourceSafety();
            SetupDebugHitPads();
            SetAllDebugHitPadsActive(false);

            Bootstrap bootstrap = FindSceneObject<Bootstrap>();
            if (bootstrap != null)
            {
                SetBool(bootstrap, "_autoStartOnPlay", false);
                SetFloat(bootstrap, "_extraDelay", 0.5f);
            }

            GameStateManager gameState = FindSceneObject<GameStateManager>();
            if (gameState != null)
            {
                SetBool(gameState, "_startOnAwake", true);
            }

            GameObject systems = FindGameObject("Systems");
            if (systems != null)
            {
                var eventLogger = EnsureComponent<EventLogger>(systems);
                SetFloat(eventLogger, "_oxygenLogThreshold", 2.5f);
                SetInt(eventLogger, "_scoreLogThreshold", 1);
                eventLogger.enabled = true;
                EditorUtility.SetDirty(eventLogger);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("[AO] Gameplay test mode enabled. EventLogger is on, Rhythm AudioSource Play On Awake is off, and all DebugHitPads are prepared but inactive.");
        }

        [MenuItem("AO/Test/Disable Gameplay Test Mode")]
        public static void DisableGameplayTestMode()
        {
            EnsureGameplaySceneOpen();

            GameObject systems = FindGameObject("Systems");
            if (systems != null)
            {
                var eventLogger = systems.GetComponent<EventLogger>();
                if (eventLogger != null)
                {
                    eventLogger.enabled = false;
                    EditorUtility.SetDirty(eventLogger);
                }
            }

            SetAllDebugHitPadsActive(false);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("[AO] Gameplay test mode disabled. EventLogger and all DebugHitPads are off.");
        }

        [MenuItem("AO/Test/Debug Hit Pads/Disable All")]
        public static void DisableAllDebugHitPads()
        {
            EnsureGameplaySceneOpen();
            SetAllDebugHitPadsActive(false);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        [MenuItem("AO/Test/Debug Hit Pads/Only Up")]
        public static void EnableOnlyUpDebugHitPad() => EnableOnlyDebugHitPad("DebugHitPad_Up");

        [MenuItem("AO/Test/Debug Hit Pads/Only Down")]
        public static void EnableOnlyDownDebugHitPad() => EnableOnlyDebugHitPad("DebugHitPad_Down");

        [MenuItem("AO/Test/Debug Hit Pads/Only Left")]
        public static void EnableOnlyLeftDebugHitPad() => EnableOnlyDebugHitPad("DebugHitPad_Left");

        [MenuItem("AO/Test/Debug Hit Pads/Only Right")]
        public static void EnableOnlyRightDebugHitPad() => EnableOnlyDebugHitPad("DebugHitPad_Right");

        [MenuItem("AO/Test/Debug Hit Pads/Only Center")]
        public static void EnableOnlyCenterDebugHitPad() => EnableOnlyDebugHitPad("DebugHitPad_Center");

        private static void SetupAudioManager(RhythmEngine rhythm, FeverConfig feverConfig)
        {
            GameObject audioRoot = FindOrCreateRoot("AudioManager");
            var manager = EnsureComponent<AudioManager>(audioRoot);

            AudioSource bgmSource = rhythm != null ? rhythm.GetComponent<AudioSource>() : null;
            if (bgmSource == null && rhythm != null) bgmSource = rhythm.gameObject.AddComponent<AudioSource>();
            if (bgmSource != null)
            {
                bgmSource.playOnAwake = false;
                bgmSource.loop = false;
            }

            AudioSource feverSource = EnsureAudioChild(audioRoot.transform, "FeverSource", loop: false);
            AudioSource sfxSource = EnsureAudioChild(audioRoot.transform, "SfxSource", loop: false);
            AudioSource ambientSource = EnsureAudioChild(audioRoot.transform, "AmbientSource", loop: true);

            SetObjectReference(manager, "_bgmSource", bgmSource);
            SetObjectReference(manager, "_feverSource", feverSource);
            SetObjectReference(manager, "_sfxSource", sfxSource);
            SetObjectReference(manager, "_ambientSource", ambientSource);
            SetFloat(manager, "_crossfadeSeconds", feverConfig != null ? feverConfig.CrossfadeSeconds : 0.5f);
        }

        private static void SetupRuntimeAudioSourceSafety()
        {
            RhythmEngine rhythm = FindSceneObject<RhythmEngine>();
            AudioSource bgmSource = rhythm != null ? rhythm.GetComponent<AudioSource>() : null;
            if (bgmSource == null && rhythm != null) bgmSource = rhythm.gameObject.AddComponent<AudioSource>();
            if (bgmSource == null) return;

            bgmSource.playOnAwake = false;
            bgmSource.loop = false;
            EditorUtility.SetDirty(bgmSource);
        }

        private static void SetupHands()
        {
            EnsureTag("Hand");
            SetupJudgementHandTargets();
        }

        private static HandTracker SetupHand(Transform parent, string name, XRNode node, Vector3 fallbackLocalPosition)
        {
            GameObject hand = parent != null ? FindOrCreateChild(parent, name) : FindOrCreateRoot(name);
            hand.SetActive(true);
            hand.tag = "Untagged";
            hand.transform.localPosition = fallbackLocalPosition;
            hand.transform.localRotation = Quaternion.identity;
            hand.transform.localScale = Vector3.one;

            var collider = EnsureComponent<SphereCollider>(hand);
            collider.isTrigger = true;
            collider.radius = HandTriggerRadius;
            collider.enabled = false;

            var body = EnsureComponent<Rigidbody>(hand);
            body.useGravity = false;
            body.isKinematic = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var follower = EnsureComponent<XRHandPoseFollower>(hand);
            var tracker = EnsureComponent<HandTracker>(hand);
            SetEnumByName(follower, "_xrNode", node.ToString());
            SetFloat(follower, "_positionSensitivity", 1f);
            SetBool(follower, "_useInitialPoseAsNeutral", true);
            SetEnumByName(tracker, "_xrNode", node.ToString());

            return tracker;
        }

        private static void SetupJudgementHandTargets()
        {
            Transform judgementRig = FindGameObject("JudgementRig")?.transform;
            if (judgementRig == null) return;

            ConfigureJudgementHandTarget(judgementRig.Find("LeftHandTarget"), XRNode.LeftHand);
            ConfigureJudgementHandTarget(judgementRig.Find("RightHandTarget"), XRNode.RightHand);
        }

        private static void ConfigureJudgementHandTarget(Transform target, XRNode hapticNode)
        {
            if (target == null) return;

            var judgementHand = EnsureComponent<JudgementHandTarget>(target.gameObject);
            judgementHand.Configure(hapticNode, HandTriggerRadius);
            EditorUtility.SetDirty(judgementHand);
        }

        private static void SetupJudgementFrame(bool forceLaneOffsets = false)
        {
            GameObject hitAnchor = FindGameObject("HitAnchor");
            if (hitAnchor == null)
            {
                Debug.LogWarning("[AO] HitAnchor not found. JudgementFrame was not created.");
                return;
            }

            GameObject frameObject = FindOrCreateChild(hitAnchor.transform, "JudgementFrame");
            frameObject.transform.localPosition = Vector3.zero;
            frameObject.transform.localRotation = Quaternion.identity;
            frameObject.transform.localScale = Vector3.one;

            bool isNewFrame = frameObject.GetComponent<JudgementFrame>() == null;
            var frame = EnsureComponent<JudgementFrame>(frameObject);
            if (isNewFrame)
            {
                SetFloat(frame, "_width", 0.55f);
                SetFloat(frame, "_height", 0.55f);
                SetVector3(frame, "_localOffset", Vector3.zero);
                SetFloat(frame, "_lineWidth", 0.012f);
                SetColor(frame, "_lineColor", new Color(0.25f, 0.88f, 0.88f, 0.42f));
                SetBool(frame, "_visible", true);
                SetBool(frame, "_showCenterCross", true);
                SetBool(frame, "_showLaneMarkers", true);
                SetFloat(frame, "_guideLineWidth", 0.006f);
                SetFloat(frame, "_laneMarkerRadius", 0.035f);
                SetColor(frame, "_guideColor", new Color(0.25f, 0.88f, 0.88f, 0.16f));
                SetColor(frame, "_laneMarkerColor", new Color(1f, 0.92f, 0.38f, 0.45f));
            }

            if (isNewFrame || forceLaneOffsets)
            {
                ApplyJudgementFrameLaneOffsets(frame);
            }

            frame.Rebuild();
        }

        private static void ApplyNoteSpawnerLaneOffsets()
        {
            NoteSpawner spawner = FindSceneObject<NoteSpawner>();
            if (spawner == null) return;

            SetVector3(spawner, "_laneUpOffset", LaneLayout.GetDefaultOffset(Lane.Up));
            SetVector3(spawner, "_laneDownOffset", LaneLayout.GetDefaultOffset(Lane.Down));
            SetVector3(spawner, "_laneLeftOffset", LaneLayout.GetDefaultOffset(Lane.Left));
            SetVector3(spawner, "_laneRightOffset", LaneLayout.GetDefaultOffset(Lane.Right));
            SetVector3(spawner, "_laneCenterOffset", LaneLayout.GetDefaultOffset(Lane.Center));
            SetBool(spawner, "_useSharedFanSpawnPoint", true);
            SetVector3(spawner, "_sharedSpawnOffset", LaneLayout.TopMidSpawnOffset);
        }

        private static void ApplyJudgementFrameLaneOffsets(JudgementFrame frame)
        {
            if (frame == null) return;

            SetVector3(frame, "_laneUpOffset", LaneLayout.GetDefaultOffset(Lane.Up));
            SetVector3(frame, "_laneDownOffset", LaneLayout.GetDefaultOffset(Lane.Down));
            SetVector3(frame, "_laneLeftOffset", LaneLayout.GetDefaultOffset(Lane.Left));
            SetVector3(frame, "_laneRightOffset", LaneLayout.GetDefaultOffset(Lane.Right));
            SetVector3(frame, "_laneCenterOffset", LaneLayout.GetDefaultOffset(Lane.Center));
        }

        private static void SetupDebugHitPads()
        {
            EnsureTag("Hand");

            GameObject hitAnchor = FindGameObject("HitAnchor");
            if (hitAnchor == null)
            {
                Debug.LogWarning("[AO] HitAnchor not found. Debug hit pads were not created.");
                return;
            }

            GameObject root = FindOrCreateChild(hitAnchor.transform, "DebugHitPads");
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            CreateDebugHitPad(root.transform, Lane.Up, "DebugHitPad_Up", LaneLayout.GetDefaultOffset(Lane.Up), new Color(0.45f, 0.8f, 1f, 0.6f));
            CreateDebugHitPad(root.transform, Lane.Down, "DebugHitPad_Down", LaneLayout.GetDefaultOffset(Lane.Down), new Color(0.2f, 0.55f, 1f, 0.6f));
            CreateDebugHitPad(root.transform, Lane.Left, "DebugHitPad_Left", LaneLayout.GetDefaultOffset(Lane.Left), new Color(0.75f, 0.95f, 0.55f, 0.6f));
            CreateDebugHitPad(root.transform, Lane.Right, "DebugHitPad_Right", LaneLayout.GetDefaultOffset(Lane.Right), new Color(1f, 0.7f, 0.45f, 0.6f));
            CreateDebugHitPad(root.transform, Lane.Center, "DebugHitPad_Center", LaneLayout.GetDefaultOffset(Lane.Center), new Color(1f, 0.9f, 0.35f, 0.6f));
            root.SetActive(false);
        }

        private static void CreateDebugHitPad(Transform parent, Lane lane, string name, Vector3 laneOffset, Color color)
        {
            Transform existing = parent.Find(name);
            GameObject pad = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            if (existing == null)
            {
                pad.name = name;
                pad.transform.SetParent(parent, false);
            }

            pad.tag = "Hand";
            pad.transform.localRotation = Quaternion.identity;
            pad.transform.localScale = Vector3.one;

            var collider = EnsureComponent<SphereCollider>(pad);
            collider.isTrigger = true;
            collider.radius = HandTriggerRadius;
            collider.center = Vector3.zero;

            var body = EnsureComponent<Rigidbody>(pad);
            body.useGravity = false;
            body.isKinematic = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            Renderer renderer = pad.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateDebugHitPadMaterial(name, color);
                renderer.enabled = true;
            }

            var hitPad = EnsureComponent<DebugLaneHitPad>(pad);
            hitPad.Configure(lane, laneOffset, 0f, HandTriggerRadius);
            pad.SetActive(false);
            EditorUtility.SetDirty(pad);
        }

        private static Material CreateDebugHitPadMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var material = new Material(shader)
            {
                name = $"{name}_DebugMaterial",
                color = color
            };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_AlphaClip", 0f);
            return material;
        }

        private static void SetupHudCanvas()
        {
            GameObject hud = FindGameObject("HUDCanvas");
            if (hud == null)
            {
                hud = new GameObject("HUDCanvas", typeof(RectTransform));
            }

            hud.transform.position = new Vector3(0f, 1.55f, 1.25f);
            hud.transform.rotation = Quaternion.identity;
            hud.transform.localScale = Vector3.one * 0.0018f;

            RectTransform hudRect = hud.GetComponent<RectTransform>();
            if (hudRect == null)
            {
                Debug.LogWarning("[AO] HUDCanvas exists without a RectTransform. Rename or remove it, then run setup again.");
                return;
            }
            hudRect.sizeDelta = new Vector2(900f, 360f);

            var canvas = EnsureComponent<Canvas>(hud);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;

            var scaler = EnsureComponent<CanvasScaler>(hud);
            scaler.dynamicPixelsPerUnit = 80f;
            scaler.referencePixelsPerUnit = 100f;
            EnsureComponent<GraphicRaycaster>(hud);

            var controller = EnsureComponent<HUDController>(hud);
            SetBool(controller, "_faceCamera", true);
            if (Camera.main != null) SetObjectReference(controller, "_cameraTarget", Camera.main.transform);

            RemoveChild(hudRect, "PlayAreaFrame");
            SetupOxygenBar(hudRect);
            SetupFeverGauge(hudRect);
            SetupScoreDisplay(hudRect);
            SetupComboCounter(hudRect);
            SetupJudgementPopup(hudRect);

            PrefabUtility.SaveAsPrefabAssetAndConnect(hud, HudPrefabPath, InteractionMode.AutomatedAction);
        }

        private static void SetupOxygenBar(RectTransform parent)
        {
            RectTransform root = EnsureRectChild(parent, "OxygenBar", new Vector2(-390f, -18f), new Vector2(80f, 240f));
            Image bg = EnsureImage(root.gameObject, new Color(0.04f, 0.09f, 0.12f, 0.72f));
            bg.raycastTarget = false;

            RectTransform fillRect = EnsureRectChild(root, "Fill", Vector2.zero, Vector2.zero);
            Stretch(fillRect);
            Image fill = EnsureImage(fillRect.gameObject, new Color(0.25f, 0.88f, 0.88f, 1f));
            fill.type = Image.Type.Simple;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;

            var bar = EnsureComponent<OxygenBar>(root.gameObject);
            SetObjectReference(bar, "_fillImage", fill);
        }

        private static void SetupFeverGauge(RectTransform parent)
        {
            RectTransform root = EnsureRectChild(parent, "FeverGauge", new Vector2(-390f, -18f), new Vector2(80f, 240f));
            Image bg = EnsureImage(root.gameObject, new Color(0f, 0f, 0f, 0f));
            bg.raycastTarget = false;

            RemoveChild(root, "Fill");

            RectTransform markerRect = EnsureRectChild(root, "Marker", new Vector2(0f, -140f), new Vector2(18f, 18f));
            Image marker = EnsureImage(markerRect.gameObject, new Color(1f, 0.82f, 0.48f, 0.95f));
            marker.raycastTarget = false;

            RectTransform labelRect = EnsureRectChild(root, "Label", new Vector2(0f, -172f), new Vector2(80f, 22f));
            TextMeshProUGUI label = EnsureText(labelRect.gameObject, "0%", 18f, TextAlignmentOptions.Center);
            label.gameObject.SetActive(false);

            var gauge = EnsureComponent<FeverGauge>(root.gameObject);
            SetObjectReference(gauge, "_fillImage", null);
            SetObjectReference(gauge, "_marker", markerRect);
            SetObjectReference(gauge, "_label", null);
            SetColor(gauge, "_chargingColor", new Color(1f, 0.82f, 0.48f, 0.95f));
            SetColor(gauge, "_activeColor", new Color(0.25f, 0.88f, 0.88f, 1f));
            SetBool(gauge, "_transparentOverlay", true);
            SetFloat(gauge, "_overlayAlpha", 0.38f);
            SetBool(gauge, "_showLabel", false);
            SetFloat(gauge, "_markerPadding", 10f);
        }

        private static void SetupScoreDisplay(RectTransform parent)
        {
            RectTransform root = EnsureRectChild(parent, "ScoreDisplay", new Vector2(315f, 168f), new Vector2(120f, 58f));
            TextMeshProUGUI legacyRootText = root.GetComponent<TextMeshProUGUI>();
            if (legacyRootText != null)
            {
                UnityEngine.Object.DestroyImmediate(legacyRootText);
            }

            Image bg = EnsureImage(root.gameObject, new Color(0.92f, 0.96f, 1f, 0.92f));
            bg.raycastTarget = false;

            RectTransform valueRect = EnsureRectChild(root, "Value", Vector2.zero, new Vector2(110f, 50f));
            TextMeshProUGUI text = EnsureText(valueRect.gameObject, "0", 26f, TextAlignmentOptions.Center);
            text.color = new Color(0.02f, 0.08f, 0.12f, 1f);
            var display = EnsureComponent<ScoreDisplay>(root.gameObject);
            SetObjectReference(display, "_scoreText", text);
        }

        private static void SetupComboCounter(RectTransform parent)
        {
            RectTransform root = EnsureRectChild(parent, "ComboCounter", new Vector2(0f, 224f), new Vector2(420f, 78f), true);
            if (root == null) return;

            Image bg = EnsureImage(root.gameObject, new Color(0.04f, 0.14f, 0.19f, 0.34f));
            bg.raycastTarget = false;
            EnsureStretchBorder(root, "Top", BorderSide.Top, 2f, new Color(0.78f, 0.94f, 1f, 0.75f));
            EnsureStretchBorder(root, "Bottom", BorderSide.Bottom, 2f, new Color(0.78f, 0.94f, 1f, 0.75f));
            EnsureStretchBorder(root, "Left", BorderSide.Left, 2f, new Color(0.78f, 0.94f, 1f, 0.75f));
            EnsureStretchBorder(root, "Right", BorderSide.Right, 2f, new Color(0.78f, 0.94f, 1f, 0.75f));

            RectTransform comboRect = EnsureRectChild(root, "ComboText", new Vector2(0f, 9f), new Vector2(380f, 42f), true);
            if (comboRect == null) return;
            TextMeshProUGUI comboText = EnsureText(comboRect.gameObject, "", 31f, TextAlignmentOptions.Center);

            RectTransform multiplierRect = EnsureRectChild(root, "MultiplierText", new Vector2(0f, -24f), new Vector2(160f, 28f), true);
            if (multiplierRect == null) return;
            TextMeshProUGUI multiplierText = EnsureText(multiplierRect.gameObject, "", 20f, TextAlignmentOptions.Center);

            var counter = EnsureComponent<ComboCounter>(root.gameObject);
            SetObjectReference(counter, "_comboText", comboText);
            SetObjectReference(counter, "_multiplierText", multiplierText);
        }

        private static void SetupJudgementPopup(RectTransform parent)
        {
            RectTransform root = EnsureRectChild(parent, "JudgementPopup", new Vector2(0f, -18f), new Vector2(520f, 150f));
            TextMeshProUGUI label = EnsureText(root.gameObject, "", 54f, TextAlignmentOptions.Center);
            var popup = EnsureComponent<JudgementPopup>(root.gameObject);
            SetObjectReference(popup, "_label", label);
        }

        private static void EnsureBorder(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            RectTransform rect = EnsureRectChild(parent, name, anchoredPosition, size);
            Image image = EnsureImage(rect.gameObject, color);
            image.raycastTarget = false;
        }

        private enum BorderSide
        {
            Top,
            Bottom,
            Left,
            Right
        }

        private static void EnsureStretchBorder(RectTransform parent, string name, BorderSide side, float thickness, Color color)
        {
            RectTransform rect = FindOrCreateRectChild(parent, name);
            if (rect == null) return;

            switch (side)
            {
                case BorderSide.Top:
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(0f, thickness);
                    break;
                case BorderSide.Bottom:
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = new Vector2(1f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(0f, thickness);
                    break;
                case BorderSide.Left:
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(thickness, 0f);
                    break;
                case BorderSide.Right:
                    rect.anchorMin = new Vector2(1f, 0f);
                    rect.anchorMax = Vector2.one;
                    rect.pivot = new Vector2(1f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(thickness, 0f);
                    break;
            }

            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            Image image = EnsureImage(rect.gameObject, color);
            image.raycastTarget = false;
        }

        private static AudioSource EnsureAudioChild(Transform parent, string name, bool loop)
        {
            GameObject child = FindOrCreateChild(parent, name);
            var source = EnsureComponent<AudioSource>(child);
            source.playOnAwake = false;
            source.loop = loop;
            return source;
        }

        private static TextMeshProUGUI EnsureText(GameObject go, string text, float size, TextAlignmentOptions alignment)
        {
            var tmp = EnsureComponent<TextMeshProUGUI>(go);
            tmp.text = text;
            tmp.fontSize = size;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = Mathf.Max(8f, size * 0.5f);
            tmp.fontSizeMax = size;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Image EnsureImage(GameObject go, Color color)
        {
            var image = EnsureComponent<Image>(go);
            image.color = color;
            return image;
        }

        private static RectTransform EnsureRectChild(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, bool preserveExistingRect = false)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            bool wasExisting = existing != null;
            GameObject child = wasExisting ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            if (existing == null && parent != null) child.transform.SetParent(parent, false);

            RectTransform rect = child.GetComponent<RectTransform>();
            if (rect == null)
            {
                Debug.LogWarning($"[AO] {name} exists without a RectTransform. Skipping UI setup for this object.");
                return null;
            }

            if (preserveExistingRect && wasExisting) return rect;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            if (size != Vector2.zero) rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return rect;
        }

        private static RectTransform FindOrCreateRectChild(Transform parent, string name)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            GameObject child = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            if (existing == null && parent != null) child.transform.SetParent(parent, false);

            RectTransform rect = child.GetComponent<RectTransform>();
            if (rect != null) return rect;

            Debug.LogWarning($"[AO] {name} exists without a RectTransform. Skipping UI setup for this object.");
            return null;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            GameObject existing = FindGameObject(name);
            if (existing != null) return existing;
            return new GameObject(name);
        }

        private static GameObject FindOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            if (existing != null) return existing.gameObject;

            var child = new GameObject(name);
            if (parent != null) child.transform.SetParent(parent, false);
            return child;
        }

        private static void RemoveChild(Transform parent, string name)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            if (existing == null) return;

            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        private static void EnsureGameplaySceneOpen()
        {
            if (SceneManager.GetActiveScene().path == GameplayScenePath) return;
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }

        private static void EnableOnlyDebugHitPad(string activePadName)
        {
            EnsureGameplaySceneOpen();
            SetupDebugHitPads();

            Transform root = FindGameObject("DebugHitPads")?.transform;
            if (root == null) return;

            root.gameObject.SetActive(true);
            EditorUtility.SetDirty(root.gameObject);

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                child.gameObject.SetActive(child.name == activePadName);
                EditorUtility.SetDirty(child.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"[AO] Enabled only {activePadName}.");
        }

        private static void SetAllDebugHitPadsActive(bool active)
        {
            Transform root = FindGameObject("DebugHitPads")?.transform;
            if (root == null) return;

            root.gameObject.SetActive(active);
            EditorUtility.SetDirty(root.gameObject);

            for (int i = 0; i < root.childCount; i++)
            {
                GameObject child = root.GetChild(i).gameObject;
                child.SetActive(active);
                EditorUtility.SetDirty(child);
            }
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

        private static T FindSceneObject<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindFirstObjectByType<T>();
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogWarning($"[AO] Missing asset: {path}");
            return asset;
        }

        private static void EnsureTag(string tag)
        {
            if (!InternalEditorUtility.tags.Contains(tag))
            {
                InternalEditorUtility.AddTag(tag);
            }
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = GetSerializedProperty(target, propertyName);
            if (property == null) return;

            property.objectReferenceValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(UnityEngine.Object target, string propertyName, float value)
        {
            SerializedProperty property = GetSerializedProperty(target, propertyName);
            if (property == null) return;

            property.floatValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(UnityEngine.Object target, string propertyName, int value)
        {
            SerializedProperty property = GetSerializedProperty(target, propertyName);
            if (property == null) return;

            property.intValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(UnityEngine.Object target, string propertyName, bool value)
        {
            SerializedProperty property = GetSerializedProperty(target, propertyName);
            if (property == null) return;

            property.boolValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector3(UnityEngine.Object target, string propertyName, Vector3 value)
        {
            SerializedProperty property = GetSerializedProperty(target, propertyName);
            if (property == null) return;

            property.vector3Value = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetColor(UnityEngine.Object target, string propertyName, Color value)
        {
            SerializedProperty property = GetSerializedProperty(target, propertyName);
            if (property == null) return;

            property.colorValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnumByName(UnityEngine.Object target, string propertyName, string enumName)
        {
            SerializedProperty property = GetSerializedProperty(target, propertyName);
            if (property == null) return;

            int index = Array.IndexOf(property.enumNames, enumName);
            if (index >= 0)
            {
                property.enumValueIndex = index;
                property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static SerializedProperty GetSerializedProperty(UnityEngine.Object target, string propertyName)
        {
            if (target == null) return null;
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"[AO] Serialized property not found: {target.GetType().Name}.{propertyName}");
            }
            return property;
        }
    }
}
