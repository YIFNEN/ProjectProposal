using AO.Character;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class AnimationRiggingArmIkSetup
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const string CharacterPrefabPath = "Assets/_Project/Prefabs/Player/PF_AO_Character_VRM10.prefab";
        private const string AnimationFolder = "Assets/_Project/Art/Characters/Animations";
        private const string ScenePoseClipPath = AnimationFolder + "/AO_RiderScenePose.anim";
        private const string ScenePoseControllerPath = AnimationFolder + "/AC_AO_RiderScenePose.controller";
        private const string ScenePoseStateName = "Scene Pose Base";
        private const string RigName = "AO_ArmRig";
        private const string LeftConstraintName = "LeftArm_TwoBoneIK";
        private const string RightConstraintName = "RightArm_TwoBoneIK";
        private const string LeftHintName = "LeftElbowHint";
        private const string RightHintName = "RightElbowHint";
        private const float PoseClipDuration = 1f / 60f;

        [MenuItem("AO/Character/Apply Animation Rigging Arm IK")]
        public static void ApplyToGameplayScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            DVariantRiderRig riderRig = FindSceneComponent<DVariantRiderRig>();
            if (riderRig == null)
            {
                Debug.LogError("[AO] DVariantRiderRig was not found in GamePlayScene. Run the MANTA rider setup before applying Animation Rigging arm IK.");
                return;
            }

            Transform characterRoot = riderRig.CharacterRoot;
            if (characterRoot == null) characterRoot = FindSceneTransform("CharacterRoot");

            Animator animator = riderRig.VisualAnimator;
            if (animator == null && characterRoot != null) animator = characterRoot.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogError("[AO] VisualRig Animator was not found. Cannot apply Animation Rigging arm IK.");
                return;
            }

            if (animator.avatar == null || !animator.avatar.isHuman)
            {
                Debug.LogError("[AO] VisualRig Animator must have a valid Humanoid Avatar for automatic arm bone binding.", animator);
                return;
            }

            AnimatorController poseController = EnsureScenePoseBaseController(animator);
            if (poseController != null && animator.runtimeAnimatorController != poseController)
            {
                animator.runtimeAnimatorController = poseController;
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            }

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EditorUtility.SetDirty(animator);

            Transform visualTargets = characterRoot != null ? characterRoot.Find("VisualHandTargets") : null;
            Transform leftTarget = riderRig.LeftVisualHandTarget ?? (visualTargets != null ? visualTargets.Find("LeftHandTarget") : null);
            Transform rightTarget = riderRig.RightVisualHandTarget ?? (visualTargets != null ? visualTargets.Find("RightHandTarget") : null);
            if (leftTarget == null || rightTarget == null)
            {
                Debug.LogError("[AO] Visual hand targets are missing under CharacterRoot/VisualHandTargets.", characterRoot);
                return;
            }

            Transform leftHint = EnsureHint(visualTargets, LeftHintName, leftTarget, new Vector3(-0.18f, -0.05f, -0.25f));
            Transform rightHint = EnsureHint(visualTargets, RightHintName, rightTarget, new Vector3(0.18f, -0.05f, -0.25f));

            RigBuilder rigBuilder = animator.GetComponent<RigBuilder>();
            if (rigBuilder == null) rigBuilder = Undo.AddComponent<RigBuilder>(animator.gameObject);
            rigBuilder.enabled = true;

            Transform rigTransform = EnsureChild(animator.transform, RigName, Vector3.zero, Quaternion.identity, Vector3.one);
            Rig rig = rigTransform.GetComponent<Rig>();
            if (rig == null) rig = Undo.AddComponent<Rig>(rigTransform.gameObject);
            rig.weight = 1f;

            ConfigureArmConstraint(
                rigTransform,
                LeftConstraintName,
                animator.GetBoneTransform(HumanBodyBones.LeftUpperArm),
                animator.GetBoneTransform(HumanBodyBones.LeftLowerArm),
                animator.GetBoneTransform(HumanBodyBones.LeftHand),
                leftTarget,
                leftHint);

            ConfigureArmConstraint(
                rigTransform,
                RightConstraintName,
                animator.GetBoneTransform(HumanBodyBones.RightUpperArm),
                animator.GetBoneTransform(HumanBodyBones.RightLowerArm),
                animator.GetBoneTransform(HumanBodyBones.RightHand),
                rightTarget,
                rightHint);

            EnsureRigLayer(rigBuilder, rig);
            RemovePoseGuardIfPresent(animator);
            DisableAnimatorIkHands(animator);
            DisablePrefabAnimatorIkHands();
            DVariantArmNaturalMotionSetup.ApplyToScene(scene);

            EditorUtility.SetDirty(rig);
            EditorUtility.SetDirty(rigBuilder);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[AO] Applied Animation Rigging arm IK to GamePlayScene. VisualRig uses a captured scene-pose base controller; hands are driven by Two-Bone IK constraints after that base pose.", animator);
        }

        [MenuItem("AO/Character/Analyze VisualRig Humanoid Pose")]
        public static void AnalyzeVisualRigHumanoidPose()
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            Animator animator = FindVisualRigAnimator();
            if (animator == null)
            {
                Debug.LogError("[AO] VisualRig Animator was not found. Cannot analyze humanoid pose.");
                return;
            }

            string report = BuildPoseReport(animator);
            string logPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "HumanoidPoseAnalysis.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            File.WriteAllText(logPath, report);

            Debug.Log($"[AO] VisualRig humanoid pose analysis written to {logPath}\n{report}", animator);
        }

        [MenuItem("AO/Character/Apply Scene Pose Base For RigBuilder")]
        public static void ApplyScenePoseBaseForRigBuilder()
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            Animator animator = FindVisualRigAnimator();
            if (animator == null)
            {
                Debug.LogError("[AO] VisualRig Animator was not found. Cannot apply scene-pose base controller.");
                return;
            }

            AnimatorController poseController = EnsureScenePoseBaseController(animator);
            if (poseController == null) return;

            animator.runtimeAnimatorController = poseController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            RemovePoseGuardIfPresent(animator);

            EditorUtility.SetDirty(animator);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[AO] Applied captured scene-pose base controller to VisualRig. RigBuilder now evaluates over a real keyed pose instead of an empty Animator stream.", animator);
        }

        private static Transform EnsureHint(Transform parent, string name, Transform handTarget, Vector3 localOffset)
        {
            Transform hint = parent != null ? parent.Find(name) : null;
            if (hint == null)
            {
                GameObject go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
                hint = go.transform;
                hint.SetParent(parent, false);
            }

            hint.localPosition = handTarget.localPosition + localOffset;
            hint.localRotation = Quaternion.identity;
            hint.localScale = Vector3.one;
            EditorUtility.SetDirty(hint);
            return hint;
        }

        private static Transform EnsureChild(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                GameObject go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
                child = go.transform;
                child.SetParent(parent, false);
            }

            child.localPosition = localPosition;
            child.localRotation = localRotation;
            child.localScale = localScale;
            EditorUtility.SetDirty(child);
            return child;
        }

        private static void ConfigureArmConstraint(
            Transform rigRoot,
            string constraintName,
            Transform upperArm,
            Transform lowerArm,
            Transform hand,
            Transform target,
            Transform hint)
        {
            if (upperArm == null || lowerArm == null || hand == null)
            {
                Debug.LogError($"[AO] Missing humanoid arm bones for {constraintName}. upper={NameOf(upperArm)}, lower={NameOf(lowerArm)}, hand={NameOf(hand)}");
                return;
            }

            Transform constraintTransform = EnsureChild(rigRoot, constraintName, Vector3.zero, Quaternion.identity, Vector3.one);
            TwoBoneIKConstraint constraint = constraintTransform.GetComponent<TwoBoneIKConstraint>();
            if (constraint == null) constraint = Undo.AddComponent<TwoBoneIKConstraint>(constraintTransform.gameObject);

            TwoBoneIKConstraintData data = constraint.data;
            data.root = upperArm;
            data.mid = lowerArm;
            data.tip = hand;
            data.target = target;
            data.hint = hint;
            data.maintainTargetPositionOffset = false;
            data.maintainTargetRotationOffset = false;
            data.targetPositionWeight = 1f;
            data.targetRotationWeight = 0.85f;
            data.hintWeight = 0.8f;
            constraint.data = data;
            constraint.weight = 1f;

            EditorUtility.SetDirty(constraint);
        }

        private static void EnsureRigLayer(RigBuilder rigBuilder, Rig rig)
        {
            bool found = false;
            for (int i = 0; i < rigBuilder.layers.Count; i++)
            {
                RigLayer layer = rigBuilder.layers[i];
                if (layer.rig != rig) continue;

                layer.active = true;
                rigBuilder.layers[i] = layer;
                found = true;
                break;
            }

            if (!found) rigBuilder.layers.Add(new RigLayer(rig, true));
        }

        private static AnimatorController EnsureScenePoseBaseController(Animator animator)
        {
            if (animator == null) return null;

            EnsureFolder(AnimationFolder);

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ScenePoseClipPath);
            if (clip == null)
            {
                clip = new AnimationClip
                {
                    name = "AO_RiderScenePose",
                    frameRate = 60f,
                    legacy = false
                };
                AssetDatabase.CreateAsset(clip, ScenePoseClipPath);
            }

            CaptureScenePoseClip(animator, clip);

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ScenePoseControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ScenePoseControllerPath);
            }

            ConfigureScenePoseController(controller, clip);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void CaptureScenePoseClip(Animator animator, AnimationClip clip)
        {
            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            {
                CaptureHumanoidScenePoseClip(animator, clip);
                return;
            }

            CaptureGenericScenePoseClip(animator, clip);
        }

        private static void CaptureHumanoidScenePoseClip(Animator animator, AnimationClip clip)
        {
            HumanPose pose = new HumanPose();
            HumanPoseHandler handler = new HumanPoseHandler(animator.avatar, animator.transform);
            handler.GetHumanPose(ref pose);
            (handler as System.IDisposable)?.Dispose();

            AnimationClip humanoidClip = UniHumanoid.AnimationClipUtility.CreateAnimationClipFromHumanPose(pose);
            humanoidClip.name = clip.name;
            humanoidClip.frameRate = 60f;
            humanoidClip.legacy = false;

            clip.ClearCurves();
            EditorUtility.CopySerialized(humanoidClip, clip);
            Object.DestroyImmediate(humanoidClip);

            clip.name = "AO_RiderScenePose";
            clip.frameRate = 60f;
            clip.legacy = false;
            ApplyLoopSettings(clip);

            EditorUtility.SetDirty(clip);
        }

        private static void CaptureGenericScenePoseClip(Animator animator, AnimationClip clip)
        {
            clip.ClearCurves();
            clip.frameRate = 60f;

            foreach (Transform transform in EnumeratePoseTransforms(animator.transform))
            {
                string path = AnimationUtility.CalculateTransformPath(transform, animator.transform);
                AddTransformCurves(clip, path, transform.localPosition, transform.localRotation, transform.localScale);
            }

            ApplyLoopSettings(clip);

            EditorUtility.SetDirty(clip);
        }

        private static void ApplyLoopSettings(AnimationClip clip)
        {
            SerializedObject clipObject = new SerializedObject(clip);
            SerializedProperty loopTime = clipObject.FindProperty("m_AnimationClipSettings.m_LoopTime");
            if (loopTime != null) loopTime.boolValue = true;
            SerializedProperty keepOriginalPositionY = clipObject.FindProperty("m_AnimationClipSettings.m_KeepOriginalPositionY");
            if (keepOriginalPositionY != null) keepOriginalPositionY.boolValue = true;
            SerializedProperty keepOriginalPositionXZ = clipObject.FindProperty("m_AnimationClipSettings.m_KeepOriginalPositionXZ");
            if (keepOriginalPositionXZ != null) keepOriginalPositionXZ.boolValue = true;
            clipObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static IEnumerable<Transform> EnumeratePoseTransforms(Transform root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (IsUnderRigObject(root, transform)) continue;
                yield return transform;
            }
        }

        private static bool IsUnderRigObject(Transform root, Transform transform)
        {
            for (Transform current = transform; current != null && current != root; current = current.parent)
            {
                if (current.name == RigName) return true;
            }

            return false;
        }

        private static void AddTransformCurves(AnimationClip clip, string path, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            SetCurve(clip, path, "m_LocalPosition.x", localPosition.x);
            SetCurve(clip, path, "m_LocalPosition.y", localPosition.y);
            SetCurve(clip, path, "m_LocalPosition.z", localPosition.z);
            SetCurve(clip, path, "m_LocalRotation.x", localRotation.x);
            SetCurve(clip, path, "m_LocalRotation.y", localRotation.y);
            SetCurve(clip, path, "m_LocalRotation.z", localRotation.z);
            SetCurve(clip, path, "m_LocalRotation.w", localRotation.w);
            SetCurve(clip, path, "m_LocalScale.x", localScale.x);
            SetCurve(clip, path, "m_LocalScale.y", localScale.y);
            SetCurve(clip, path, "m_LocalScale.z", localScale.z);
        }

        private static void SetCurve(AnimationClip clip, string path, string propertyName, float value)
        {
            AnimationCurve curve = AnimationCurve.Constant(0f, PoseClipDuration, value);
            EditorCurveBinding binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), propertyName);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        private static void ConfigureScenePoseController(AnimatorController controller, AnimationClip clip)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            if (layers.Length == 0)
            {
                controller.AddLayer("Base Layer");
                layers = controller.layers;
            }

            layers[0].iKPass = false;
            controller.layers = layers;

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState state = null;
            ChildAnimatorState[] states = stateMachine.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state != null && states[i].state.name == ScenePoseStateName)
                {
                    state = states[i].state;
                    break;
                }
            }

            if (state == null)
            {
                state = stateMachine.AddState(ScenePoseStateName);
            }

            state.motion = clip;
            state.writeDefaultValues = false;
            state.speed = 1f;
            stateMachine.defaultState = state;

            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
        }

        private static void RemovePoseGuardIfPresent(Animator animator)
        {
            VrmRigPoseGuard poseGuard = animator != null ? animator.GetComponent<VrmRigPoseGuard>() : null;
            if (poseGuard == null) return;

            Undo.DestroyObjectImmediate(poseGuard);
        }

        private static string BuildPoseReport(Animator animator)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine("[AO] VisualRig Humanoid Pose Analysis");
            builder.AppendLine($"Scene: {GameplayScenePath}");
            builder.AppendLine($"Animator: {animator.name}");
            builder.AppendLine($"Controller: {(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "<none>")}");
            builder.AppendLine($"Avatar: {(animator.avatar != null ? animator.avatar.name : "<none>")}");
            builder.AppendLine($"Humanoid: {(animator.avatar != null && animator.avatar.isHuman ? "yes" : "no")}");
            builder.AppendLine($"ApplyRootMotion: {animator.applyRootMotion}");
            builder.AppendLine($"CullingMode: {animator.cullingMode}");
            builder.AppendLine();
            AppendAnimationClipDiagnostics(builder, animator);
            builder.AppendLine();
            AppendTransform(builder, "VisualRig", animator.transform);
            builder.AppendLine();

            HumanBodyBones[] bones =
            {
                HumanBodyBones.Hips,
                HumanBodyBones.Spine,
                HumanBodyBones.Chest,
                HumanBodyBones.UpperChest,
                HumanBodyBones.Neck,
                HumanBodyBones.Head,
                HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.LeftFoot,
                HumanBodyBones.LeftToes,
                HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightLowerLeg,
                HumanBodyBones.RightFoot,
                HumanBodyBones.RightToes,
                HumanBodyBones.LeftShoulder,
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm,
                HumanBodyBones.LeftHand,
                HumanBodyBones.RightShoulder,
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.RightLowerArm,
                HumanBodyBones.RightHand
            };

            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = animator.isHuman ? animator.GetBoneTransform(bones[i]) : null;
                AppendTransform(builder, bones[i].ToString(), bone);
            }

            return builder.ToString();
        }

        private static void AppendAnimationClipDiagnostics(System.Text.StringBuilder builder, Animator animator)
        {
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            builder.AppendLine("Controller Clip Diagnostics:");
            if (controller == null)
            {
                builder.AppendLine("- <none>");
                return;
            }

            HashSet<AnimationClip> seen = new HashSet<AnimationClip>();
            AnimationClip[] clips = controller.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null || !seen.Add(clip)) continue;

                EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(clip);
                EditorCurveBinding[] objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                int animatorCurves = 0;
                int transformCurves = 0;
                int otherCurves = 0;

                for (int bindingIndex = 0; bindingIndex < curveBindings.Length; bindingIndex++)
                {
                    System.Type bindingType = curveBindings[bindingIndex].type;
                    if (bindingType == typeof(Animator)) animatorCurves++;
                    else if (bindingType == typeof(Transform)) transformCurves++;
                    else otherCurves++;
                }

                builder.AppendLine(
                    $"- {clip.name}: AnimatorCurves={animatorCurves}, TransformCurves={transformCurves}, " +
                    $"OtherCurves={otherCurves}, ObjectReferenceCurves={objectBindings.Length}");

                if (animator.avatar != null && animator.avatar.isHuman && transformCurves > 0)
                {
                    builder.AppendLine("  WARNING: Humanoid Animator will not reliably apply Transform curves on Humanoid-bound bones. Use Animator muscle curves for the base pose.");
                }
            }
        }

        private static void AppendTransform(System.Text.StringBuilder builder, string label, Transform transform)
        {
            if (transform == null)
            {
                builder.AppendLine($"{label}: <missing>");
                return;
            }

            string parent = transform.parent != null ? transform.parent.name : "<none>";
            builder.AppendLine(
                $"{label}: name={transform.name}, parent={parent}, " +
                $"localPos={Format(transform.localPosition)}, localRot={Format(transform.localRotation)}, localScale={Format(transform.localScale)}, " +
                $"worldPos={Format(transform.position)}, worldRot={Format(transform.rotation)}");
        }

        private static string Format(Vector3 value)
        {
            return $"({value.x:0.#####}, {value.y:0.#####}, {value.z:0.#####})";
        }

        private static string Format(Quaternion value)
        {
            return $"({value.x:0.#####}, {value.y:0.#####}, {value.z:0.#####}, {value.w:0.#####})";
        }

        private static void DisableAnimatorIkHands(Animator animator)
        {
            OculusUpperBodyDriver driver = animator.GetComponent<OculusUpperBodyDriver>();
            if (driver == null) return;

            SetSerialized(driver, "_driveHandsWithAnimatorIk", false);
            SetSerialized(driver, "_driveHead", true);
            SetSerialized(driver, "_driveChest", true);
            EditorUtility.SetDirty(driver);
        }

        private static void DisablePrefabAnimatorIkHands()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            if (prefab == null) return;

            OculusUpperBodyDriver driver = prefab.GetComponentInChildren<OculusUpperBodyDriver>(true);
            if (driver == null) return;

            SetSerialized(driver, "_driveHandsWithAnimatorIk", false);
            EditorUtility.SetDirty(driver);
            PrefabUtility.SavePrefabAsset(prefab);
        }

        private static void SetSerialized(Object target, string propertyName, bool value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) property.boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static string NameOf(Transform transform)
        {
            return transform != null ? transform.name : "<missing>";
        }

        private static T FindSceneComponent<T>() where T : Component
        {
            foreach (T component in Resources.FindObjectsOfTypeAll<T>())
            {
                if (component == null || !component.gameObject.scene.IsValid() || !component.gameObject.scene.isLoaded) continue;
                return component;
            }

            return null;
        }

        private static Transform FindSceneTransform(string objectName)
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!go.scene.IsValid() || !go.scene.isLoaded) continue;
                if (go.name == objectName) return go.transform;
            }

            return null;
        }

        private static Animator FindVisualRigAnimator()
        {
            DVariantRiderRig riderRig = FindSceneComponent<DVariantRiderRig>();
            if (riderRig != null && riderRig.VisualAnimator != null) return riderRig.VisualAnimator;

            Transform visualRig = FindSceneTransform("VisualRig");
            return visualRig != null ? visualRig.GetComponent<Animator>() : FindSceneComponent<Animator>();
        }
    }
}
