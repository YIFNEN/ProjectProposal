using AO.Character;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class DVariantArmNaturalMotionSetup
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const string LeftHintName = "LeftElbowHint";
        private const string RightHintName = "RightElbowHint";

        [MenuItem("AO/Character/Apply Dynamic Arm Natural Motion")]
        public static void ApplyToGameplayScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            bool applied = ApplyToScene(scene);

            if (applied)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }

            Debug.Log(applied
                ? "[AO] Applied dynamic arm natural motion to GamePlayScene."
                : "[AO] Dynamic arm natural motion was not applied. Required D-Variant references are missing.");
        }

        public static void ApplyFromCommandLine()
        {
            ApplyToGameplayScene();
        }

        public static bool ApplyToScene(Scene scene)
        {
            DVariantRiderRig riderRig = FindFirstSceneObject<DVariantRiderRig>(scene);
            if (riderRig == null)
            {
                Debug.LogWarning("[AO] DVariantRiderRig was not found. Dynamic arm natural motion was not configured.");
                return false;
            }

            Transform characterRoot = riderRig.CharacterRoot ?? FindSceneTransform(scene, "CharacterRoot");
            Animator animator = riderRig.VisualAnimator;
            if (animator == null && characterRoot != null) animator = characterRoot.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogWarning("[AO] VisualRig Animator was not found. Dynamic arm natural motion was not configured.");
                return false;
            }

            Transform visualTargets = characterRoot != null ? characterRoot.Find("VisualHandTargets") : null;
            if (visualTargets == null)
            {
                Debug.LogWarning("[AO] CharacterRoot/VisualHandTargets was not found. Dynamic arm natural motion was not configured.");
                return false;
            }

            Transform leftTarget = riderRig.LeftVisualHandTarget ?? visualTargets.Find("LeftHandTarget");
            Transform rightTarget = riderRig.RightVisualHandTarget ?? visualTargets.Find("RightHandTarget");
            if (leftTarget == null || rightTarget == null)
            {
                Debug.LogWarning("[AO] Visual hand targets were not found. Dynamic arm natural motion was not configured.");
                return false;
            }

            Transform leftHint = EnsureHint(visualTargets, LeftHintName, leftTarget, new Vector3(-0.18f, -0.05f, -0.25f));
            Transform rightHint = EnsureHint(visualTargets, RightHintName, rightTarget, new Vector3(0.18f, -0.05f, -0.25f));

            DVariantArmNaturalMotionDriver driver = visualTargets.GetComponent<DVariantArmNaturalMotionDriver>();
            bool created = false;
            if (driver == null)
            {
                driver = Undo.AddComponent<DVariantArmNaturalMotionDriver>(visualTargets.gameObject);
                created = true;
            }

            if (created) driver.ApplyRecommendedDefaults();
            driver.BindSceneReferences(riderRig, animator, characterRoot, leftTarget, rightTarget, leftHint, rightHint);
            driver.enabled = true;

            EditorUtility.SetDirty(leftHint);
            EditorUtility.SetDirty(rightHint);
            EditorUtility.SetDirty(driver);
            return true;
        }

        private static Transform EnsureHint(Transform parent, string name, Transform handTarget, Vector3 localOffset)
        {
            Transform hint = parent.Find(name);
            if (hint == null)
            {
                GameObject go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
                hint = go.transform;
                hint.SetParent(parent, false);
                hint.localPosition = handTarget.localPosition + localOffset;
                hint.localRotation = Quaternion.identity;
                hint.localScale = Vector3.one;
            }

            return hint;
        }

        private static Transform FindSceneTransform(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindChildByName(root.transform, objectName);
                if (found != null) return found;
            }

            return null;
        }

        private static Transform FindChildByName(Transform parent, string objectName)
        {
            if (parent.name == objectName) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildByName(parent.GetChild(i), objectName);
                if (found != null) return found;
            }

            return null;
        }

        private static T FindFirstSceneObject<T>(Scene scene) where T : Component
        {
            foreach (T item in Resources.FindObjectsOfTypeAll<T>())
            {
                if (item == null || item.gameObject.scene != scene) continue;
                return item;
            }

            return null;
        }
    }
}
