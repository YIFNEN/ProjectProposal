using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class SceneCameraSettingsSync
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";

        private static readonly string[] TargetScenePaths =
        {
            "Assets/_Project/Scenes/01_Title.unity",
            "Assets/_Project/Scenes/02_Lobby.unity",
            "Assets/_Project/Scenes/04_Result.unity"
        };

        [MenuItem("AO/Setup/Sync Menu Cameras From Gameplay")]
        public static void SyncMenuCamerasFromGameplay()
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
            if (!File.Exists(GameplayScenePath))
            {
                Debug.LogError($"[AO] Gameplay scene not found: {GameplayScenePath}");
                return;
            }

            AssetDatabase.Refresh();
            Scene gameplayScene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            Camera sourceCamera = FindMainCamera(gameplayScene);
            if (sourceCamera == null)
            {
                Debug.LogError("[AO] Main Camera not found in GamePlayScene.");
                return;
            }

            CameraSnapshot cameraSnapshot = CameraSnapshot.Capture(sourceCamera);
            UniversalAdditionalCameraData sourceAdditionalData = sourceCamera.GetComponent<UniversalAdditionalCameraData>();
            AdditionalCameraDataSnapshot additionalSnapshot = AdditionalCameraDataSnapshot.Capture(sourceAdditionalData);

            int changedCount = 0;
            foreach (string scenePath in TargetScenePaths)
            {
                if (!File.Exists(scenePath))
                {
                    Debug.LogWarning($"[AO] Target scene skipped because it does not exist: {scenePath}");
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Camera targetCamera = FindMainCamera(scene);
                if (targetCamera == null)
                {
                    Debug.LogWarning($"[AO] Target scene has no Main Camera: {scenePath}");
                    continue;
                }

                cameraSnapshot.ApplyTo(targetCamera);

                UniversalAdditionalCameraData targetAdditionalData = targetCamera.GetComponent<UniversalAdditionalCameraData>();
                if (targetAdditionalData == null)
                {
                    targetAdditionalData = targetCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                }

                additionalSnapshot.ApplyTo(targetAdditionalData);
                EditorUtility.SetDirty(targetCamera);
                EditorUtility.SetDirty(targetAdditionalData);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                changedCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AO] Synced Gameplay camera render settings to {changedCount} menu/result scenes. Camera transforms were preserved.");
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
                Camera childCamera = FindMainCameraInChildren(root.GetChild(i));
                if (childCamera != null) return childCamera;
            }

            return null;
        }

        private readonly struct CameraSnapshot
        {
            private readonly CameraClearFlags _clearFlags;
            private readonly Color _backgroundColor;
            private readonly float _nearClipPlane;
            private readonly float _farClipPlane;
            private readonly float _fieldOfView;
            private readonly bool _orthographic;
            private readonly float _orthographicSize;
            private readonly int _cullingMask;
            private readonly RenderingPath _renderingPath;
            private readonly int _targetDisplay;
            private readonly StereoTargetEyeMask _stereoTargetEye;
            private readonly bool _allowHdr;
            private readonly bool _allowMsaa;
            private readonly bool _allowDynamicResolution;
            private readonly bool _forceIntoRenderTexture;
            private readonly bool _useOcclusionCulling;
            private readonly float _stereoConvergence;
            private readonly float _stereoSeparation;
            private readonly Rect _rect;
            private readonly Vector2 _sensorSize;
            private readonly Vector2 _lensShift;
            private readonly Camera.GateFitMode _gateFit;
            private readonly float _focalLength;
            private readonly int _iso;
            private readonly float _shutterSpeed;
            private readonly float _aperture;
            private readonly float _focusDistance;

            private CameraSnapshot(Camera camera)
            {
                _clearFlags = camera.clearFlags;
                _backgroundColor = camera.backgroundColor;
                _nearClipPlane = camera.nearClipPlane;
                _farClipPlane = camera.farClipPlane;
                _fieldOfView = camera.fieldOfView;
                _orthographic = camera.orthographic;
                _orthographicSize = camera.orthographicSize;
                _cullingMask = camera.cullingMask;
                _renderingPath = camera.renderingPath;
                _targetDisplay = camera.targetDisplay;
                _stereoTargetEye = camera.stereoTargetEye;
                _allowHdr = camera.allowHDR;
                _allowMsaa = camera.allowMSAA;
                _allowDynamicResolution = camera.allowDynamicResolution;
                _forceIntoRenderTexture = camera.forceIntoRenderTexture;
                _useOcclusionCulling = camera.useOcclusionCulling;
                _stereoConvergence = camera.stereoConvergence;
                _stereoSeparation = camera.stereoSeparation;
                _rect = camera.rect;
                _sensorSize = camera.sensorSize;
                _lensShift = camera.lensShift;
                _gateFit = camera.gateFit;
                _focalLength = camera.focalLength;
                _iso = camera.iso;
                _shutterSpeed = camera.shutterSpeed;
                _aperture = camera.aperture;
                _focusDistance = camera.focusDistance;
            }

            public static CameraSnapshot Capture(Camera camera)
            {
                return new CameraSnapshot(camera);
            }

            public void ApplyTo(Camera camera)
            {
                camera.clearFlags = _clearFlags;
                camera.backgroundColor = _backgroundColor;
                camera.nearClipPlane = _nearClipPlane;
                camera.farClipPlane = _farClipPlane;
                camera.fieldOfView = _fieldOfView;
                camera.orthographic = _orthographic;
                camera.orthographicSize = _orthographicSize;
                camera.cullingMask = _cullingMask;
                camera.renderingPath = _renderingPath;
                camera.targetDisplay = _targetDisplay;
                camera.stereoTargetEye = _stereoTargetEye;
                camera.allowHDR = _allowHdr;
                camera.allowMSAA = _allowMsaa;
                camera.allowDynamicResolution = _allowDynamicResolution;
                camera.forceIntoRenderTexture = _forceIntoRenderTexture;
                camera.useOcclusionCulling = _useOcclusionCulling;
                camera.stereoConvergence = _stereoConvergence;
                camera.stereoSeparation = _stereoSeparation;
                camera.rect = _rect;
                camera.sensorSize = _sensorSize;
                camera.lensShift = _lensShift;
                camera.gateFit = _gateFit;
                camera.focalLength = _focalLength;
                camera.iso = _iso;
                camera.shutterSpeed = _shutterSpeed;
                camera.aperture = _aperture;
                camera.focusDistance = _focusDistance;
            }
        }

        private sealed class AdditionalCameraDataSnapshot
        {
            private readonly UniversalAdditionalCameraData _source;

            private AdditionalCameraDataSnapshot(UniversalAdditionalCameraData source)
            {
                _source = source;
            }

            public static AdditionalCameraDataSnapshot Capture(UniversalAdditionalCameraData source)
            {
                return new AdditionalCameraDataSnapshot(source);
            }

            public void ApplyTo(UniversalAdditionalCameraData target)
            {
                if (_source == null || target == null) return;

                SerializedObject sourceObject = new SerializedObject(_source);
                SerializedObject targetObject = new SerializedObject(target);

                CopyProperty(sourceObject, targetObject, "m_RenderShadows");
                CopyProperty(sourceObject, targetObject, "m_RequiresDepthTextureOption");
                CopyProperty(sourceObject, targetObject, "m_RequiresOpaqueTextureOption");
                CopyProperty(sourceObject, targetObject, "m_CameraType");
                CopyProperty(sourceObject, targetObject, "m_RendererIndex");
                CopyProperty(sourceObject, targetObject, "m_VolumeLayerMask");
                CopyProperty(sourceObject, targetObject, "m_VolumeFrameworkUpdateModeOption");
                CopyProperty(sourceObject, targetObject, "m_RenderPostProcessing");
                CopyProperty(sourceObject, targetObject, "m_Antialiasing");
                CopyProperty(sourceObject, targetObject, "m_AntialiasingQuality");
                CopyProperty(sourceObject, targetObject, "m_StopNaN");
                CopyProperty(sourceObject, targetObject, "m_Dithering");
                CopyProperty(sourceObject, targetObject, "m_ClearDepth");
                CopyProperty(sourceObject, targetObject, "m_AllowXRRendering");
                CopyProperty(sourceObject, targetObject, "m_AllowHDROutput");
                CopyProperty(sourceObject, targetObject, "m_UseScreenCoordOverride");
                CopyProperty(sourceObject, targetObject, "m_ScreenSizeOverride");
                CopyProperty(sourceObject, targetObject, "m_ScreenCoordScaleBias");
                CopyProperty(sourceObject, targetObject, "m_RequiresDepthTexture");
                CopyProperty(sourceObject, targetObject, "m_RequiresColorTexture");
                CopyProperty(sourceObject, targetObject, "m_TaaSettings.m_Quality");
                CopyProperty(sourceObject, targetObject, "m_TaaSettings.m_FrameInfluence");
                CopyProperty(sourceObject, targetObject, "m_TaaSettings.m_JitterScale");
                CopyProperty(sourceObject, targetObject, "m_TaaSettings.m_MipBias");
                CopyProperty(sourceObject, targetObject, "m_TaaSettings.m_VarianceClampScale");
                CopyProperty(sourceObject, targetObject, "m_TaaSettings.m_ContrastAdaptiveSharpening");

                targetObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void CopyProperty(SerializedObject sourceObject, SerializedObject targetObject, string propertyPath)
        {
            SerializedProperty sourceProperty = sourceObject.FindProperty(propertyPath);
            SerializedProperty targetProperty = targetObject.FindProperty(propertyPath);
            if (sourceProperty == null || targetProperty == null) return;

            switch (sourceProperty.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Enum:
                    targetProperty.intValue = sourceProperty.intValue;
                    break;
                case SerializedPropertyType.Boolean:
                    targetProperty.boolValue = sourceProperty.boolValue;
                    break;
                case SerializedPropertyType.Float:
                    targetProperty.floatValue = sourceProperty.floatValue;
                    break;
                case SerializedPropertyType.Color:
                    targetProperty.colorValue = sourceProperty.colorValue;
                    break;
                case SerializedPropertyType.ObjectReference:
                    targetProperty.objectReferenceValue = sourceProperty.objectReferenceValue;
                    break;
                case SerializedPropertyType.Vector2:
                    targetProperty.vector2Value = sourceProperty.vector2Value;
                    break;
                case SerializedPropertyType.Vector3:
                    targetProperty.vector3Value = sourceProperty.vector3Value;
                    break;
                case SerializedPropertyType.Vector4:
                    targetProperty.vector4Value = sourceProperty.vector4Value;
                    break;
                case SerializedPropertyType.Rect:
                    targetProperty.rectValue = sourceProperty.rectValue;
                    break;
                case SerializedPropertyType.Bounds:
                    targetProperty.boundsValue = sourceProperty.boundsValue;
                    break;
                case SerializedPropertyType.Quaternion:
                    targetProperty.quaternionValue = sourceProperty.quaternionValue;
                    break;
            }
        }
    }
}
