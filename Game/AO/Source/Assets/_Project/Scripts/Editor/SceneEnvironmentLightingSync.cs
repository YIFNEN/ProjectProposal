using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class SceneEnvironmentLightingSync
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";

        private static readonly string[] TargetScenePaths =
        {
            "Assets/_Project/Scenes/01_Title.unity",
            "Assets/_Project/Scenes/02_Lobby.unity",
            "Assets/_Project/Scenes/04_Result.unity"
        };

        [MenuItem("AO/Setup/Sync Gameplay Environment Lighting To Menu Scenes")]
        public static void SyncFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SyncAll();
        }

        public static void SyncFromCommandLine()
        {
            SyncAll();
        }

        private static void SyncAll()
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            EnvironmentSnapshot snapshot = EnvironmentSnapshot.Capture();

            foreach (string scenePath in TargetScenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Light targetSun = FindDirectionalLight(scene);
                snapshot.Apply(targetSun);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[AO] Synced Gameplay Environment Lighting to {scenePath}; Sun Source={(targetSun != null ? targetSun.name : "None")}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static Light FindDirectionalLight(Scene scene)
        {
            Light fallback = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.type != LightType.Directional) continue;
                    if (light.name == "Directional Light") return light;
                    if (fallback == null) fallback = light;
                }
            }

            return fallback;
        }

        private sealed class EnvironmentSnapshot
        {
            private bool _fog;
            private Color _fogColor;
            private FogMode _fogMode;
            private float _fogDensity;
            private float _fogStartDistance;
            private float _fogEndDistance;
            private Color _ambientSkyColor;
            private Color _ambientEquatorColor;
            private Color _ambientGroundColor;
            private float _ambientIntensity;
            private AmbientMode _ambientMode;
            private Color _subtractiveShadowColor;
            private Material _skybox;
            private float _haloStrength;
            private float _flareStrength;
            private float _flareFadeSpeed;
            private DefaultReflectionMode _defaultReflectionMode;
            private int _defaultReflectionResolution;
            private int _reflectionBounces;
            private float _reflectionIntensity;
            private Cubemap _customReflection;

            public static EnvironmentSnapshot Capture()
            {
                return new EnvironmentSnapshot
                {
                    _fog = RenderSettings.fog,
                    _fogColor = RenderSettings.fogColor,
                    _fogMode = RenderSettings.fogMode,
                    _fogDensity = RenderSettings.fogDensity,
                    _fogStartDistance = RenderSettings.fogStartDistance,
                    _fogEndDistance = RenderSettings.fogEndDistance,
                    _ambientSkyColor = RenderSettings.ambientSkyColor,
                    _ambientEquatorColor = RenderSettings.ambientEquatorColor,
                    _ambientGroundColor = RenderSettings.ambientGroundColor,
                    _ambientIntensity = RenderSettings.ambientIntensity,
                    _ambientMode = RenderSettings.ambientMode,
                    _subtractiveShadowColor = RenderSettings.subtractiveShadowColor,
                    _skybox = RenderSettings.skybox,
                    _haloStrength = RenderSettings.haloStrength,
                    _flareStrength = RenderSettings.flareStrength,
                    _flareFadeSpeed = RenderSettings.flareFadeSpeed,
                    _defaultReflectionMode = RenderSettings.defaultReflectionMode,
                    _defaultReflectionResolution = RenderSettings.defaultReflectionResolution,
                    _reflectionBounces = RenderSettings.reflectionBounces,
                    _reflectionIntensity = RenderSettings.reflectionIntensity,
                    _customReflection = RenderSettings.customReflectionTexture as Cubemap
                };
            }

            public void Apply(Light sun)
            {
                RenderSettings.fog = _fog;
                RenderSettings.fogColor = _fogColor;
                RenderSettings.fogMode = _fogMode;
                RenderSettings.fogDensity = _fogDensity;
                RenderSettings.fogStartDistance = _fogStartDistance;
                RenderSettings.fogEndDistance = _fogEndDistance;
                RenderSettings.ambientSkyColor = _ambientSkyColor;
                RenderSettings.ambientEquatorColor = _ambientEquatorColor;
                RenderSettings.ambientGroundColor = _ambientGroundColor;
                RenderSettings.ambientIntensity = _ambientIntensity;
                RenderSettings.ambientMode = _ambientMode;
                RenderSettings.subtractiveShadowColor = _subtractiveShadowColor;
                RenderSettings.skybox = _skybox;
                RenderSettings.haloStrength = _haloStrength;
                RenderSettings.flareStrength = _flareStrength;
                RenderSettings.flareFadeSpeed = _flareFadeSpeed;
                RenderSettings.defaultReflectionMode = _defaultReflectionMode;
                RenderSettings.defaultReflectionResolution = _defaultReflectionResolution;
                RenderSettings.reflectionBounces = _reflectionBounces;
                RenderSettings.reflectionIntensity = _reflectionIntensity;
                RenderSettings.customReflectionTexture = _customReflection;
                RenderSettings.sun = sun;
            }
        }
    }
}
