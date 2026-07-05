using System.Collections.Generic;
using System.IO;
using System;
using AO.Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AO.Editor
{
    public static class MenuCharacterShowcaseSetup
    {
        private const string TitleScenePath = "Assets/_Project/Scenes/01_Title.unity";
        private const string LobbyScenePath = "Assets/_Project/Scenes/02_Lobby.unity";
        private const string ResultScenePath = "Assets/_Project/Scenes/04_Result.unity";
        private const string CharacterPrefabPath = "Assets/_Project/Prefabs/Player/PF_AO_Character_VRM10.prefab";
        private const string AnimationFolder = "Assets/_Project/Art/Characters/Models/Final/animation";
        private const string TitleAnimationFolder = AnimationFolder + "/Title";
        private const string LobbyAnimationFolder = AnimationFolder + "/Lobby";
        private const string ResultAnimationFolder = AnimationFolder + "/Result";
        private const string TitleControllerPath = AnimationFolder + "/AC_AO_Menu_Title_Idle.controller";
        private const string LobbyControllerPath = AnimationFolder + "/AC_AO_Menu_Lobby_SittingCycle.controller";
        private const string ResultControllerPath = AnimationFolder + "/AC_AO_Menu_Result_Cycle.controller";

        private static readonly Vector3 MenuCharacterPosition = new Vector3(1.726f, 0.278f, 8.063f);
        private static readonly Quaternion MenuCharacterRotation = Quaternion.Euler(0f, 150f, 0f);

        [MenuItem("AO/Setup/Apply Menu Character Showcase")]
        public static void Apply()
        {
            AssetDatabase.Refresh();

            AnimatorController titleController = EnsureTitleController();
            AnimatorController lobbyController = EnsureLobbyController();
            AnimatorController resultController = EnsureResultController();

            ConfigureMenuScene(TitleScenePath, titleController, "Title");
            ConfigureMenuScene(LobbyScenePath, lobbyController, "Lobby");
            ConfigureMenuScene(ResultScenePath, resultController, "Result");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AO] Menu character showcase applied. Title/Lobby/Result use their scene-specific animation folders; root motion and XR/controller pose drivers are disabled on menu characters.");
        }

        public static void ApplyFromCommandLine()
        {
            Apply();
        }

        private static AnimatorController EnsureTitleController()
        {
            List<AnimationClip> clips = LoadMenuClipsFromFolder(TitleAnimationFolder, "Title");
            if (clips.Count == 0)
            {
                Debug.LogError($"[AO] Title animation clips not found under {TitleAnimationFolder}.");
                return null;
            }

            AnimatorController controller = EnsureControllerAsset(TitleControllerPath);
            ConfigureCycleController(controller, clips);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorController EnsureLobbyController()
        {
            List<AnimationClip> clips = LoadMenuClipsFromFolder(LobbyAnimationFolder, "Lobby");
            if (clips.Count == 0)
            {
                Debug.LogError($"[AO] Lobby animation clips not found under {LobbyAnimationFolder}.");
                return null;
            }

            AnimatorController controller = EnsureControllerAsset(LobbyControllerPath);
            ConfigureCycleController(controller, clips);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorController EnsureResultController()
        {
            List<AnimationClip> clips = LoadMenuClipsFromFolder(ResultAnimationFolder, "Result");
            if (clips.Count == 0)
            {
                Debug.LogError($"[AO] Result animation clips not found under {ResultAnimationFolder}.");
                return null;
            }

            AnimatorController controller = EnsureControllerAsset(ResultControllerPath);
            ConfigureCycleController(controller, clips);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static List<AnimationClip> LoadMenuClipsFromFolder(string folderPath, string sceneLabel)
        {
            List<AnimationClip> clips = new List<AnimationClip>();
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogError($"[AO] {sceneLabel} animation folder not found: {folderPath}");
                return clips;
            }

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folderPath });
            System.Array.Sort(guids, (a, b) => string.CompareOrdinal(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b)));

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ConfigureModelClipForMenuLoop(path);
                AnimationClip clip = LoadFirstClip(path);
                if (clip != null) clips.Add(clip);
            }

            Debug.Log($"[AO] {sceneLabel} menu animation clips loaded: {clips.Count} from {folderPath}");
            return clips;
        }

        private static AnimatorController EnsureControllerAsset(string path)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller != null) return controller;

            EnsureFolder(Path.GetDirectoryName(path));
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AssetDatabase.ImportAsset(path);
            return controller;
        }

        private static void ConfigureSingleStateController(AnimatorController controller, string stateName, AnimationClip clip)
        {
            if (controller == null || clip == null) return;

            AnimatorStateMachine stateMachine = ResetBaseLayer(controller);
            AnimatorState state = stateMachine.AddState(stateName, new Vector3(280f, 80f, 0f));
            state.motion = clip;
            state.writeDefaultValues = false;
            state.speed = 1f;
            stateMachine.defaultState = state;
        }

        private static void ConfigureCycleController(AnimatorController controller, IReadOnlyList<AnimationClip> clips)
        {
            if (controller == null || clips == null || clips.Count == 0) return;

            AnimatorStateMachine stateMachine = ResetBaseLayer(controller);
            AnimatorState previous = null;
            AnimatorState first = null;

            for (int i = 0; i < clips.Count; i++)
            {
                AnimationClip clip = clips[i];
                AnimatorState state = stateMachine.AddState(SanitizeStateName(clip.name), new Vector3(280f, 80f + i * 70f, 0f));
                state.motion = clip;
                state.writeDefaultValues = false;
                state.speed = 1f;

                if (first == null) first = state;
                if (previous != null) AddTimedTransition(previous, state);
                previous = state;
            }

            if (previous != null && first != null && previous != first)
            {
                AddTimedTransition(previous, first);
            }

            stateMachine.defaultState = first;
        }

        private static AnimatorStateMachine ResetBaseLayer(AnimatorController controller)
        {
            if (controller.layers == null || controller.layers.Length == 0)
            {
                controller.AddLayer("Base Layer");
            }

            AnimatorControllerLayer layer = controller.layers[0];
            layer.name = "Base Layer";
            layer.defaultWeight = 1f;
            layer.iKPass = false;
            controller.layers = new[] { layer };

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState state in stateMachine.states)
            {
                stateMachine.RemoveState(state.state);
            }

            foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines)
            {
                stateMachine.RemoveStateMachine(child.stateMachine);
            }

            foreach (AnimatorTransition transition in stateMachine.entryTransitions)
            {
                stateMachine.RemoveEntryTransition(transition);
            }

            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
            {
                stateMachine.RemoveAnyStateTransition(transition);
            }

            return stateMachine;
        }

        private static void AddTimedTransition(AnimatorState from, AnimatorState to)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = 0.95f;
            transition.hasFixedDuration = true;
            transition.duration = 0.25f;
            transition.canTransitionToSelf = false;
        }

        private static void ConfigureMenuScene(string scenePath, RuntimeAnimatorController controller, string sceneLabel)
        {
            if (controller == null)
            {
                Debug.LogWarning($"[AO] Skipping {sceneLabel} scene because no menu animation controller was created.");
                return;
            }

            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[AO] Menu scene not found: {scenePath}");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject character = FindOrCreateCharacter();
            if (character == null)
            {
                Debug.LogError($"[AO] Could not create menu character in {scenePath}.");
                return;
            }

            character.name = "PF_AO_Character_VRM10";
            character.transform.SetPositionAndRotation(MenuCharacterPosition, MenuCharacterRotation);
            character.transform.localScale = Vector3.one;

            Animator animator = character.GetComponent<Animator>() ?? character.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.enabled = true;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.speed = 1f;
                EditorUtility.SetDirty(animator);
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            }
            else
            {
                Debug.LogWarning($"[AO] Animator not found on menu character in {sceneLabel} scene.", character);
            }

            DisableSceneTrackingDrivers(character);
            ConfigureExpressionDriver(character, sceneLabel);
            PrefabUtility.RecordPrefabInstancePropertyModifications(character.transform);
            EditorUtility.SetDirty(character);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static GameObject FindOrCreateCharacter()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[AO] Character prefab not found: {CharacterPrefabPath}");
                return null;
            }

            foreach (Animator animator in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                GameObject root = PrefabUtility.GetNearestPrefabInstanceRoot(animator.gameObject);
                if (root == null) continue;

                string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
                if (sourcePath == CharacterPrefabPath) return root;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            return instance;
        }

        private static void DisableSceneTrackingDrivers(GameObject character)
        {
            foreach (OculusUpperBodyDriver driver in character.GetComponentsInChildren<OculusUpperBodyDriver>(true))
            {
                driver.enabled = false;
                SerializedObject serializedDriver = new SerializedObject(driver);
                SetObject(serializedDriver, "_xrTrackingRoot", null);
                SetObject(serializedDriver, "_hmd", null);
                SetObject(serializedDriver, "_leftController", null);
                SetObject(serializedDriver, "_rightController", null);
                SetObject(serializedDriver, "_leftHandTarget", null);
                SetObject(serializedDriver, "_rightHandTarget", null);
                SetObject(serializedDriver, "_riderRig", null);
                SetBool(serializedDriver, "_autoBindMissingSceneTargets", false);
                SetBool(serializedDriver, "_readMissingReferencesFromXRNodes", false);
                SetBool(serializedDriver, "_driveHead", false);
                SetBool(serializedDriver, "_driveChest", false);
                SetBool(serializedDriver, "_driveHandsWithAnimatorIk", false);
                serializedDriver.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(driver);
                PrefabUtility.RecordPrefabInstancePropertyModifications(driver);
            }

            foreach (MonoBehaviour behaviour in character.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;

                string typeName = behaviour.GetType().Name;
                if (typeName != "DVariantRiderRig" &&
                    typeName != "FeverHandEffect" &&
                    typeName != "CharacterTransformProbe" &&
                    typeName != "VrmRigPoseGuard" &&
                    typeName != "RigBuilder" &&
                    typeName != "Rig" &&
                    typeName != "TwoBoneIKConstraint")
                {
                    continue;
                }

                behaviour.enabled = false;
                EditorUtility.SetDirty(behaviour);
                PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
            }
        }

        private static void ConfigureExpressionDriver(GameObject character, string sceneLabel)
        {
            Component driver = GetCharacterExpressionDriver(character);
            if (driver == null)
            {
                Type driverType = FindCharacterExpressionDriverType();
                if (driverType != null) driver = character.AddComponent(driverType);
            }

            if (driver == null) return;
            if (driver is MonoBehaviour behaviour) behaviour.enabled = true;
            driver.GetType().GetMethod("ConfigureForSceneLabel")?.Invoke(driver, new object[] { sceneLabel });
            EditorUtility.SetDirty(driver);
            PrefabUtility.RecordPrefabInstancePropertyModifications(driver);
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

        private static AnimationClip LoadFirstClip(string path)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                {
                    return clip;
                }
            }

            return null;
        }

        private static void ConfigureModelClipForMenuLoop(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            bool changed = false;
            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                changed = true;
            }

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                changed = true;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips == null || clips.Length == 0)
            {
                Debug.LogWarning($"[AO] No animation clips found in menu model: {path}");
                return;
            }

            for (int i = 0; i < clips.Length; i++)
            {
                ModelImporterClipAnimation clip = clips[i];
                string expectedName = MenuClipNameFromPath(path);

                if (clips.Length == 1 && !string.IsNullOrWhiteSpace(expectedName) && clip.name != expectedName)
                {
                    clip.name = expectedName;
                    changed = true;
                }

                if (!clip.loopTime)
                {
                    clip.loopTime = true;
                    changed = true;
                }

                if (!clip.loopPose)
                {
                    clip.loopPose = true;
                    changed = true;
                }

                if (!clip.lockRootRotation)
                {
                    clip.lockRootRotation = true;
                    changed = true;
                }

                if (!clip.keepOriginalOrientation)
                {
                    clip.keepOriginalOrientation = true;
                    changed = true;
                }

                if (!clip.lockRootHeightY)
                {
                    clip.lockRootHeightY = true;
                    changed = true;
                }

                if (!clip.keepOriginalPositionY)
                {
                    clip.keepOriginalPositionY = true;
                    changed = true;
                }

                if (!clip.lockRootPositionXZ)
                {
                    clip.lockRootPositionXZ = true;
                    changed = true;
                }

                if (!clip.keepOriginalPositionXZ)
                {
                    clip.keepOriginalPositionXZ = true;
                    changed = true;
                }

                if (clip.heightFromFeet)
                {
                    clip.heightFromFeet = false;
                    changed = true;
                }

                if (clip.mirror)
                {
                    clip.mirror = false;
                    changed = true;
                }

                clips[i] = clip;
            }

            importer.clipAnimations = clips;
            if (!changed) return;

            importer.SaveAndReimport();
        }

        private static string MenuClipNameFromPath(string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(fileName)) return null;

            int marker = fileName.IndexOf('@');
            if (marker >= 0 && marker + 1 < fileName.Length)
            {
                return fileName.Substring(marker + 1);
            }

            return fileName;
        }

        private static string SanitizeStateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Sitting";
            return name.Replace("@", "_").Replace(" ", "_");
        }

        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;
            string normalized = folderPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(normalized)) return;

            string parent = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
            string name = Path.GetFileName(normalized);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(normalized))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void SetObject(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = value;
        }

        private static void SetBool(SerializedObject so, string propertyName, bool value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null) property.boolValue = value;
        }
    }
}
