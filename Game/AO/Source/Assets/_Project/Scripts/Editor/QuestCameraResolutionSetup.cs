using System.IO;
using AO.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class QuestCameraResolutionSetup
    {
        private const string QualityUrpConfigPath = "Assets/Settings/Project Configuration/Quality URP Config.asset";
        private const string PerformanceUrpConfigPath = "Assets/Settings/Project Configuration/Performance URP Config.asset";
        private const int Quest3PerEyeWidth = 2064;
        private const int Quest3PerEyeHeight = 2208;
        private const int Quest3StereoPreviewWidth = Quest3PerEyeWidth * 2;
        private const int Quest3StereoPreviewHeight = Quest3PerEyeHeight;

        [MenuItem("AO/Setup/Apply Quest-like Game View Camera Preview")]
        public static void ApplyQuestLikeGameViewCameraPreview()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ApplyInternal();
        }

        public static void ApplyFromCommandLine()
        {
            ApplyInternal();
        }

        private static void ApplyInternal()
        {
            AssetDatabase.Refresh();

            int urpConfigured = ConfigureUrpRenderScale();
            int sceneCount = ConfigureBuildSceneCameras();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[AO] Quest-like Game View camera preview settings applied.\n" +
                "Oculus/XR target settings were intentionally left unchanged.\n" +
                $"URP assets render scale set to 1.0: {urpConfigured}\n" +
                $"Build scene Main Cameras configured/cleaned: {sceneCount}\n" +
                $"Game View reference sizes: {Quest3PerEyeWidth}x{Quest3PerEyeHeight} per eye, " +
                $"{Quest3StereoPreviewWidth}x{Quest3StereoPreviewHeight} stereo-wide.");
        }

        private static int ConfigureUrpRenderScale()
        {
            int changed = 0;
            if (SetRenderScale(QualityUrpConfigPath)) changed++;
            if (SetRenderScale(PerformanceUrpConfigPath)) changed++;
            return changed;
        }

        private static bool SetRenderScale(string assetPath)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset == null)
            {
                Debug.LogWarning($"[AO] URP asset not found: {assetPath}");
                return false;
            }

            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty renderScale = serialized.FindProperty("m_RenderScale");
            if (renderScale == null)
            {
                Debug.LogWarning($"[AO] URP asset has no m_RenderScale: {assetPath}");
                return false;
            }

            renderScale.floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return true;
        }

        private static int ConfigureBuildSceneCameras()
        {
            int changed = 0;
            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            {
                if (!buildScene.enabled || string.IsNullOrEmpty(buildScene.path) || !File.Exists(buildScene.path))
                {
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                Camera camera = FindMainCamera(scene);
                if (camera == null)
                {
                    Debug.LogWarning($"[AO] Build scene skipped because Main Camera was not found: {buildScene.path}");
                    continue;
                }

                camera.targetTexture = null;
                camera.targetDisplay = 0;
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                camera.allowDynamicResolution = false;
                camera.stereoTargetEye = StereoTargetEyeMask.Both;
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(camera.gameObject);

                UniversalAdditionalCameraData additional = camera.GetComponent<UniversalAdditionalCameraData>();
                if (additional == null)
                {
                    additional = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                }

                SetAdditionalBool(additional, "m_AllowXRRendering", true);
                SetAdditionalBool(additional, "m_UseScreenCoordOverride", false);

                QuestResolutionRuntimeSettings runtime = camera.GetComponent<QuestResolutionRuntimeSettings>();
                if (runtime != null)
                {
                    Object.DestroyImmediate(runtime, true);
                }

                EditorUtility.SetDirty(camera);
                EditorUtility.SetDirty(additional);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                changed++;
            }

            return changed;
        }

        private static Camera FindMainCamera(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Camera camera = FindMainCameraInChildren(root.transform);
                if (camera != null) return camera;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Camera camera = root.GetComponentInChildren<Camera>(true);
                if (camera != null) return camera;
            }

            return null;
        }

        private static Camera FindMainCameraInChildren(Transform root)
        {
            Camera camera = root.GetComponent<Camera>();
            if (camera != null && root.CompareTag("MainCamera")) return camera;

            for (int i = 0; i < root.childCount; i++)
            {
                Camera child = FindMainCameraInChildren(root.GetChild(i));
                if (child != null) return child;
            }

            return null;
        }

        private static void SetAdditionalBool(UniversalAdditionalCameraData additional, string propertyPath, bool value)
        {
            SerializedObject serialized = new SerializedObject(additional);
            SetBool(serialized, propertyPath, value);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(SerializedObject serialized, string propertyPath, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyPath);
            if (property != null) property.boolValue = value;
        }

    }
}
