using AO.Cameras;
using AO.Character;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class RenderCameraTiltSetup
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const string RawHmdReferenceName = "RawHmdReference";
        private static readonly Vector3 DefaultRenderTiltEuler = new Vector3(4.5f, 0f, 0f);
        private static readonly Vector3 DefaultRenderPositionOffsetLocal = new Vector3(0f, 0.12f, 0f);

        [MenuItem("AO/Setup/Apply Render Camera Tilt")]
        public static void ApplyMenu()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = OpenGameplayScene();
            if (!scene.IsValid()) return;

            Transform rawHmd = EnsureRenderCameraTiltInCurrentScene();
            if (rawHmd == null) return;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AO] Render camera correction applied. Main Camera renders with +4.5 deg local X and +0.12m local Y offset, while DVariantRiderRig/OculusUpperBodyDriver use RawHmdReference.");
        }

        public static void ApplyFromCommandLine()
        {
            Scene scene = OpenGameplayScene();
            if (!scene.IsValid()) return;

            Transform rawHmd = EnsureRenderCameraTiltInCurrentScene();
            if (rawHmd == null) return;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        internal static Transform EnsureRenderCameraTiltInCurrentScene()
        {
            Camera mainCamera = FindMainCamera(SceneManager.GetActiveScene());
            if (mainCamera == null)
            {
                Debug.LogError("[AO] Main Camera not found. Cannot apply render camera tilt.");
                return null;
            }

            Transform cameraOffset = mainCamera.transform.parent;
            if (cameraOffset == null)
            {
                Debug.LogError("[AO] Main Camera has no parent Camera Offset. Cannot apply render camera tilt safely.", mainCamera);
                return null;
            }

            Transform rawHmd = EnsureRawHmdReference(cameraOffset, mainCamera.transform);
            ConfigureRenderTiltRig(mainCamera.transform, rawHmd);
            RebindGameplayHmdReferences(rawHmd);
            return rawHmd;
        }

        private static Scene OpenGameplayScene()
        {
            if (!File.Exists(GameplayScenePath))
            {
                Debug.LogError($"[AO] Gameplay scene not found: {GameplayScenePath}");
                return default;
            }

            AssetDatabase.Refresh();
            return EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }

        private static Transform EnsureRawHmdReference(Transform cameraOffset, Transform mainCamera)
        {
            Transform raw = cameraOffset.Find(RawHmdReferenceName);
            if (raw == null)
            {
                GameObject go = new GameObject(RawHmdReferenceName);
                Undo.RegisterCreatedObjectUndo(go, "Create RawHmdReference");
                raw = go.transform;
                raw.SetParent(cameraOffset, false);
            }

            raw.localPosition = mainCamera.localPosition;
            raw.localRotation = Quaternion.identity;
            raw.localScale = Vector3.one;
            EditorUtility.SetDirty(raw.gameObject);
            return raw;
        }

        private static void ConfigureRenderTiltRig(Transform mainCamera, Transform rawHmd)
        {
            RenderCameraTiltRig rig = mainCamera.GetComponent<RenderCameraTiltRig>();
            if (rig == null) rig = Undo.AddComponent<RenderCameraTiltRig>(mainCamera.gameObject);

            SetObject(rig, "_renderCamera", mainCamera);
            SetObject(rig, "_rawHmdReference", rawHmd);
            SetVector3(rig, "_renderRotationOffsetEuler", DefaultRenderTiltEuler);
            SetVector3(rig, "_renderPositionOffsetLocal", DefaultRenderPositionOffsetLocal);
            SetBool(rig, "_enableRenderRotationOffset", true);
            SetBool(rig, "_enableRenderPositionOffset", true);
            SetBool(rig, "_driveRawHmdReference", true);
            SetBool(rig, "_driveRenderCamera", true);
            SetBool(rig, "_applyPosition", true);
            SetBool(rig, "_applyRotation", true);
            SetBool(rig, "_applyBeforeRender", true);
            SetBool(rig, "_disableTrackedPoseDriversOnRenderCamera", true);
            EditorUtility.SetDirty(rig);
        }

        private static void RebindGameplayHmdReferences(Transform rawHmd)
        {
            foreach (DVariantRiderRig riderRig in Object.FindObjectsByType<DVariantRiderRig>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!riderRig.gameObject.scene.isLoaded) continue;
                SetObject(riderRig, "_hmd", rawHmd);
                EditorUtility.SetDirty(riderRig);
            }

            foreach (OculusUpperBodyDriver driver in Object.FindObjectsByType<OculusUpperBodyDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!driver.gameObject.scene.isLoaded) continue;
                SetObject(driver, "_hmd", rawHmd);
                EditorUtility.SetDirty(driver);
            }
        }

        private static Camera FindMainCamera(Scene scene)
        {
            if (Camera.main != null && Camera.main.gameObject.scene == scene) return Camera.main;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Camera camera = FindMainCameraInChildren(root.transform);
                if (camera != null) return camera;
            }

            return Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
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

        private static void SetObject(Object target, string propertyName, Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = value;
            serializedObject.ApplyModifiedProperties();
        }

        private static void SetBool(Object target, string propertyName, bool value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) property.boolValue = value;
            serializedObject.ApplyModifiedProperties();
        }

        private static void SetVector3(Object target, string propertyName, Vector3 value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) property.vector3Value = value;
            serializedObject.ApplyModifiedProperties();
        }
    }
}
