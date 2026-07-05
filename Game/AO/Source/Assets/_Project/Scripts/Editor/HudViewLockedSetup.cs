using AO.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AO.Editor
{
    public static class HudViewLockedSetup
    {
        [MenuItem("AO/Setup/Make Oxygen Critical Pulse View Locked")]
        public static void MakeOxygenCriticalPulseViewLocked()
        {
            Scene originalScene = SceneManager.GetActiveScene();
            string originalScenePath = originalScene.path;
            bool changed = false;

            try
            {
                foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
                {
                    if (!buildScene.enabled || string.IsNullOrWhiteSpace(buildScene.path)) continue;
                    if (!buildScene.path.EndsWith("GamePlayScene.unity", System.StringComparison.OrdinalIgnoreCase)) continue;

                    Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                    HUDController hud = FindObject<HUDController>(scene);
                    if (hud == null)
                    {
                        Debug.LogError("[AO] HUDCanvas/HUDController was not found in GamePlayScene.");
                        continue;
                    }

                    Transform camera = Camera.main != null ? Camera.main.transform : FindByName(scene, "Main Camera");
                    SerializedObject so = new SerializedObject(hud);
                    so.FindProperty("_cameraTarget").objectReferenceValue = camera;
                    so.FindProperty("_faceCamera").boolValue = true;
                    so.FindProperty("_followCamera").boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    OxygenCriticalEffect effect = hud.GetComponent<OxygenCriticalEffect>();
                    if (effect == null)
                    {
                        Debug.LogError("[AO] OxygenCriticalEffect was not found on HUDCanvas.");
                        continue;
                    }

                    SerializedObject effectSo = new SerializedObject(effect);
                    effectSo.FindProperty("_cameraTarget").objectReferenceValue = camera;
                    effectSo.FindProperty("_followCamera").boolValue = true;
                    effectSo.FindProperty("_cameraDistance").floatValue = 1.15f;
                    effectSo.FindProperty("_cameraOffset").vector2Value = Vector2.zero;
                    SetBoolIfFound(effectSo, "_forceFullTargetRect", true);
                    SetFloatIfFound(effectSo, "_viewWidthOverscan", 1.6f);
                    SetFloatIfFound(effectSo, "_viewHeightOverscan", 2.25f);
                    SetBoolIfFound(effectSo, "_forceStretchedImage", true);
                    effectSo.ApplyModifiedPropertiesWithoutUndo();

                    ConfigurePulse(hud.transform as RectTransform);

                    EditorUtility.SetDirty(hud);
                    EditorUtility.SetDirty(effect);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    changed = true;
                }
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(originalScenePath))
                {
                    EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                }
            }

            if (changed)
            {
                Debug.Log("[AO] OxygenCriticalPulse is now view-locked. HUDCanvas stays in its scene-authored world-space position.");
            }
        }

        private static void SetBoolIfFound(SerializedObject target, string propertyName, bool value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property != null) property.boolValue = value;
        }

        private static void SetFloatIfFound(SerializedObject target, string propertyName, float value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property != null) property.floatValue = value;
        }

        private static void ConfigurePulse(RectTransform hud)
        {
            if (hud == null) return;

            RectTransform pulse = FindRect(hud, "OxygenCriticalPulse");
            if (pulse == null)
            {
                Debug.LogWarning("[AO] OxygenCriticalPulse was not found under HUDCanvas. No pulse object was created.");
                return;
            }

            Undo.RecordObject(pulse, "Configure Oxygen Critical Pulse");
            pulse.anchorMin = Vector2.zero;
            pulse.anchorMax = Vector2.one;
            pulse.offsetMin = Vector2.zero;
            pulse.offsetMax = Vector2.zero;
            pulse.pivot = new Vector2(0.5f, 0.5f);
            pulse.localRotation = Quaternion.identity;
            pulse.localScale = Vector3.one;
            pulse.SetAsLastSibling();

            Image image = pulse.GetComponent<Image>();
            if (image != null)
            {
                Undo.RecordObject(image, "Configure Oxygen Critical Pulse Image");
                image.raycastTarget = false;
                image.preserveAspect = false;
                image.type = Image.Type.Simple;
            }

            EditorUtility.SetDirty(pulse);
            if (image != null) EditorUtility.SetDirty(image);
        }

        private static T FindObject<T>(Scene scene) where T : Component
        {
            T[] objects = Resources.FindObjectsOfTypeAll<T>();
            foreach (T item in objects)
            {
                if (item != null && item.gameObject.scene == scene) return item;
            }

            return null;
        }

        private static Transform FindByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindRecursive(root.transform, name);
                if (found != null) return found;
            }

            return null;
        }

        private static RectTransform FindRect(Transform parent, string name)
        {
            Transform child = FindRecursive(parent, name);
            return child != null ? child.GetComponent<RectTransform>() : null;
        }

        private static Transform FindRecursive(Transform parent, string name)
        {
            if (parent == null) return null;
            if (parent.name == name) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindRecursive(parent.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
