using AO.Notes;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AO.Editor
{
    public static class FishStrokeHeartSetup
    {
        private const string FishWrapperPrefabPath = "Assets/_Project/Prefabs/Notes/FishNote_Wrapper.prefab";
        private const string FishVisualPrefabFolder = "Assets/_Project/Prefabs/Environment/Imported/Fish/Note";
        private const string HeartSpritePath = "Assets/_Project/Art/VFX/Textures/P_Heart.png";
        private const string SuccessVfxPath = "Assets/_Project/Prefabs/HUD/FX_Stroke_Heart.prefab";

        [MenuItem("AO/Setup/Apply Fish Stroke Heart Progress")]
        public static void Apply()
        {
            Sprite heartSprite = PrepareHeartSprite();
            GameObject successVfx = AssetDatabase.LoadAssetAtPath<GameObject>(SuccessVfxPath);

            GameObject root = PrefabUtility.LoadPrefabContents(FishWrapperPrefabPath);
            try
            {
                FishNote fish = root.GetComponent<FishNote>();
                if (fish == null)
                {
                    Debug.LogError("[AO] FishNote component is missing from FishNote_Wrapper.prefab.");
                    return;
                }

                RemoveWrapperDebugMesh(root);
                GameObject heart = EnsureHeartObject(root.transform, heartSprite);
                Transform visualRoot = EnsureChild(root.transform, "VisualRoot", Vector3.zero, Quaternion.identity, Vector3.one);
                Transform headAnchor = EnsureChild(root.transform, "HeadAnchor", new Vector3(0f, 0f, 0.25f), Quaternion.identity, Vector3.one);
                ConfigureFishSerializedFields(fish, heart.transform, heart.GetComponent<Image>(), successVfx, visualRoot, headAnchor);

                PrefabUtility.SaveAsPrefabAsset(root, FishWrapperPrefabPath);
                Debug.Log("[AO] Fish stroke heart progress and default schooling fish candidates applied to FishNote_Wrapper.prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Sprite PrepareHeartSprite()
        {
            TextureImporter importer = AssetImporter.GetAtPath(HeartSpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(HeartSpritePath);
        }

        private static void RemoveWrapperDebugMesh(GameObject root)
        {
            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            if (renderer != null) Object.DestroyImmediate(renderer, true);

            MeshFilter filter = root.GetComponent<MeshFilter>();
            if (filter != null) Object.DestroyImmediate(filter, true);
        }

        private static GameObject EnsureHeartObject(Transform root, Sprite heartSprite)
        {
            Transform existing = root.Find("StrokeHeartProgress");
            GameObject heart = existing != null
                ? existing.gameObject
                : new GameObject("StrokeHeartProgress", typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer), typeof(Image));

            heart.transform.SetParent(root, false);
            heart.layer = root.gameObject.layer;
            heart.SetActive(false);

            RectTransform rect = heart.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localPosition = new Vector3(0f, 0.08f, 0f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(1f, 1f);

            Canvas canvas = heart.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 50;

            Image image = heart.GetComponent<Image>();
            image.sprite = heartSprite;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Bottom;
            image.fillClockwise = true;
            image.fillAmount = 0f;
            image.color = new Color(1f, 0.08f, 0.12f, 0.9f);
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.maskable = false;

            EditorUtility.SetDirty(heart);
            return heart;
        }

        private static Transform EnsureChild(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            Transform existing = parent.Find(name);
            Transform child = existing;
            if (child == null)
            {
                GameObject go = new GameObject(name);
                child = go.transform;
                child.SetParent(parent, false);
            }

            child.localPosition = localPosition;
            child.localRotation = localRotation;
            child.localScale = localScale;
            child.gameObject.layer = parent.gameObject.layer;
            EditorUtility.SetDirty(child);
            return child;
        }

        private static void ConfigureFishSerializedFields(FishNote fish, Transform heartRoot, Image heartImage, GameObject successVfx, Transform visualRoot, Transform headAnchor)
        {
            SerializedObject so = new SerializedObject(fish);
            SetObject(so, "_visualRoot", visualRoot);
            SetObject(so, "_headAnchor", headAnchor);
            SetObject(so, "_successVfxPrefab", successVfx);
            SetFloat(so, "_successVfxScale", 0.34f);
            SetVector3(so, "_successVfxLocalOffset", new Vector3(0f, 0.04f, 0f));
            SetBool(so, "_successVfxFollowHeadAnchor", true);
            SetFloat(so, "_successVfxWorldUpOffset", 0.24f);
            SetFloat(so, "_successVfxLifetime", 1.8f);
            SetBool(so, "_emitApproachHearts", true);
            SetObject(so, "_approachHeartPrefab", successVfx);
            SetFloat(so, "_approachHeartStartT", 0.08f);
            SetFloat(so, "_approachHeartEndT", 0.92f);
            SetFloat(so, "_approachHeartInterval", 0.32f);
            SetFloat(so, "_approachHeartMinScale", 0.08f);
            SetFloat(so, "_approachHeartMaxScale", 0.18f);
            SetVector3(so, "_approachHeartLocalOffset", new Vector3(0f, 0.08f, -0.08f));
            SetFloat(so, "_approachHeartWorldUpOffset", 0.1f);
            SetVector3(so, "_approachHeartRandomWorldJitter", new Vector3(0.06f, 0.05f, 0.04f));
            SetFloat(so, "_approachHeartLifetime", 1.1f);
            SetBool(so, "_playVisualPrefabAnimations", true);
            SetObject(so, "_heartProgressRoot", heartRoot);
            SetObject(so, "_heartProgressImage", heartImage);
            SetObject(so, "_heartProgressSprite", null);
            SetVector3(so, "_heartProgressLocalOffset", new Vector3(0f, 0.08f, 0f));
            SetBool(so, "_heartProgressFollowHeadAnchor", true);
            SetFloat(so, "_heartProgressWorldUpOffset", 0.32f);
            SetColor(so, "_heartProgressColor", new Color(1f, 0.08f, 0.12f, 0.9f));
            SetFloat(so, "_heartProgressMinScale", 0.16f);
            SetFloat(so, "_heartProgressMaxScale", 0.38f);
            SetBool(so, "_heartProgressBillboard", true);
            SetBool(so, "_resetProgressWhenContactLost", true);
            SetBool(so, "_exitAfterStrokeSuccess", true);
            SetFloat(so, "_successHoldSeconds", 0.12f);
            SetFloat(so, "_successExitSeconds", 0.32f);
            SetBool(so, "_scaleStrokeOverlapWithVisualScale", true);
            SetFloat(so, "_minimumStrokeOverlapScale", 1f);
            SetBool(so, "_randomizeVisualOnSpawn", true);
            ConfigureDefaultVisualCandidates(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fish);
        }

        private static void ConfigureDefaultVisualCandidates(SerializedObject so)
        {
            SerializedProperty candidates = so.FindProperty("_visualCandidates");
            if (candidates == null) return;

            if (HasAuthoredVisualCandidate(candidates))
            {
                Debug.Log("[AO] FishNote visual candidates already exist; keeping authored candidate list.");
                return;
            }

            DefaultFishVisualCandidate[] defaults =
            {
                new DefaultFishVisualCandidate("blue_note", "Blue_Fish_03.prefab", Vector3.zero, new Vector3(0f, 0f, 0.55f), 0.24f, 0.06f),
                new DefaultFishVisualCandidate("cyan_note", "Cyan_Fish.prefab", Vector3.zero, new Vector3(0f, 0f, 0.55f), 0.24f, 0.06f),
                new DefaultFishVisualCandidate("pink_note", "Pink_Fish.prefab", Vector3.zero, new Vector3(0f, 0f, 0.55f), 0.24f, 0.06f),
                new DefaultFishVisualCandidate("red_note", "Red_Fish.prefab", Vector3.zero, new Vector3(0f, 0f, 0.55f), 0.24f, 0.06f),
                new DefaultFishVisualCandidate("violet_note", "Violet_Fish.prefab", Vector3.zero, new Vector3(0f, 0f, 0.55f), 0.24f, 0.06f),
                new DefaultFishVisualCandidate("black_white_note", "Black_White_Fish.prefab", Vector3.zero, new Vector3(0f, 0f, 0.6f), 0.26f, 0.06f),
                new DefaultFishVisualCandidate("dolphin_note", "dolphin.prefab", new Vector3(0f, -90f, 0f), new Vector3(0f, 0f, 0.14f), 0.26f, 0.07f),
                new DefaultFishVisualCandidate("whale_note", "Whale.prefab", new Vector3(0f, -90f, 0f), new Vector3(0f, 0f, 0.9f), 0.34f, 0.08f)
            };

            System.Collections.Generic.List<DefaultFishVisualCandidate> loaded =
                new System.Collections.Generic.List<DefaultFishVisualCandidate>(defaults.Length);

            for (int i = 0; i < defaults.Length; i++)
            {
                string path = $"{FishVisualPrefabFolder}/{defaults[i].PrefabName}";
                defaults[i].Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (defaults[i].Prefab == null)
                {
                    Debug.LogWarning($"[AO] Missing default FishNote visual candidate prefab: {path}");
                    continue;
                }

                loaded.Add(defaults[i]);
            }

            candidates.arraySize = loaded.Count;
            for (int i = 0; i < loaded.Count; i++)
            {
                SerializedProperty item = candidates.GetArrayElementAtIndex(i);
                SetChildString(item, "Id", loaded[i].Id);
                SetChildObject(item, "Prefab", loaded[i].Prefab);
                SetChildVector3(item, "LocalPosition", Vector3.zero);
                SetChildVector3(item, "LocalEulerAngles", loaded[i].LocalEulerAngles);
                SetChildVector3(item, "LocalScale", Vector3.one);
                SetChildVector3(item, "HeadLocalPosition", loaded[i].HeadLocalPosition);
                SetChildFloat(item, "StrokeOverlapRadius", loaded[i].StrokeOverlapRadius);
                SetChildFloat(item, "StrokeOverlapPadding", loaded[i].StrokeOverlapPadding);
            }

            Debug.Log($"[AO] Added {loaded.Count} default Note-folder FishNote visual candidates.");
        }

        private static bool HasAuthoredVisualCandidate(SerializedProperty candidates)
        {
            for (int i = 0; i < candidates.arraySize; i++)
            {
                SerializedProperty item = candidates.GetArrayElementAtIndex(i);
                SerializedProperty prefab = item.FindPropertyRelative("Prefab");
                if (prefab != null && prefab.objectReferenceValue != null) return true;

                SerializedProperty id = item.FindPropertyRelative("Id");
                if (id != null && !string.IsNullOrWhiteSpace(id.stringValue)) return true;
            }

            return false;
        }

        private static void SetChildString(SerializedProperty parent, string name, string value)
        {
            SerializedProperty prop = parent.FindPropertyRelative(name);
            if (prop != null) prop.stringValue = value;
        }

        private static void SetChildObject(SerializedProperty parent, string name, Object value)
        {
            SerializedProperty prop = parent.FindPropertyRelative(name);
            if (prop != null) prop.objectReferenceValue = value;
        }

        private static void SetChildFloat(SerializedProperty parent, string name, float value)
        {
            SerializedProperty prop = parent.FindPropertyRelative(name);
            if (prop != null) prop.floatValue = value;
        }

        private static void SetChildVector3(SerializedProperty parent, string name, Vector3 value)
        {
            SerializedProperty prop = parent.FindPropertyRelative(name);
            if (prop != null) prop.vector3Value = value;
        }

        private struct DefaultFishVisualCandidate
        {
            public readonly string Id;
            public readonly string PrefabName;
            public readonly Vector3 LocalEulerAngles;
            public readonly Vector3 HeadLocalPosition;
            public readonly float StrokeOverlapRadius;
            public readonly float StrokeOverlapPadding;
            public GameObject Prefab;

            public DefaultFishVisualCandidate(
                string id,
                string prefabName,
                Vector3 localEulerAngles,
                Vector3 headLocalPosition,
                float strokeOverlapRadius,
                float strokeOverlapPadding)
            {
                Id = id;
                PrefabName = prefabName;
                LocalEulerAngles = localEulerAngles;
                HeadLocalPosition = headLocalPosition;
                StrokeOverlapRadius = strokeOverlapRadius;
                StrokeOverlapPadding = strokeOverlapPadding;
                Prefab = null;
            }
        }

        private static void SetObject(SerializedObject so, string name, Object value)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop != null) prop.objectReferenceValue = value;
        }

        private static void SetFloat(SerializedObject so, string name, float value)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop != null) prop.floatValue = value;
        }

        private static void SetBool(SerializedObject so, string name, bool value)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop != null) prop.boolValue = value;
        }

        private static void SetVector3(SerializedObject so, string name, Vector3 value)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop != null) prop.vector3Value = value;
        }

        private static void SetColor(SerializedObject so, string name, Color value)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop != null) prop.colorValue = value;
        }
    }
}
