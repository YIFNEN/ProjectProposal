using AO.Character;
using AO.Judgement;
using AO.Rhythm;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class GameplayCurrentDecisionsSetup
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const float HandHorizontalScale = 1f;
        private const float HandVerticalScale = 1.15f;
        private const float HandDepthScale = 1.05f;
        private static readonly Vector3 DebugHandWorkspacePositiveLocal = new Vector3(0.78f, 0.85f, 0.82f);
        private static readonly Vector3 DebugHandWorkspaceNegativeLocal = new Vector3(0.78f, 0.62f, 0.70f);

        [MenuItem("AO/Setup/Apply Current Gameplay Decisions")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            int removedLaneObjects = RemoveLaneGuides(scene);
            int removedHandProxies = RemoveHitAnchorHandProxies(scene);
            bool disabledFeverHands = DisableMantaFeverHandEffect(scene);
            bool tunedHandMapping = ApplyNaturalHandMapping(scene);
            bool appliedBodyCalibration = GameplayBodyCalibrationSetup.ApplyToScene(scene);
            bool appliedArmNaturalMotion = DVariantArmNaturalMotionSetup.ApplyToScene(scene);
            DisableLegacyFeverHandEffects(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AO] Current gameplay decisions applied. Removed lane guide objects/components: {removedLaneObjects}. Removed legacy HitAnchor hand proxies: {removedHandProxies}. MantaRoot FeverHandEffect disabled: {disabledFeverHands}. Direct XR hand mapping tuned: {tunedHandMapping}. Body calibration applied: {appliedBodyCalibration}. Dynamic arm natural motion applied: {appliedArmNaturalMotion}.");
        }

        public static void ApplyFromCommandLine()
        {
            Apply();
        }

        private static int RemoveLaneGuides(Scene scene)
        {
            int removed = 0;

            foreach (LanePathGuide guide in FindSceneObjects<LanePathGuide>(scene))
            {
                if (guide == null) continue;

                Transform lineRoot = guide.transform.Find("LanePathLines");
                if (lineRoot != null)
                {
                    Object.DestroyImmediate(lineRoot.gameObject, true);
                    removed++;
                }

                Object.DestroyImmediate(guide, true);
                removed++;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                removed += DestroyNamedChildren(root.transform, "LanePathLines");
            }

            return removed;
        }

        private static bool ApplyNaturalHandMapping(Scene scene)
        {
            bool changed = false;

            DVariantRiderRig rig = FindFirstSceneObject<DVariantRiderRig>(scene);
            if (rig != null)
            {
                ApplyNaturalHandMapping(rig);
                rig.CaptureSceneAuthoredHandRest();
                changed = true;
            }
            else
            {
                Debug.LogWarning("[AO] DVariantRiderRig was not found while applying natural hand mapping.");
            }

            return changed;
        }

        private static bool DisableMantaFeverHandEffect(Scene scene)
        {
            DVariantRiderRig rig = FindFirstSceneObject<DVariantRiderRig>(scene);
            if (rig == null)
            {
                Debug.LogWarning("[AO] DVariantRiderRig was not found in GamePlayScene. MantaRoot FeverHandEffect was not disabled.");
                return false;
            }

            FeverHandEffect effect = rig.GetComponent<FeverHandEffect>();
            if (effect == null) return false;

            rig.enabled = true;
            EditorUtility.SetDirty(rig);

            effect.enabled = false;
            SetObject(effect, "_riderRig", rig);
            SetObject(effect, "_effectPrefab", null);
            SetBool(effect, "_spawnHandEffectOnFever", false);
            SetFloat(effect, "_spawnInterval", 0.18f);
            SetFloat(effect, "_effectLifetime", 1.6f);
            SetBool(effect, "_followVisualHandTargets", true);
            SetBool(effect, "_usePersistentTrails", true);
            SetFloat(effect, "_followSmoothSpeed", 18f);
            SetBool(effect, "_parentTrailToHand", false);
            EditorUtility.SetDirty(effect);

            ApplyNaturalHandMapping(rig);

            return true;
        }

        private static void ApplyNaturalHandMapping(DVariantRiderRig rig)
        {
            if (rig == null) return;

            rig.enabled = true;
            SetBool(rig, "_followHmd", false);
            SetBool(rig, "_snapOnEnable", false);
            SetBool(rig, "_yawFollowsHmd", false);
            SetBool(rig, "_driveHandTargetsFromControllers", true);
            SetBool(rig, "_readControllerPoseDirectlyFromXRNodes", true);
            SetBool(rig, "_enableKeyboardMouseDebugInput", true);
            SetBool(rig, "_driveHitAnchorToRig", false);
            SetFloat(rig, "_handTargetSmoothSpeed", 48f);
                SetFloat(rig, "_visualHandTargetSmoothSpeed", 42f);
            SetFloat(rig, "_horizontalScale", HandHorizontalScale);
            SetFloat(rig, "_verticalScale", HandVerticalScale);
            SetFloat(rig, "_depthScale", HandDepthScale);
            SetBool(rig, "_useCommonPlayfieldHorizontalMapping", true);
            SetFloat(rig, "_minimumCommonPlayfieldControllerSpan", 0.12f);
            SetBool(rig, "_limitHandWorkspace", false);
            SetVector3(rig, "_handWorkspacePositiveLocal", DebugHandWorkspacePositiveLocal);
            SetVector3(rig, "_handWorkspaceNegativeLocal", DebugHandWorkspaceNegativeLocal);
            EditorUtility.SetDirty(rig);
        }

        private static int RemoveHitAnchorHandProxies(Scene scene)
        {
            GameObject hitAnchor = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                hitAnchor = FindChildByName(root.transform, "HitAnchor")?.gameObject;
                if (hitAnchor != null) break;
            }

            if (hitAnchor == null) return 0;

            int removed = 0;
            removed += RemoveDirectChildIfLegacyProxy(hitAnchor.transform, "LeftHand");
            removed += RemoveDirectChildIfLegacyProxy(hitAnchor.transform, "RightHand");
            return removed;
        }

        private static int RemoveDirectChildIfLegacyProxy(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child == null) return 0;

            bool isLegacyProxy =
                child.GetComponent<XRHandPoseFollower>() != null ||
                child.GetComponent<HandTracker>() != null;
            if (!isLegacyProxy) return 0;

            Object.DestroyImmediate(child.gameObject, true);
            return 1;
        }

        private static Transform FindChildByName(Transform parent, string childName)
        {
            if (parent.name == childName) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildByName(parent.GetChild(i), childName);
                if (found != null) return found;
            }

            return null;
        }

        private static void DisableLegacyFeverHandEffects(Scene scene)
        {
            foreach (FeverHandEffect effect in FindSceneObjects<FeverHandEffect>(scene))
            {
                if (effect == null) continue;
                if (effect.GetComponent<DVariantRiderRig>() != null || effect.gameObject.name == "MantaRoot") continue;
                if (!effect.enabled) continue;

                effect.enabled = false;
                EditorUtility.SetDirty(effect);
            }
        }

        private static int DestroyNamedChildren(Transform parent, string childName)
        {
            int removed = 0;

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                removed += DestroyNamedChildren(child, childName);
                if (child.name != childName) continue;

                Object.DestroyImmediate(child.gameObject, true);
                removed++;
            }

            return removed;
        }

        private static T FindFirstSceneObject<T>(Scene scene) where T : Component
        {
            foreach (T item in FindSceneObjects<T>(scene))
            {
                return item;
            }

            return null;
        }

        private static T[] FindSceneObjects<T>(Scene scene) where T : Component
        {
            System.Collections.Generic.List<T> results = new System.Collections.Generic.List<T>();
            foreach (T item in Resources.FindObjectsOfTypeAll<T>())
            {
                if (item == null || item.gameObject.scene != scene) continue;
                results.Add(item);
            }

            return results.ToArray();
        }

        private static void SetObject(Object target, string propertyName, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null) return;
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string propertyName, float value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null) return;
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string propertyName, bool value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null) return;
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector3(Object target, string propertyName, Vector3 value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null) return;
            prop.vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
