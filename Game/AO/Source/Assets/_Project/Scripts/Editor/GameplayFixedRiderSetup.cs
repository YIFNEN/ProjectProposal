using System.IO;
using AO.Character;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class GameplayFixedRiderSetup
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";

        [MenuItem("AO/Character/Apply Fixed MANTA Rider Runtime")]
        public static void ApplyFixedMantaRiderRuntime()
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
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            DVariantRiderRig rig = Object.FindFirstObjectByType<DVariantRiderRig>(FindObjectsInactive.Include);
            if (rig == null || rig.gameObject.scene != scene)
            {
                Debug.LogError("[AO] DVariantRiderRig was not found in GamePlayScene.");
                return;
            }

            SetBool(rig, "_followHmd", false);
            SetBool(rig, "_snapOnEnable", false);
            SetBool(rig, "_yawFollowsHmd", false);
            SetBool(rig, "_driveHitAnchorToRig", false);
            SetBool(rig, "_driveHandTargetsFromControllers", true);
            SetBool(rig, "_readControllerPoseDirectlyFromXRNodes", true);
            SetBool(rig, "_enableKeyboardMouseDebugInput", true);
            EditorUtility.SetDirty(rig);

            foreach (MantaRideMotion rideMotion in Object.FindObjectsByType<MantaRideMotion>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (rideMotion == null || rideMotion.gameObject.scene != scene) continue;
                SetBool(rideMotion, "_playOnAwake", false);
                EditorUtility.SetDirty(rideMotion);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AO] Fixed MANTA rider runtime applied. Serialized scene values were updated; they remain editable in the Inspector.");
        }

        private static void SetBool(Object target, string propertyPath, bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyPath);
            if (property != null) property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
