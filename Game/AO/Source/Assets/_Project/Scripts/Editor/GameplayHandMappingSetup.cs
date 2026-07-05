using AO.Character;
using AO.Judgement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class GameplayHandMappingSetup
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";

        public const float HorizontalScale = 1f;
        public const float VerticalScale = 1.15f;
        public const float DepthScale = 1.05f;
        public static readonly Vector3 DebugWorkspacePositiveLocal = new Vector3(0.78f, 0.85f, 0.82f);
        public static readonly Vector3 DebugWorkspaceNegativeLocal = new Vector3(0.78f, 0.62f, 0.70f);

        [MenuItem("AO/Character/Apply Natural Gameplay Hand Mapping")]
        public static void ApplyToGameplayScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            ApplyToScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[AO] Natural gameplay hand mapping applied. DVariantRiderRig reads XR controller poses directly; hard hand workspace clamp is preserved for debug only and disabled by default.");
        }

        public static void ApplyToGameplaySceneFromCommandLine()
        {
            ApplyToGameplayScene();
        }

        public static bool ApplyToScene(Scene scene)
        {
            bool changed = false;

            DVariantRiderRig rig = FindSceneComponent<DVariantRiderRig>(scene);
            if (rig != null)
            {
                rig.enabled = true;
                SetBool(rig, "_driveHandTargetsFromControllers", true);
                SetBool(rig, "_readControllerPoseDirectlyFromXRNodes", true);
                SetBool(rig, "_enableKeyboardMouseDebugInput", true);
                SetFloat(rig, "_horizontalScale", HorizontalScale);
                SetFloat(rig, "_verticalScale", VerticalScale);
                SetFloat(rig, "_depthScale", DepthScale);
                SetBool(rig, "_useCommonPlayfieldHorizontalMapping", true);
                SetFloat(rig, "_minimumCommonPlayfieldControllerSpan", 0.12f);
                SetBool(rig, "_limitHandWorkspace", false);
                SetVector3(rig, "_handWorkspacePositiveLocal", DebugWorkspacePositiveLocal);
                SetVector3(rig, "_handWorkspaceNegativeLocal", DebugWorkspaceNegativeLocal);
                SetFloat(rig, "_handTargetSmoothSpeed", 48f);
                SetFloat(rig, "_visualHandTargetSmoothSpeed", 42f);
                rig.CaptureSceneAuthoredHandRest();
                EditorUtility.SetDirty(rig);
                changed = true;
            }
            else
            {
                Debug.LogWarning("[AO] DVariantRiderRig was not found while applying natural hand mapping.");
            }

            return changed;
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            foreach (T component in FindSceneComponents<T>(scene))
            {
                return component;
            }

            return null;
        }

        private static System.Collections.Generic.IEnumerable<T> FindSceneComponents<T>(Scene scene) where T : Component
        {
            foreach (T item in Resources.FindObjectsOfTypeAll<T>())
            {
                if (item == null || item.gameObject.scene != scene) continue;
                yield return item;
            }
        }

        private static void SetFloat(Object target, string propertyName, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null) property.floatValue = value;
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
