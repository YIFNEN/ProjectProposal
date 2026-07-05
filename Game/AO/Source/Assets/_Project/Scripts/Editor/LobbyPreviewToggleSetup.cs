using System.IO;
using AO.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AO.Editor
{
    public static class LobbyPreviewToggleSetup
    {
        private const string LobbyScenePath = "Assets/_Project/Scenes/02_Lobby.unity";
        private const string LobbyUiPath = "Assets/_Project/Art/UI/Lobby";
        private const string PlayIconPath = LobbyUiPath + "/T_UI_Lobby_PreviewPlay_Icon.png";
        private const string PauseIconPath = LobbyUiPath + "/T_UI_Lobby_PreviewPause_Icon.png";

        [MenuItem("AO/Setup/Apply Lobby Preview Toggle")]
        public static void Apply()
        {
            EnsureIconSprite(PlayIconPath, true);
            EnsureIconSprite(PauseIconPath, false);
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            Sprite playSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayIconPath);
            Sprite pauseSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PauseIconPath);

            ConfigureControllerSprites(playSprite, pauseSprite);
            ConfigureScenePreviewButton(scene, playSprite);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[AO] Lobby preview toggle applied: preview no longer auto-starts, play/pause icons are assigned.");
        }

        public static void ApplyFromCommandLine()
        {
            Apply();
        }

        private static void ConfigureControllerSprites(Sprite playSprite, Sprite pauseSprite)
        {
            foreach (LobbyScreenController controller in Object.FindObjectsByType<LobbyScreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (controller == null || !controller.gameObject.scene.isLoaded) continue;

                SerializedObject so = new SerializedObject(controller);
                SetObject(so, "_previewPlaySprite", playSprite);
                SetObject(so, "_previewPauseSprite", pauseSprite);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
            }
        }

        private static void ConfigureScenePreviewButton(Scene scene, Sprite playSprite)
        {
            RectTransform playPanel = FindInSceneRect(scene, "SongPlayPanel");
            if (playPanel == null) return;

            Button previewButton = RuntimeUiFactory.EnsureButton(playPanel, "PreviewToggleButton", "PREVIEW", new Vector2(0f, 44f), new Vector2(360f, 42f), null);
            Place((RectTransform)previewButton.transform, new Vector2(0f, 44f), new Vector2(360f, 42f));

            Image buttonImage = previewButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.sprite = null;
                buttonImage.color = new Color(0.03f, 0.19f, 0.25f, 0.88f);
                buttonImage.raycastTarget = true;
                EditorUtility.SetDirty(buttonImage);
            }

            TMP_Text label = previewButton.transform.Find("Label") != null ? previewButton.transform.Find("Label").GetComponent<TMP_Text>() : null;
            if (label != null)
            {
                label.text = "PREVIEW";
                label.fontSize = 20f;
                label.fontSizeMax = 20f;
                label.fontSizeMin = 12f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = new Color(0.86f, 1f, 0.96f, 1f);
                Place(label.rectTransform, new Vector2(18f, 0f), new Vector2(230f, 34f));
                EditorUtility.SetDirty(label);
            }

            RectTransform iconRect = RuntimeUiFactory.EnsurePanel(previewButton.transform, "PreviewIcon", new Vector2(-124f, 0f), new Vector2(26f, 26f), Color.white);
            Place(iconRect, new Vector2(-124f, 0f), new Vector2(26f, 26f));
            iconRect.SetAsLastSibling();

            Image icon = iconRect.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = playSprite;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.color = Color.white;
                EditorUtility.SetDirty(icon);
            }

            EditorUtility.SetDirty(previewButton);
        }

        private static void EnsureIconSprite(string assetPath, bool playIcon)
        {
            Directory.CreateDirectory(Path.GetFullPath(LobbyUiPath));

            string absolutePath = Path.GetFullPath(assetPath);
            if (!File.Exists(absolutePath))
            {
                Texture2D texture = CreateIconTexture(playIcon);
                File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 256;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static Texture2D CreateIconTexture(bool playIcon)
        {
            const int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color fill = new Color(0.78f, 1f, 0.92f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size;
                    float ny = (y + 0.5f) / size;
                    bool inside = playIcon ? IsInsidePlayIcon(nx, ny) : IsInsidePauseIcon(nx, ny);
                    texture.SetPixel(x, y, inside ? fill : clear);
                }
            }

            texture.Apply();
            return texture;
        }

        private static bool IsInsidePlayIcon(float x, float y)
        {
            Vector2 a = new Vector2(0.34f, 0.24f);
            Vector2 b = new Vector2(0.34f, 0.76f);
            Vector2 c = new Vector2(0.78f, 0.5f);
            Vector2 p = new Vector2(x, y);

            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        private static bool IsInsidePauseIcon(float x, float y)
        {
            return IsInsideRoundedBar(x, y, 0.32f, 0.45f) || IsInsideRoundedBar(x, y, 0.55f, 0.68f);
        }

        private static bool IsInsideRoundedBar(float x, float y, float minX, float maxX)
        {
            const float minY = 0.25f;
            const float maxY = 0.75f;
            const float radius = 0.06f;

            if (x >= minX && x <= maxX && y >= minY + radius && y <= maxY - radius) return true;
            if (y >= minY && y <= maxY && x >= minX + radius && x <= maxX - radius) return true;

            Vector2 bottomLeft = new Vector2(minX + radius, minY + radius);
            Vector2 bottomRight = new Vector2(maxX - radius, minY + radius);
            Vector2 topLeft = new Vector2(minX + radius, maxY - radius);
            Vector2 topRight = new Vector2(maxX - radius, maxY - radius);
            Vector2 p = new Vector2(x, y);
            return Vector2.Distance(p, bottomLeft) <= radius ||
                   Vector2.Distance(p, bottomRight) <= radius ||
                   Vector2.Distance(p, topLeft) <= radius ||
                   Vector2.Distance(p, topRight) <= radius;
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }

        private static RectTransform FindInSceneRect(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                RectTransform found = FindRecursive(root.transform, name);
                if (found != null) return found;
            }

            return null;
        }

        private static RectTransform FindRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent as RectTransform;

            for (int i = 0; i < parent.childCount; i++)
            {
                RectTransform found = FindRecursive(parent.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        private static void SetObject(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = value;
        }

        private static void Place(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            if (rect == null) return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }
    }
}
