using AO.Character;
using AO.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class GameplayBodyCalibrationSetup
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const string CalibrationObjectName = "GameplayBodyCalibration";

        [MenuItem("AO/Character/Apply Gameplay Body Calibration")]
        public static void ApplyToGameplayScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            ApplyToScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AO] Gameplay body calibration applied. GameStateManager will run body/hand-range tuning before starting the song.");
        }

        public static void ApplyToGameplaySceneFromCommandLine()
        {
            ApplyToGameplayScene();
        }

        public static bool ApplyToScene(Scene scene)
        {
            DVariantRiderRig riderRig = FindSceneComponent<DVariantRiderRig>(scene);
            GameStateManager gameState = FindSceneComponent<GameStateManager>(scene);
            OculusUpperBodyDriver upperBody = FindSceneComponent<OculusUpperBodyDriver>(scene);
            Transform hmd = Camera.main != null && Camera.main.gameObject.scene == scene ? Camera.main.transform : FindMainCamera(scene);

            if (riderRig == null)
            {
                Debug.LogWarning("[AO] DVariantRiderRig was not found. Body calibration object was not configured.");
                return false;
            }

            GameObject calibrationObject = FindRootObject(scene, CalibrationObjectName);
            if (calibrationObject == null)
            {
                calibrationObject = new GameObject(CalibrationObjectName);
                SceneManager.MoveGameObjectToScene(calibrationObject, scene);
            }

            GameplayBodyCalibrationController calibration = calibrationObject.GetComponent<GameplayBodyCalibrationController>();
            if (calibration == null) calibration = calibrationObject.AddComponent<GameplayBodyCalibrationController>();

            SetObject(calibration, "_riderRig", riderRig);
            SetObject(calibration, "_upperBodyDriver", upperBody);
            SetObject(calibration, "_hmd", hmd);
            SetBool(calibration, "_enabledBeforeSong", true);
            SetBool(calibration, "_runEveryGameplayStart", true);
            SetBool(calibration, "_allowSavedProfileSkip", true);
            SetBool(calibration, "_loadSavedProfile", true);
            SetBool(calibration, "_saveProfile", true);
            SetVector3(calibration, "_workspacePositiveLocal", riderRig.BaseHandWorkspacePositiveLocal);
            SetVector3(calibration, "_workspaceNegativeLocal", riderRig.BaseHandWorkspaceNegativeLocal);
            EditorUtility.SetDirty(calibration);

            if (gameState != null)
            {
                SetObject(gameState, "_bodyCalibrationController", calibration);
                SetBool(gameState, "_calibrateBodyBeforeSong", true);
                EditorUtility.SetDirty(gameState);
            }
            else
            {
                Debug.LogWarning("[AO] GameStateManager was not found. Body calibration was created but not linked to song startup.");
            }

            return true;
        }

        private static GameObject FindRootObject(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == objectName) return root;
            }

            return null;
        }

        private static Transform FindMainCamera(Scene scene)
        {
            foreach (Camera camera in Resources.FindObjectsOfTypeAll<Camera>())
            {
                if (camera == null || camera.gameObject.scene != scene) continue;
                if (camera.CompareTag("MainCamera")) return camera.transform;
            }

            return null;
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            foreach (T component in Resources.FindObjectsOfTypeAll<T>())
            {
                if (component == null || component.gameObject.scene != scene) continue;
                return component;
            }

            return null;
        }

        private static void SetObject(Object target, string propertyName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string propertyName, bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null) property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector3(Object target, string propertyName, Vector3 value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null) property.vector3Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
