using AO.Character;
using AO.Judgement;
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using Object = UnityEngine.Object;

namespace AO.Editor
{
    public static class CharacterSetupTools
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const string Vrm10SourcePath = "Assets/_Project/Art/Characters/Models/Final/AO_Model_Final1.vrm";
        private const string CharacterPrefabPath = "Assets/_Project/Prefabs/Player/PF_AO_Character_VRM10.prefab";
        private const string AnimationFolder = "Assets/_Project/Art/Characters/Animations";
        private const string IdleClipPath = AnimationFolder + "/AO_RiderIdle.anim";
        private const string ControllerPath = AnimationFolder + "/AC_AO_Rider.controller";
        private const float HandTriggerRadius = 0.1125f;

        [MenuItem("AO/Character/Create VRM10 Character Prefab From Source")]
        public static void CreateVrm10CharacterPrefabFromSourceMenu()
        {
            GameObject prefab = EnsureVrm10CharacterPrefabAsset();
            if (prefab != null)
            {
                Selection.activeObject = prefab;
                Debug.Log($"[AO] Created/updated VRM10 character prefab: {CharacterPrefabPath}", prefab);
            }
        }

        [MenuItem("AO/Character/Add Oculus Upper Body Driver To Selection")]
        public static void AddOculusUpperBodyDriverToSelection()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogError("[AO] Select a VRM character root or child first.");
                return;
            }

            Animator animator = selected.GetComponentInParent<Animator>();
            if (animator == null)
            {
                Debug.LogError("[AO] The selected object is not under a character Animator.");
                return;
            }

            OculusUpperBodyDriver driver = animator.GetComponent<OculusUpperBodyDriver>();
            if (driver == null)
            {
                Undo.AddComponent<OculusUpperBodyDriver>(animator.gameObject);
                EditorUtility.SetDirty(animator.gameObject);
                Debug.Log($"[AO] Added OculusUpperBodyDriver to {animator.gameObject.name}. Use the component context menu to auto-bind scene targets.");
            }
            else
            {
                Debug.Log($"[AO] OculusUpperBodyDriver already exists on {animator.gameObject.name}.", driver);
            }
        }

        [MenuItem("AO/Character/Validate VRM10 Character Prefab Materials And Rig")]
        public static void ValidateVrm10CharacterPrefabMaterialsAndRig()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[AO] VRM10 character prefab missing: {CharacterPrefabPath}");
                return;
            }

            Animator animator = prefab.GetComponent<Animator>() ?? prefab.GetComponentInChildren<Animator>(true);
            OculusUpperBodyDriver driver = animator != null ? animator.GetComponent<OculusUpperBodyDriver>() : null;
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            int materialSlots = 0;
            int missingMaterials = 0;
            int missingShaders = 0;
            System.Text.StringBuilder materialNames = new System.Text.StringBuilder();

            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materialSlots++;
                    Material material = materials[i];
                    if (material == null)
                    {
                        missingMaterials++;
                        Debug.LogError($"[AO] Missing character material: {renderer.name} slot {i}", renderer);
                        continue;
                    }

                    if (material.shader == null)
                    {
                        missingShaders++;
                        Debug.LogError($"[AO] Character material has missing shader: {material.name} on {renderer.name}", material);
                    }

                    if (materialNames.Length > 0) materialNames.Append(", ");
                    materialNames.Append(material.name);
                }
            }

            string avatar = animator != null && animator.avatar != null ? animator.avatar.name : "<none>";
            string humanoid = animator != null && animator.avatar != null && animator.avatar.isHuman ? "yes" : "no";
            string controller = animator != null && animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "<none>";
            Component expressionDriver = animator != null ? GetCharacterExpressionDriver(animator.gameObject) : null;

            Debug.Log(
                "[AO] VRM10 character prefab validation\n" +
                $"Prefab: {CharacterPrefabPath}\n" +
                $"Renderers: {renderers.Length}, material slots: {materialSlots}, missing materials: {missingMaterials}, missing shaders: {missingShaders}\n" +
                $"Animator: {(animator != null ? animator.name : "<missing>")}, avatar: {avatar}, humanoid: {humanoid}, controller: {controller}\n" +
                $"OculusUpperBodyDriver: {(driver != null ? "present" : "missing")}\n" +
                $"CharacterExpressionDriver: {(expressionDriver != null ? "present" : "missing")}\n" +
                $"Materials: {materialNames}",
                prefab);
        }

        [MenuItem("AO/Character/Apply Manta Rider Rig To Gameplay Scene")]
        public static void ApplyMantaRiderRigToGameplayScene()
        {
            GameObject characterPrefab = EnsureVrm10CharacterPrefabAsset();
            if (characterPrefab == null)
            {
                Debug.LogError("[AO] Aborted MANTA rider setup because the VRM10 character prefab could not be created.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            Transform hmd = RenderCameraTiltSetup.EnsureRenderCameraTiltInCurrentScene()
                ?? (Camera.main != null ? Camera.main.transform : FindSceneTransform("Main Camera"));
            Transform leftController = null;
            Transform rightController = null;
            Transform hitAnchor = FindSceneTransform("HitAnchor");
            Transform xrOrigin = FindSceneTransform("XR Origin (VR)");

            GameObject root = FindSceneObject("MantaRoot");
            if (root == null)
            {
                root = FindSceneObject("DVariantRiderRoot");
                if (root != null)
                {
                    root.name = "MantaRoot";
                }
                else
                {
                    root = new GameObject("MantaRoot");
                    Undo.RegisterCreatedObjectUndo(root, "Create MantaRoot");
                }
            }

            root.transform.SetParent(null, true);

            Transform seatAnchor = EnsureChild(root.transform, "SeatAnchor", new Vector3(0f, -1.03f, 0.08f), Quaternion.identity, Vector3.one * 0.38f);
            Transform characterRoot = EnsureChild(seatAnchor, "CharacterRoot", new Vector3(0f, 0.28f, -0.18f), Quaternion.identity, Vector3.one / 0.38f);
            Transform judgementRig = EnsureChild(root.transform, "JudgementRig", Vector3.zero, Quaternion.identity, Vector3.one);
            Transform leftTarget = EnsureChild(judgementRig, "LeftHandTarget", new Vector3(-0.55f, 1.302f, 3.093f), Quaternion.identity, Vector3.one);
            Transform rightTarget = EnsureChild(judgementRig, "RightHandTarget", new Vector3(0.509f, 1.302f, 3.093f), Quaternion.identity, Vector3.one);
            Transform visualHandTargets = EnsureChild(characterRoot, "VisualHandTargets", Vector3.zero, Quaternion.identity, Vector3.one);
            Transform leftVisualTarget = EnsureChild(visualHandTargets, "LeftHandTarget", new Vector3(-0.634f, 1.299f, -0.05f), Quaternion.identity, Vector3.one);
            Transform rightVisualTarget = EnsureChild(visualHandTargets, "RightHandTarget", new Vector3(0.452f, 1.299f, -0.05f), Quaternion.identity, Vector3.one);

            ValidateArtEnvironmentManta();
            Animator animator = EnsureCharacterVisual(characterRoot, characterPrefab);
            if (animator != null)
            {
                CharacterPrefabRigSafetyTools.ApplySafeAnimatorSettings(animator);
                EditorUtility.SetDirty(animator);
            }

            DVariantRiderRig rig = root.GetComponent<DVariantRiderRig>();
            if (rig == null) rig = Undo.AddComponent<DVariantRiderRig>(root);
            rig.enabled = true;
            rig.BindSceneReferences(hmd, leftController, rightController, hitAnchor, leftTarget, rightTarget, leftVisualTarget, rightVisualTarget, characterRoot, seatAnchor, animator);
            ConfigureNaturalHandMapping(rig);
            EditorUtility.SetDirty(rig);

            ConfigureJudgementHandTarget(leftTarget, XRNode.LeftHand);
            ConfigureJudgementHandTarget(rightTarget, XRNode.RightHand);

            if (animator != null)
            {
                OculusUpperBodyDriver driver = animator.GetComponent<OculusUpperBodyDriver>();
                if (driver == null) driver = Undo.AddComponent<OculusUpperBodyDriver>(animator.gameObject);
                ConfigureUpperBodyDriver(driver);
                driver.BindSceneTargets(rig, hmd, leftController, rightController, xrOrigin);
                EditorUtility.SetDirty(driver);
            }

            FeverHandEffect feverHandEffect = root.GetComponent<FeverHandEffect>();
            if (feverHandEffect != null) DisableMantaFeverHandEffect(feverHandEffect, rig);
            DisableLegacyFeverHandEffects(feverHandEffect);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[AO] Applied MANTA rider rig to GamePlayScene. Check MantaRoot/SeatAnchor/CharacterRoot/VisualRig and MantaRoot/JudgementRig in the Hierarchy.");
        }

        private static void DisableMantaFeverHandEffect(FeverHandEffect effect, DVariantRiderRig rig)
        {
            if (effect == null) return;

            effect.enabled = false;
            SetSerializedObjectReference(effect, "_riderRig", rig);
            SetSerializedObjectReference(effect, "_effectPrefab", null);
            SetSerialized(effect, "_spawnHandEffectOnFever", false);
            SetSerialized(effect, "_spawnInterval", 0.18f);
            SetSerialized(effect, "_effectLifetime", 1.6f);
            SetSerialized(effect, "_followSmoothSpeed", 18f);
            SetSerialized(effect, "_usePersistentTrails", true);
            SetSerialized(effect, "_followVisualHandTargets", true);
            SetSerialized(effect, "_parentTrailToHand", false);
            EditorUtility.SetDirty(effect);
        }

        private static void ConfigureNaturalHandMapping(DVariantRiderRig rig)
        {
            if (rig == null) return;

            SetSerialized(rig, "_readControllerPoseDirectlyFromXRNodes", true);
            SetSerialized(rig, "_enableKeyboardMouseDebugInput", true);
            SetSerialized(rig, "_horizontalScale", 1f);
            SetSerialized(rig, "_verticalScale", 1.15f);
            SetSerialized(rig, "_depthScale", 1.05f);
            SetSerialized(rig, "_useCommonPlayfieldHorizontalMapping", true);
            SetSerialized(rig, "_minimumCommonPlayfieldControllerSpan", 0.12f);
            SetSerialized(rig, "_limitHandWorkspace", false);
            SetSerialized(rig, "_handWorkspacePositiveLocal", new Vector3(0.78f, 0.85f, 0.82f));
            SetSerialized(rig, "_handWorkspaceNegativeLocal", new Vector3(0.78f, 0.62f, 0.70f));
            SetSerialized(rig, "_visualHandTargetSmoothSpeed", 42f);
        }

        private static void DisableLegacyFeverHandEffects(FeverHandEffect activeEffect)
        {
            foreach (FeverHandEffect effect in Object.FindObjectsByType<FeverHandEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (effect == null || effect == activeEffect) continue;
                if (!effect.gameObject.scene.isLoaded) continue;
                if (!effect.enabled) continue;

                effect.enabled = false;
                EditorUtility.SetDirty(effect);
            }
        }

        [MenuItem("AO/Character/Analyze Selected VRM Character")]
        public static void AnalyzeSelectedVrmCharacter()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogError("[AO] Select a VRM character root or child first.");
                return;
            }

            Animator animator = selected.GetComponentInParent<Animator>();
            if (animator == null)
            {
                Debug.LogError("[AO] No Animator was found on the selected character hierarchy.");
                return;
            }

            string controller = animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "<none>";
            string avatar = animator.avatar != null ? animator.avatar.name : "<none>";
            string humanoid = animator.avatar != null && animator.avatar.isHuman ? "yes" : "no";

            string bones =
                $"Head={Bone(animator, HumanBodyBones.Head)}, " +
                $"Neck={Bone(animator, HumanBodyBones.Neck)}, " +
                $"Chest={Bone(animator, HumanBodyBones.Chest)}, " +
                $"UpperChest={Bone(animator, HumanBodyBones.UpperChest)}, " +
                $"LeftHand={Bone(animator, HumanBodyBones.LeftHand)}, " +
                $"RightHand={Bone(animator, HumanBodyBones.RightHand)}";

            Debug.Log(
                "[AO] Selected character analysis\n" +
                $"Root: {animator.gameObject.name}\n" +
                $"Animator Controller: {controller}\n" +
                $"Avatar: {avatar}\n" +
                $"Humanoid Avatar: {humanoid}\n" +
                $"Key bones: {bones}",
                animator);
        }

        private static string Bone(Animator animator, HumanBodyBones bone)
        {
            Transform transform = animator.GetBoneTransform(bone);
            return transform != null ? transform.name : "<missing>";
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
                child.localPosition = localPosition;
                child.localRotation = localRotation;
                child.localScale = localScale;
                EditorUtility.SetDirty(child);
            }
            return child;
        }

        private static void ConfigureJudgementHandTarget(Transform target, XRNode hapticNode)
        {
            if (target == null) return;

            TrySetTag(target.gameObject, "Hand");

            JudgementHandTarget judgementHand = target.GetComponent<JudgementHandTarget>();
            if (judgementHand == null) judgementHand = Undo.AddComponent<JudgementHandTarget>(target.gameObject);
            judgementHand.Configure(hapticNode, HandTriggerRadius);
            EditorUtility.SetDirty(judgementHand);
        }

        private static void ConfigureInputProxyHand(Transform proxy)
        {
            if (proxy == null) return;

            proxy.gameObject.SetActive(true);
            TrySetTag(proxy.gameObject, "Untagged");

            Collider collider = proxy.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                EditorUtility.SetDirty(collider);
            }

            XRHandPoseFollower follower = proxy.GetComponent<XRHandPoseFollower>();
            if (follower != null)
            {
                SetSerialized(follower, "_positionSensitivity", 1f);
                SetSerialized(follower, "_useInitialPoseAsNeutral", true);
                EditorUtility.SetDirty(follower);
            }
        }

        private static void TrySetTag(GameObject go, string tag)
        {
            if (go == null) return;

            try
            {
                go.tag = tag;
            }
            catch (UnityException)
            {
                Debug.LogWarning($"[AO] Tag '{tag}' is not available yet for {go.name}. Run the Week 3 gameplay setup once if the Hand tag is missing.", go);
            }
        }

        private static void ValidateArtEnvironmentManta()
        {
            Transform artRoot = FindSceneTransform("ArtEnvironmentRoot");
            Transform manta = artRoot != null ? FindDescendantByName(artRoot, "MANTA") : null;
            if (manta != null) return;

            Debug.LogWarning("[AO] ArtEnvironmentRoot/MANTA was not found. Keep the authored MANTA under ArtEnvironmentRoot as the ride visual.");
        }

        private static Animator EnsureCharacterVisual(Transform characterRoot, GameObject characterPrefab)
        {
            Transform existing = characterRoot.Find("VisualRig") ?? characterRoot.Find("VisualRig_AO_Model");
            GameObject characterObject = existing != null ? existing.gameObject : null;
            if (characterObject != null) characterObject.name = "VisualRig";
            if (characterObject != null)
            {
                string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(characterObject);
                bool hasAnimator = characterObject.GetComponent<Animator>() != null || characterObject.GetComponentInChildren<Animator>(true) != null;
                if ((!string.IsNullOrEmpty(sourcePath) && sourcePath != CharacterPrefabPath) || !hasAnimator)
                {
                    Debug.LogWarning($"[AO] Replacing stale VisualRig source '{sourcePath}' with {CharacterPrefabPath}. Tune rider placement on CharacterRoot/SeatAnchor after replacement.");
                    Undo.DestroyObjectImmediate(characterObject);
                    characterObject = null;

                    GameObject replacement = InstantiateCharacterPrefab(characterPrefab, characterRoot);
                    if (replacement == null) return null;
                    characterObject = replacement;
                }
            }
            if (characterObject == null)
            {
                if (characterPrefab == null)
                {
                    Debug.LogError($"[AO] Character prefab missing at {CharacterPrefabPath}. Run AO/Character/Create VRM10 Character Prefab From Source first.");
                    return null;
                }

                characterObject = InstantiateCharacterPrefab(characterPrefab, characterRoot);
                if (characterObject == null) return null;
            }

            if (existing == null)
            {
                characterObject.transform.localPosition = Vector3.zero;
                characterObject.transform.localRotation = Quaternion.identity;
                characterObject.transform.localScale = Vector3.one;
                EditorUtility.SetDirty(characterObject);
            }
            return characterObject.GetComponent<Animator>();
        }

        private static GameObject InstantiateCharacterPrefab(GameObject characterPrefab, Transform characterRoot)
        {
            GameObject characterObject = PrefabUtility.InstantiatePrefab(characterPrefab, characterRoot) as GameObject;
            if (characterObject == null) return null;
            Undo.RegisterCreatedObjectUndo(characterObject, "Create VisualRig");
            characterObject.name = "VisualRig";
            return characterObject;
        }

        private static Transform FindDescendantByName(Transform parent, string name)
        {
            if (parent == null) return null;
            if (parent.name == name) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindDescendantByName(parent.GetChild(i), name);
                if (result != null) return result;
            }

            return null;
        }

        private static GameObject EnsureVrm10CharacterPrefabAsset()
        {
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Prefabs/Player");

            GameObject importedRoot = EnsureVrm10ImportedRoot();
            if (importedRoot == null) return AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);

            GameObject instance = PrefabUtility.InstantiatePrefab(importedRoot) as GameObject;
            if (instance == null) instance = Object.Instantiate(importedRoot);
            if (instance == null) return null;

            instance.name = Path.GetFileNameWithoutExtension(CharacterPrefabPath);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            Animator animator = instance.GetComponent<Animator>() ?? instance.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                CharacterPrefabRigSafetyTools.ApplySafeAnimatorSettings(animator);
                OculusUpperBodyDriver driver = animator.GetComponent<OculusUpperBodyDriver>();
                if (driver == null) driver = animator.gameObject.AddComponent<OculusUpperBodyDriver>();
                ConfigureUpperBodyDriver(driver);

                EnsureCharacterExpressionDriver(animator.gameObject);
            }
            else
            {
                Debug.LogWarning("[AO] VRM10 source imported, but no Animator was found on the created character prefab instance.", instance);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, CharacterPrefabPath, out bool success);
            Object.DestroyImmediate(instance);
            AssetDatabase.ImportAsset(CharacterPrefabPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            if (!success || prefab == null)
            {
                Debug.LogError($"[AO] Failed to save VRM10 character prefab at {CharacterPrefabPath}.");
                return null;
            }

            CharacterPrefabRigSafetyTools.ApplyToCharacterPrefabAsset();
            return prefab;
        }

        private static GameObject EnsureVrm10ImportedRoot()
        {
            if (!File.Exists(ToProjectFullPath(Vrm10SourcePath)))
            {
                Debug.LogError($"[AO] VRM10 source missing: {Vrm10SourcePath}");
                return null;
            }

            AssetDatabase.ImportAsset(Vrm10SourcePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetImporter importer = AssetImporter.GetAtPath(Vrm10SourcePath);
            if (!IsVrm10Importer(importer))
            {
                RegenerateStaleVrmMeta();
                AssetDatabase.ImportAsset(Vrm10SourcePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                importer = AssetImporter.GetAtPath(Vrm10SourcePath);
            }

            if (!IsVrm10Importer(importer))
            {
                string importerType = importer != null ? importer.GetType().FullName : "<null>";
                Debug.LogError($"[AO] {Vrm10SourcePath} is still not using UniVRM10.VrmScriptedImporter. Current importer: {importerType}");
                return null;
            }

            ConfigureVrm10Importer(importer);
            importer.SaveAndReimport();

            GameObject importedRoot = AssetDatabase.LoadAssetAtPath<GameObject>(Vrm10SourcePath);
            if (importedRoot != null) return importedRoot;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(Vrm10SourcePath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is GameObject go) return go;
            }

            Debug.LogError($"[AO] VRM10 importer ran, but no GameObject root was loaded from {Vrm10SourcePath}.");
            return null;
        }

        private static bool IsVrm10Importer(AssetImporter importer)
        {
            return importer != null && importer.GetType().FullName == "UniVRM10.VrmScriptedImporter";
        }

        private static void ConfigureVrm10Importer(AssetImporter importer)
        {
            SerializedObject serializedObject = new SerializedObject(importer);

            SerializedProperty migrate = serializedObject.FindProperty("MigrateToVrm1");
            if (migrate != null) migrate.boolValue = true;

            SerializedProperty renderPipeline = serializedObject.FindProperty("RenderPipeline");
            if (renderPipeline != null)
            {
                for (int i = 0; i < renderPipeline.enumNames.Length; i++)
                {
                    if (renderPipeline.enumNames[i] == "UniversalRenderPipeline")
                    {
                        renderPipeline.enumValueIndex = i;
                        break;
                    }
                }
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RegenerateStaleVrmMeta()
        {
            string metaPath = ToProjectFullPath(Vrm10SourcePath + ".meta");
            if (!File.Exists(metaPath)) return;

            Debug.LogWarning($"[AO] Regenerating stale VRM meta so UniVRM10 ScriptedImporter can own {Vrm10SourcePath}.");
            File.Delete(metaPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static AnimatorController EnsureRiderAnimatorController()
        {
            if (!AssetDatabase.IsValidFolder(AnimationFolder))
            {
                Directory.CreateDirectory(AnimationFolder);
                AssetDatabase.Refresh();
            }

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
            if (clip == null)
            {
                clip = new AnimationClip
                {
                    name = "AO_RiderIdle",
                    frameRate = 60f,
                    legacy = false
                };

                SerializedObject clipObject = new SerializedObject(clip);
                SerializedProperty loopTime = clipObject.FindProperty("m_AnimationClipSettings.m_LoopTime");
                if (loopTime != null) loopTime.boolValue = true;
                clipObject.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(clip, IdleClipPath);
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                AnimatorControllerLayer[] layers = controller.layers;
                layers[0].iKPass = true;
                controller.layers = layers;

                AnimatorState state = controller.layers[0].stateMachine.AddState("Riding Idle");
                state.motion = clip;
                state.writeDefaultValues = false;
                controller.layers[0].stateMachine.defaultState = state;
                EditorUtility.SetDirty(controller);
            }
            else
            {
                AnimatorControllerLayer[] layers = controller.layers;
                if (layers.Length > 0 && !layers[0].iKPass)
                {
                    layers[0].iKPass = true;
                    controller.layers = layers;
                    EditorUtility.SetDirty(controller);
                }
            }

            return controller;
        }

        private static void ConfigureUpperBodyDriver(OculusUpperBodyDriver driver)
        {
            if (driver == null) return;

            SetSerialized(driver, "_autoBindMissingSceneTargets", true);
            SetSerialized(driver, "_readMissingReferencesFromXRNodes", true);
            SetSerialized(driver, "_driveHead", true);
            SetSerialized(driver, "_driveChest", true);
            SetSerialized(driver, "_poseSmoothSpeed", 12f);
            SetSerialized(driver, "_chestYawWeight", 0.22f);
            SetSerialized(driver, "_chestPitchWeight", 0.1f);
            SetSerialized(driver, "_neckWeight", 0.22f);
            SetSerialized(driver, "_headWeight", 0.38f);
            SetSerialized(driver, "_driveHandsWithAnimatorIk", false);
            SetSerialized(driver, "_handIkPositionWeight", 0.95f);
            SetSerialized(driver, "_handIkRotationWeight", 0.6f);
        }

        private static Type FindCharacterExpressionDriverType()
        {
            return Type.GetType("AO.Character.CharacterExpressionDriver, Assembly-CSharp");
        }

        private static Component GetCharacterExpressionDriver(GameObject target)
        {
            Type driverType = FindCharacterExpressionDriverType();
            return target != null && driverType != null ? target.GetComponent(driverType) : null;
        }

        private static Component EnsureCharacterExpressionDriver(GameObject target)
        {
            if (target == null) return null;

            Component driver = GetCharacterExpressionDriver(target);
            if (driver != null) return driver;

            Type driverType = FindCharacterExpressionDriverType();
            return driverType != null ? target.AddComponent(driverType) : null;
        }

        private static GameObject FindSceneObject(string objectName)
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!go.scene.IsValid() || !go.scene.isLoaded) continue;
                if (go.name == objectName) return go;
            }

            return null;
        }

        private static Transform FindSceneTransform(string objectName)
        {
            GameObject go = FindSceneObject(objectName);
            return go != null ? go.transform : null;
        }

        private static void SetSerialized(Object target, string propertyName, float value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) property.floatValue = value;
            serializedObject.ApplyModifiedProperties();
        }

        private static void SetSerialized(Object target, string propertyName, bool value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) property.boolValue = value;
            serializedObject.ApplyModifiedProperties();
        }

        private static void SetSerialized(Object target, string propertyName, Vector3 value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) property.vector3Value = value;
            serializedObject.ApplyModifiedProperties();
        }

        private static void SetSerializedObjectReference(Object target, string propertyName, Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = value;
            serializedObject.ApplyModifiedProperties();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static string ToProjectFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return projectRoot == null ? assetPath : Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }
    }

    public static class CharacterPrefabRigSafetyTools
    {
        public const string CharacterPrefabPath = "Assets/_Project/Prefabs/Player/PF_AO_Character_VRM10.prefab";
        public const string ScenePoseClipPath = "Assets/_Project/Art/Characters/Animations/AO_RiderScenePose.anim";
        public const string ScenePoseControllerPath = "Assets/_Project/Art/Characters/Animations/AC_AO_RiderScenePose.controller";

        [MenuItem("AO/Character/Apply PF_AO_Character_VRM10 Rig Safety")]
        public static void ApplyCharacterPrefabRigSafetyMenu()
        {
            if (ApplyToCharacterPrefabAsset())
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
                Selection.activeObject = prefab;
                Debug.Log($"[AO] Applied PF_AO_Character_VRM10 rig safety. New scene placements now start with {ScenePoseControllerPath}.", prefab);
            }
        }

        public static bool ApplyToCharacterPrefabAsset()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[AO] Character prefab missing at {CharacterPrefabPath}.");
                return false;
            }

            Animator animator = prefab.GetComponent<Animator>() ?? prefab.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogError($"[AO] No Animator found in {CharacterPrefabPath}. Cannot apply rig safety.", prefab);
                return false;
            }

            if (!ValidateScenePoseAssets(out string report))
            {
                Debug.LogError($"[AO] Refusing to assign unsafe scene-pose controller to {CharacterPrefabPath}.\n{report}", prefab);
                return false;
            }

            ApplySafeAnimatorSettings(animator);
            PrefabUtility.SavePrefabAsset(prefab);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AO] PF_AO_Character_VRM10 rig safety validated.\n{report}", prefab);
            return true;
        }

        public static void ApplySafeAnimatorSettings(Animator animator)
        {
            if (animator == null) return;

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ScenePoseControllerPath);
            if (controller != null)
            {
                animator.runtimeAnimatorController = controller;
            }

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            OculusUpperBodyDriver driver = animator.GetComponent<OculusUpperBodyDriver>();
            if (driver != null)
            {
                SetBool(driver, "_driveHandsWithAnimatorIk", false);
            }

            EditorUtility.SetDirty(animator);
            if (driver != null) EditorUtility.SetDirty(driver);
        }

        public static bool ValidateScenePoseAssets(out string report)
        {
            StringBuilder builder = new StringBuilder();
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ScenePoseControllerPath);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ScenePoseClipPath);

            builder.AppendLine($"Controller: {(controller != null ? controller.name : "<missing>")}");
            builder.AppendLine($"Clip: {(clip != null ? clip.name : "<missing>")}");

            if (controller == null || clip == null)
            {
                report = builder.ToString();
                return false;
            }

            int animatorCurves = 0;
            int transformCurves = 0;
            int otherCurves = 0;
            EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(clip);
            for (int i = 0; i < curveBindings.Length; i++)
            {
                System.Type bindingType = curveBindings[i].type;
                if (bindingType == typeof(Animator)) animatorCurves++;
                else if (bindingType == typeof(Transform)) transformCurves++;
                else otherCurves++;
            }

            int objectReferenceCurves = AnimationUtility.GetObjectReferenceCurveBindings(clip).Length;
            builder.AppendLine($"AO_RiderScenePose diagnostics: AnimatorCurves={animatorCurves}, TransformCurves={transformCurves}, OtherCurves={otherCurves}, ObjectReferenceCurves={objectReferenceCurves}");

            bool controllerUsesClip = false;
            AnimationClip[] clips = controller.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == clip)
                {
                    controllerUsesClip = true;
                    break;
                }
            }

            builder.AppendLine($"ControllerUsesScenePoseClip: {(controllerUsesClip ? "yes" : "no")}");
            report = builder.ToString();

            return controllerUsesClip && animatorCurves > 0 && transformCurves == 0;
        }

        private static void SetBool(Object target, string propertyName, bool value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) property.boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
