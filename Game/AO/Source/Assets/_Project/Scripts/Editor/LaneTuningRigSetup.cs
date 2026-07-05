using AO.Rhythm;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.Editor
{
    public static class LaneTuningRigSetup
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const string HitAnchorName = "HitAnchor";
        private const string SpawnAnchorName = "SpawnAnchor";
        private const string RigName = "LaneTuningRig";
        private const string SharedSpawnPointName = "SharedSpawnPoint";

        [MenuItem("AO/Setup/Apply Lane Tuning Handles")]
        public static void ApplyToGameplayScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            NoteSpawner spawner = FindSceneComponent<NoteSpawner>(scene);
            if (spawner == null)
            {
                Debug.LogError($"[AO] NoteSpawner was not found in {GameplayScenePath}.");
                return;
            }

            GameObject hitAnchorObject = FindSceneGameObject(scene, HitAnchorName);
            if (hitAnchorObject == null)
            {
                Debug.LogError($"[AO] {HitAnchorName} was not found in {GameplayScenePath}.");
                return;
            }

            GameObject spawnAnchorObject = FindSceneGameObject(scene, SpawnAnchorName);
            Transform hitAnchor = hitAnchorObject.transform;
            Transform spawnAnchor = spawnAnchorObject != null ? spawnAnchorObject.transform : null;
            JudgementFrame frame = FindSceneComponent<JudgementFrame>(scene);
            LanePathGuide pathGuide = spawner.GetComponent<LanePathGuide>() ?? FindSceneComponent<LanePathGuide>(scene);
            Transform debugHitPadsRoot = hitAnchor.Find("DebugHitPads");

            GameObject rigObject = FindSceneGameObject(scene, RigName);
            if (rigObject == null)
            {
                rigObject = new GameObject(RigName);
            }

            rigObject.transform.SetParent(hitAnchor, false);
            rigObject.transform.localPosition = Vector3.zero;
            rigObject.transform.localRotation = Quaternion.identity;
            rigObject.transform.localScale = Vector3.one;

            Vector3 hitBase = hitAnchor.position;
            Vector3 spawnBase = spawnAnchor != null ? spawnAnchor.position : rigObject.transform.position;

            Transform laneUp = EnsureHandle(
                rigObject.transform,
                "Lane_Up",
                hitBase + spawner.GetConfiguredLaneOffset(Lane.Up),
                "Up / Upper Left",
                new Color(0.3f, 0.78f, 1f, 0.88f));

            Transform laneDown = EnsureHandle(
                rigObject.transform,
                "Lane_Down",
                hitBase + spawner.GetConfiguredLaneOffset(Lane.Down),
                "Down / Lower Right",
                new Color(0.28f, 0.52f, 1f, 0.88f));

            Transform laneLeft = EnsureHandle(
                rigObject.transform,
                "Lane_Left",
                hitBase + spawner.GetConfiguredLaneOffset(Lane.Left),
                "Left / Lower Left",
                new Color(0.62f, 0.95f, 0.46f, 0.88f));

            Transform laneRight = EnsureHandle(
                rigObject.transform,
                "Lane_Right",
                hitBase + spawner.GetConfiguredLaneOffset(Lane.Right),
                "Right / Upper Right",
                new Color(1f, 0.66f, 0.32f, 0.88f));

            Transform laneCenter = EnsureHandle(
                rigObject.transform,
                "Lane_Center",
                hitBase + spawner.GetConfiguredLaneOffset(Lane.Center),
                "Center",
                new Color(1f, 0.9f, 0.28f, 0.9f));

            Transform sharedSpawnPoint = EnsureHandle(
                rigObject.transform,
                SharedSpawnPointName,
                spawnBase + spawner.SharedSpawnOffset,
                "Shared Spawn",
                new Color(0.78f, 0.55f, 1f, 0.86f));

            LaneTuningRig rig = rigObject.GetComponent<LaneTuningRig>();
            if (rig == null) rig = rigObject.AddComponent<LaneTuningRig>();

            rig.ConfigureTargets(
                hitAnchor,
                spawnAnchor,
                spawner,
                frame,
                pathGuide,
                debugHitPadsRoot,
                laneUp,
                laneDown,
                laneLeft,
                laneRight,
                laneCenter,
                sharedSpawnPoint);
            rig.ApplyHandlesToTargets();

            EditorUtility.SetDirty(rigObject);
            EditorUtility.SetDirty(rig);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[AO] Lane tuning handles applied. Move HitAnchor/LaneTuningRig/Lane_* in the Scene view to tune note lanes.");
        }

        [MenuItem("AO/Setup/Sync Lane Offsets From Lane Handles")]
        public static void SyncFromHandles()
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            LaneTuningRig rig = FindSceneComponent<LaneTuningRig>(scene);
            if (rig == null)
            {
                Debug.LogError("[AO] LaneTuningRig was not found. Run AO/Setup/Apply Lane Tuning Handles first.");
                return;
            }

            rig.ApplyHandlesToTargets();
            EditorUtility.SetDirty(rig);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[AO] Lane offsets synced from LaneTuningRig handles.");
        }

        private static Transform EnsureHandle(Transform parent, string name, Vector3 worldPosition, string label, Color color)
        {
            Transform existing = parent.Find(name);
            GameObject handleObject = existing != null ? existing.gameObject : new GameObject(name);
            handleObject.transform.SetParent(parent, true);
            handleObject.transform.position = worldPosition;
            handleObject.transform.localRotation = Quaternion.identity;
            handleObject.transform.localScale = Vector3.one;

            LaneTuningHandle handle = handleObject.GetComponent<LaneTuningHandle>();
            if (handle == null) handle = handleObject.AddComponent<LaneTuningHandle>();
            handle.Configure(label, color, 0.055f);

            EditorUtility.SetDirty(handleObject);
            EditorUtility.SetDirty(handle);
            return handleObject.transform;
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T component = roots[i].GetComponentInChildren<T>(true);
                if (component != null) return component;
            }

            return null;
        }

        private static GameObject FindSceneGameObject(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform found = FindDeepChild(roots[i].transform, objectName);
                if (found != null) return found.gameObject;
            }

            return null;
        }

        private static Transform FindDeepChild(Transform root, string childName)
        {
            if (root.name == childName) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeepChild(root.GetChild(i), childName);
                if (found != null) return found;
            }

            return null;
        }
    }
}
