using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class GameplayCharacterLightingSetup
    {
        private static readonly string[] MainScenePaths =
        {
            "Assets/_Project/Scenes/01_Title.unity",
            "Assets/_Project/Scenes/02_Lobby.unity",
            "Assets/_Project/Scenes/GamePlayScene.unity",
            "Assets/_Project/Scenes/04_Result.unity"
        };

        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const string RootName = "AO_GameplayCharacterLightingRoot";

        [MenuItem("AO/Setup/Apply Gameplay Character Correction Lights")]
        public static void ApplyGameplayCharacterCorrectionLights()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ApplyScenePath(GameplayScenePath);
        }

        [MenuItem("AO/Setup/Apply Character Color Lights/Open Scene")]
        public static void ApplyOpenSceneCharacterColorLights()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = SceneManager.GetActiveScene();
            ApplyToActiveScene(scene, saveScene: true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("AO/Setup/Apply Character Color Lights/All Main Scenes")]
        public static void ApplyAllMainSceneCharacterColorLights()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ApplyAllMainScenesInternal();
        }

        public static void ApplyFromCommandLine()
        {
            ApplyScenePath(GameplayScenePath);
        }

        public static void ApplyAllMainScenesFromCommandLine()
        {
            ApplyAllMainScenesInternal();
        }

        private static void ApplyAllMainScenesInternal()
        {
            foreach (string scenePath in MainScenePaths)
            {
                ApplyScenePath(scenePath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ApplyScenePath(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogError($"[AO] Scene not found: {scenePath}");
                return;
            }

            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            ApplyToActiveScene(scene, saveScene: true);
        }

        private static void ApplyToActiveScene(Scene scene, bool saveScene)
        {
            Transform anchor = FindCharacterLightingAnchor();
            LightingFrame frame = BuildLightingFrame(anchor);

            Transform root = FindSceneTransform(RootName);
            if (root == null) root = new GameObject(RootName).transform;

            root.SetParent(null, false);
            root.position = frame.Origin;
            root.rotation = frame.Rotation;
            root.localScale = Vector3.one;

            ConfigureSpotLocal(
                root,
                "PearlWhite_Fill_Light",
                new Vector3(0.24f, 0.22f, -0.62f),
                new Vector3(0f, 0.06f, 0f),
                new Color(1f, 0.965f, 0.9f, 1f),
                0.55f,
                1.85f,
                70f,
                frame.Scale);

            ConfigureSpotLocal(
                root,
                "LavenderPink_Rim_Light",
                new Vector3(0.58f, 0.42f, 0.78f),
                new Vector3(0f, 0.1f, 0f),
                new Color(0.86f, 0.74f, 1f, 1f),
                0.9f,
                2.05f,
                68f,
                frame.Scale);

            ConfigureSpotLocal(
                root,
                "NeutralCool_Key_Light",
                new Vector3(-0.22f, 0.58f, -0.72f),
                new Vector3(0f, 0.08f, 0f),
                new Color(0.9f, 0.96f, 1f, 1f),
                1.05f,
                2.15f,
                62f,
                frame.Scale);

            ConfigureSpotLocal(
                root,
                "WarmPearl_Accent_Light",
                new Vector3(-0.46f, 0.08f, -0.36f),
                new Vector3(0f, -0.04f, 0f),
                new Color(1f, 0.86f, 0.68f, 1f),
                0.28f,
                1.55f,
                58f,
                frame.Scale);

            EditorUtility.SetDirty(root.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene) EditorSceneManager.SaveScene(scene);

            string anchorName = anchor != null ? anchor.name : "fallback";
            Debug.Log($"[AO] Character color lights applied to {scene.path}. Anchor={anchorName}, worldOrigin={frame.Origin}, localScaleFactor={frame.Scale:0.00}. Lights use local offsets and no shadows.");
        }

        private static void ConfigureSpotLocal(Transform root, string name, Vector3 localPosition, Vector3 localLookAt, Color color, float intensity, float range, float spotAngle, float scale)
        {
            Transform child = root.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
            }

            child.SetParent(root, false);
            child.localPosition = localPosition * scale;

            Vector3 direction = (localLookAt * scale) - child.localPosition;
            if (direction.sqrMagnitude > 0.0001f)
            {
                child.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
            child.localScale = Vector3.one;

            Light light = child.GetComponent<Light>();
            if (light == null) light = child.gameObject.AddComponent<Light>();

            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = range * scale;
            light.spotAngle = spotAngle;
            light.innerSpotAngle = spotAngle * 0.62f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
            light.cullingMask = ~0;
            light.bounceIntensity = 0.05f;
            light.useColorTemperature = false;

            EditorUtility.SetDirty(child.gameObject);
            EditorUtility.SetDirty(light);
        }

        private static Transform FindCharacterLightingAnchor()
        {
            string[] preferredAnchors =
            {
                "VisualRig",
                "CharacterRoot",
                "PF_AO_Character_VRM10",
                "MantaRoot"
            };

            Transform fallback = null;
            foreach (string anchorName in preferredAnchors)
            {
                Transform candidate = FindSceneTransform(anchorName);
                if (candidate == null) continue;
                if (fallback == null) fallback = candidate;
                if (HasVisibleRenderer(candidate)) return candidate;
            }

            return fallback ?? FindSceneTransform("HitAnchor");
        }

        private static LightingFrame BuildLightingFrame(Transform anchor)
        {
            Bounds bounds;
            bool hasBounds = TryGetRendererBounds(anchor, out bounds);
            Vector3 origin = hasBounds
                ? bounds.center
                : (anchor != null ? anchor.position + Vector3.up * 1.05f : new Vector3(0f, 1.35f, 1f));

            float height = hasBounds ? Mathf.Clamp(bounds.size.y, 0.9f, 2.4f) : 1.6f;
            float scale = Mathf.Clamp(height / 1.65f, 0.72f, 1.35f);

            return new LightingFrame
            {
                Origin = origin,
                Rotation = BuildYawRotation(anchor),
                Scale = scale
            };
        }

        private static Quaternion BuildYawRotation(Transform anchor)
        {
            if (anchor == null) return Quaternion.identity;

            Vector3 forward = Vector3.ProjectOnPlane(anchor.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private static bool HasVisibleRenderer(Transform root)
        {
            if (root == null) return false;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null && renderer.enabled) return true;
            }

            return false;
        }

        private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds(root != null ? root.position : Vector3.zero, Vector3.zero);
            if (root == null) return false;

            bool found = false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled) continue;

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private struct LightingFrame
        {
            public Vector3 Origin;
            public Quaternion Rotation;
            public float Scale;
        }

        private static Transform FindSceneTransform(string objectName)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform found = FindChildRecursive(root.transform, objectName);
                if (found != null) return found;
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform root, string objectName)
        {
            if (root.name == objectName) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), objectName);
                if (found != null) return found;
            }

            return null;
        }
    }
}
