using AO.Rhythm;
using AO.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace AO.Editor
{
    public static class FinalUiSpriteSetup
    {
        private const string TitleScenePath = "Assets/_Project/Scenes/01_Title.unity";
        private const string LobbyScenePath = "Assets/_Project/Scenes/02_Lobby.unity";
        private const string GameplayScenePath = "Assets/_Project/Scenes/GamePlayScene.unity";
        private const string ResultScenePath = "Assets/_Project/Scenes/04_Result.unity";

        private const string GameplayUiPath = "Assets/_Project/Art/UI/GamePlayScene";
        private const string LobbyUiPath = "Assets/_Project/Art/UI/Lobby";
        private const string SettingsUiPath = "Assets/_Project/Art/UI/Settings";
        private const string ResultUiPath = "Assets/_Project/Art/UI/Result";
        private const string RidiFontPath = "Assets/_Project/Art/UI/GamePlayScene/RIDIBatang.otf";
        private const string RidiTmpFontPath = "Assets/_Project/Art/UI/GamePlayScene/RIDIBatang_TMP.asset";
        private const string OxygenBubbleMaterialPath = "Assets/_Project/Art/UI/Materials/M_OxygenGaugeBubble.mat";
        private const string OxygenFrameSpritePath = GameplayUiPath + "/T_AO_UI_OxygenTube_RefGlass_Alpha_RawPreserve_NoRed_C01.png";
        private const string FeverFrameAlphaMaskPath = GameplayUiPath + "/T_AO_UI_Fever_FrameAlphaMask.png";
        private const string HitTargetCenterSpritePath = GameplayUiPath + "/T_AO_UI_CenterDiamondMarker_RefGlass_Alpha_RawPreserve_NoRed_C01.png";
        private const string HitAnchorArrowUpperLeftSpritePath = GameplayUiPath + "/T_AO_UI_HitAnchorArrow_UpperLeft.png";
        private const string HitAnchorArrowUpperRightSpritePath = GameplayUiPath + "/T_AO_UI_HitAnchorArrow_UpperRight.png";
        private const string HitAnchorArrowLowerLeftSpritePath = GameplayUiPath + "/T_AO_UI_HitAnchorArrow_LowerLeft.png";
        private const string HitAnchorArrowLowerRightSpritePath = GameplayUiPath + "/T_AO_UI_HitAnchorArrow_LowerRight.png";
        private const bool PreserveDecorativeSpriteAspect = true;

        private static TMP_FontAsset _uiFont;

        public static void ApplyFinalUiSprites()
        {
            ConfigureUiSpriteImports();
            _uiFont = EnsureRidiTmpFontAsset();

            ApplyTitleScene();
            ApplyLobbyScene();
            ApplyGameplayScene();
            ApplyResultScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AO] Final UI sprites applied. Existing gameplay and menu object names were preserved.");
        }

        public static void ApplyResultUiFromCommandLine()
        {
            ConfigureSpritesInFolder(ResultUiPath);
            _uiFont = EnsureRidiTmpFontAsset();
            ApplyResultScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AO] Result UI layout applied.");
        }

        [MenuItem("AO/Setup/Apply Oxygen Bar UI Only")]
        public static void ApplyOxygenBarUiOnly()
        {
            ConfigureSpritesInFolder(GameplayUiPath);

            GameObject hud = FindGameObject("HUDCanvas");
            if (hud == null)
            {
                Debug.LogWarning("[AO] HUDCanvas was not found in the active scene. Open GamePlayScene before applying Oxygen Bar UI only.");
                return;
            }

            GameObject oxygen = FindDirect(hud.transform, "OxygenBar");
            if (oxygen == null)
            {
                Debug.LogWarning("[AO] OxygenBar was not found under HUDCanvas.");
                return;
            }

            ApplyOxygenBarUi(oxygen);
            GameObject fever = FindDirect(hud.transform, "FeverGauge");
            ApplyFeverOutlineGauge(fever, oxygen);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[AO] Oxygen Bar and Fever outline gauge UI updated only in the active scene. Other UI objects were not touched.");
        }

        public static void ApplyGameplayOxygenFeverGaugeFromCommandLine()
        {
            ConfigureSpritesInFolder(GameplayUiPath);
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            GameObject hud = FindGameObject("HUDCanvas");
            if (hud == null)
            {
                Debug.LogWarning("[AO] HUDCanvas was not found in GamePlayScene.");
                return;
            }

            GameObject oxygen = FindDirect(hud.transform, "OxygenBar");
            if (oxygen != null) ApplyOxygenBarUi(oxygen);

            GameObject fever = FindDirect(hud.transform, "FeverGauge");
            ApplyFeverOutlineGauge(fever, oxygen);

            SaveActiveScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AO] Gameplay Oxygen/Fever gauge UI applied from command line.");
        }

        [MenuItem("AO/Setup/Apply Hit Target Anchor Arrows")]
        public static void ApplyHitTargetAnchorArrows()
        {
            ConfigureSpritesInFolder(GameplayUiPath);
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            ReplaceJudgementFrameWithSprites();
            SaveActiveScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AO] HitTargetCanvas updated: center marker plus four hit-anchor arrows.");
        }

        public static void ApplyHitTargetAnchorArrowsFromCommandLine()
        {
            ApplyHitTargetAnchorArrows();
        }

        [MenuItem("AO/Setup/Reimport UI Sprites Only")]
        public static void ReimportUiSpritesOnly()
        {
            ConfigureUiSpriteImports();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AO] UI sprite import settings refreshed only. Scene object positions and hierarchy were not changed.");
        }

        [MenuItem("AO/Setup/Repair RIDIBatang TMP Font Asset")]
        public static void RepairRidiTmpFontAsset()
        {
            _uiFont = RecreateRidiTmpFontAsset();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(_uiFont != null
                ? "[AO] RIDIBatang TMP font asset repaired. Run Apply Final UI Sprites again if scene text still references the old asset."
                : "[AO] RIDIBatang TMP font asset repair failed. Check that RIDIBatang.otf exists.");
        }

        [MenuItem("AO/Setup/Preserve UI Sprite Aspect In Open Scenes")]
        public static void PreserveUiSpriteAspectInOpenScenes()
        {
            int changed = 0;
            foreach (Image image in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!image.gameObject.scene.isLoaded || image.sprite == null) continue;

                bool preserve = ShouldPreserveAspect(image.gameObject, image.sprite);
                if (image.preserveAspect == preserve) continue;

                image.preserveAspect = preserve;
                EditorUtility.SetDirty(image);
                EditorSceneManager.MarkSceneDirty(image.gameObject.scene);
                changed++;
            }

            Debug.Log($"[AO] Preserve Aspect updated on {changed} UI Images in open scenes.");
        }

        private static void ConfigureUiSpriteImports()
        {
            ConfigureSpritesInFolder(GameplayUiPath);
            ConfigureSpritesInFolder(LobbyUiPath);
            ConfigureSpritesInFolder(SettingsUiPath);
            ConfigureSpritesInFolder(ResultUiPath);
        }

        private static TMP_FontAsset EnsureRidiTmpFontAsset()
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RidiTmpFontPath);
            if (IsUsableTmpFontAsset(existing)) return existing;

            return RecreateRidiTmpFontAsset();
        }

        private static TMP_FontAsset RecreateRidiTmpFontAsset()
        {
            AssetDatabase.DeleteAsset(RidiTmpFontPath);
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(RidiFontPath);
            if (sourceFont == null)
            {
                Debug.LogWarning($"[AO] Missing UI font: {RidiFontPath}");
                return null;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);

            fontAsset.name = "RIDIBatang_TMP";
            AssetDatabase.CreateAsset(fontAsset, RidiTmpFontPath);
            fontAsset.hideFlags = HideFlags.None;
            AddSubAssetIfNeeded(fontAsset.material, fontAsset);
            if (fontAsset.atlasTextures != null)
            {
                foreach (Texture2D atlas in fontAsset.atlasTextures)
                {
                    AddSubAssetIfNeeded(atlas, fontAsset);
                }
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(RidiTmpFontPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RidiTmpFontPath);
        }

        private static bool IsUsableTmpFontAsset(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null) return false;

            try
            {
                if (fontAsset.material == null) return false;
                if (fontAsset.atlasTexture == null) return false;
                if (!IsLiveTexture(fontAsset.atlasTexture)) return false;

                Texture2D[] atlases = fontAsset.atlasTextures;
                if (atlases == null || atlases.Length == 0) return false;
                foreach (Texture2D atlas in atlases)
                {
                    if (!IsLiveTexture(atlas)) return false;
                }
            }
            catch (System.Exception e) when (e is MissingReferenceException || e is UnassignedReferenceException || e is System.NullReferenceException)
            {
                return false;
            }

            return true;
        }

        private static bool IsLiveTexture(Texture2D texture)
        {
            if (texture == null) return false;
            try
            {
                _ = texture.width;
                _ = texture.height;
                _ = texture.isReadable;
            }
            catch (System.Exception e) when (e is MissingReferenceException || e is UnassignedReferenceException || e is System.NullReferenceException)
            {
                return false;
            }

            return true;
        }

        private static void AddSubAssetIfNeeded(Object subAsset, Object mainAsset)
        {
            if (subAsset == null || mainAsset == null) return;
            if (AssetDatabase.Contains(subAsset)) return;
            subAsset.hideFlags = HideFlags.None;
            AssetDatabase.AddObjectToAsset(subAsset, mainAsset);
        }

        private static void ConfigureSpritesInFolder(string folder)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool changed = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    changed = true;
                }

                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    changed = true;
                }

                if (!importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    changed = true;
                }

                if (importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = false;
                    changed = true;
                }

                if (importer.wrapMode != TextureWrapMode.Clamp)
                {
                    importer.wrapMode = TextureWrapMode.Clamp;
                    changed = true;
                }

                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (settings.spriteMeshType != SpriteMeshType.FullRect)
                {
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    importer.SetTextureSettings(settings);
                    changed = true;
                }

                if (!importer.sRGBTexture)
                {
                    importer.sRGBTexture = true;
                    changed = true;
                }

                if (importer.filterMode != FilterMode.Bilinear)
                {
                    importer.filterMode = FilterMode.Bilinear;
                    changed = true;
                }

                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    changed = true;
                }

                if (importer.maxTextureSize < 4096)
                {
                    importer.maxTextureSize = 4096;
                    changed = true;
                }

                changed |= ConfigureDefaultTexturePlatform(importer);
                changed |= ConfigureNamedTexturePlatform(importer, "Standalone");
                changed |= ConfigureNamedTexturePlatform(importer, "Android");

                if (importer.npotScale != TextureImporterNPOTScale.None)
                {
                    importer.npotScale = TextureImporterNPOTScale.None;
                    changed = true;
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }
        }

        private static bool ConfigureDefaultTexturePlatform(TextureImporter importer)
        {
            TextureImporterPlatformSettings settings = importer.GetDefaultPlatformTextureSettings();
            bool changed = false;
            if (settings.maxTextureSize != 4096)
            {
                settings.maxTextureSize = 4096;
                changed = true;
            }

            if (settings.textureCompression != TextureImporterCompression.Uncompressed)
            {
                settings.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (changed) importer.SetPlatformTextureSettings(settings);
            return changed;
        }

        private static bool ConfigureNamedTexturePlatform(TextureImporter importer, string platformName)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platformName);
            bool changed = false;
            if (!settings.overridden)
            {
                settings.overridden = true;
                changed = true;
            }

            if (settings.maxTextureSize != 4096)
            {
                settings.maxTextureSize = 4096;
                changed = true;
            }

            if (settings.textureCompression != TextureImporterCompression.Uncompressed)
            {
                settings.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (changed) importer.SetPlatformTextureSettings(settings);
            return changed;
        }

        private static void ApplyTitleScene()
        {
            EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);

            GameObject titleCanvas = FindGameObject("TitleCanvas");
            if (titleCanvas != null)
            {
                GameObject startButton = FindDirect(titleCanvas.transform, "StartButton");
                Transform startSprite = startButton != null ? FindDirectTransform(startButton.transform, "Sprite") : null;
                Transform startLabel = startButton != null ? FindDirectTransform(startButton.transform, "Label") : null;
                Image startImage = ApplyImage(startSprite != null ? startSprite.gameObject : startButton, SpriteAt($"{LobbyUiPath}/T_UI_Title_StartButton_WaterPanel.png"), Color.white, true, true);
                SetRect(startSprite as RectTransform, Vector2.zero, new Vector2(330f, 96f));
                if (startLabel != null) startLabel.SetAsLastSibling();
                Button start = startButton != null ? startButton.GetComponent<Button>() : null;
                if (start != null && startImage != null) start.targetGraphic = startImage;
            }

            SaveActiveScene();
        }

        private static void ApplyLobbyScene()
        {
            EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);

            GameObject canvas = FindGameObject("LobbyCanvas");
            if (canvas != null)
            {
                ApplyButtonSprite(FindDirect(canvas.transform, "ExitButton"), SpriteAt($"{LobbyUiPath}/T_UI_Lobby_TopUtilityButton_OceanBlend.png"));
                Transform title = FindDirectTransform(canvas.transform, "Title");
                Transform titleFrame = FindDirectTransform(canvas.transform, "TitleFrame");
                if (titleFrame == null && title != null)
                {
                    titleFrame = FindDirectTransform(title, "TitleFrame") ?? FindDirectTransform(title, "GameObject");
                }
                if (titleFrame != null)
                {
                    titleFrame.name = "TitleFrame";
                    titleFrame.SetParent(canvas.transform, false);
                    if (title != null) titleFrame.SetSiblingIndex(title.GetSiblingIndex());
                    Vector2 titlePosition = title is RectTransform titleRect ? titleRect.anchoredPosition : new Vector2(0f, 315f);
                    ApplyImage(titleFrame.gameObject, SpriteAt($"{LobbyUiPath}/T_UI_Lobby_TitleFrame_UserProvided_Transparent.png"), new Color(1f, 1f, 1f, 0.9f), false, true);
                    SetRect(titleFrame as RectTransform, titlePosition, new Vector2(430f, 136f));
                }
                SetRect(FindRect(canvas.transform, "ExitButton"), new Vector2(590f, 318f), new Vector2(170f, 52f));

                ApplyArrowButton(canvas.transform, "PreviousSongButton", $"{LobbyUiPath}/T_UI_Lobby_Arrow_Left_OceanBlend.png", new Vector2(272f, 19f));
                ApplyArrowButton(canvas.transform, "NextSongButton", $"{LobbyUiPath}/T_UI_Lobby_Arrow_Right_OceanBlend.png", new Vector2(591f, 19f));
                SetRect(FindRect(canvas.transform, "PreviousSongButton"), new Vector2(272f, 19f), new Vector2(72f, 310f));
                SetRect(FindRect(canvas.transform, "NextSongButton"), new Vector2(591f, 19f), new Vector2(72f, 310f));

                Transform contentRoot = FindDirectTransform(canvas.transform, "SongContentRoot");
                Transform info = contentRoot != null ? FindDirectTransform(contentRoot, "SongInfoPanel") : null;
                Transform play = contentRoot != null ? FindDirectTransform(contentRoot, "SongPlayPanel") : null;

                if (info != null)
                {
                    RectTransform contentRootRect = contentRoot as RectTransform;
                    if (contentRootRect != null)
                    {
                        contentRootRect.anchoredPosition = new Vector2(98f, contentRootRect.anchoredPosition.y);
                    }
                    SetRectIfTiny(info as RectTransform, new Vector2(330f, 108f), new Vector2(510f, 348f));
                    Image accidentalInnerFrame = info.GetComponent<Image>();
                    if (accidentalInnerFrame != null) accidentalInnerFrame.enabled = false;

                    RectTransform glow = EnsureImageChild(info, "SelectedGlow", SpriteAt($"{LobbyUiPath}/T_UI_Lobby_SongCard_Frame_OceanBlend.png"), new Vector2(0f, 0f), new Vector2(548f, 386f), Color.white);
                    glow.SetAsFirstSibling();

                    SetTextStyle(FindDirectTransform(info, "SongIndex"), new Color(0.73f, 0.94f, 1f, 0.82f), 19f);
                    SetTextStyle(FindDirectTransform(info, "SongName"), Color.white, 27f);
                    SetTextStyle(FindDirectTransform(info, "SongRecord"), new Color(0.82f, 0.95f, 1f, 0.94f), 20f);
                    SetRectIfTiny(FindRect(info, "SongIndex"), new Vector2(0f, 122f), new Vector2(360f, 32f));
                    SetRectIfTiny(FindRect(info, "SongName"), new Vector2(0f, 64f), new Vector2(390f, 64f));
                    SetRectIfTiny(FindRect(info, "SongRecord"), new Vector2(0f, -58f), new Vector2(390f, 132f));
                    ConfigureLobbyThumbnailViewport(info);
                }

                if (play != null)
                {
                    SetRectIfTiny(play as RectTransform, new Vector2(330f, -174f), new Vector2(510f, 128f));
                    ApplyImage(play.gameObject, null, new Color(1f, 1f, 1f, 0f), false, false);

                    GameObject normal = FindDirect(play, "NormalButton");
                    GameObject eternal = FindDirect(play, "EternalButton");
                    ApplyButtonSprite(normal, SpriteAt($"{LobbyUiPath}/T_UI_Lobby_ModeBadge_Normal_OceanBlend.png"));
                    ApplyButtonSprite(eternal, SpriteAt($"{LobbyUiPath}/T_UI_Lobby_ModeBadge_Eternal_OceanBlend.png"));
                    SetRectIfTiny(normal != null ? normal.transform as RectTransform : null, new Vector2(-112f, -18f), new Vector2(190f, 72f));
                    SetRectIfTiny(eternal != null ? eternal.transform as RectTransform : null, new Vector2(112f, -18f), new Vector2(190f, 72f));

                    if (eternal != null)
                    {
                        RectTransform lockIcon = EnsureImageChild(eternal.transform, "EternalLockIcon", SpriteAt($"{LobbyUiPath}/T_UI_Lobby_LockIcon_Eternal_OceanBlend.png"), new Vector2(68f, 24f), new Vector2(38f, 38f), Color.white);
                        lockIcon.SetAsLastSibling();
                    }
                }
            }

            SaveActiveScene();
        }

        private static void ConfigureLobbyThumbnailViewport(Transform info)
        {
            RectTransform thumbnailFrame = FindRect(info, "ThumbnailFrame");
            RectTransform thumbnailImage = FindRect(info, "ThumbnailImage");
            if (thumbnailFrame == null || thumbnailImage == null) return;
            thumbnailFrame.localScale = new Vector3(2.15f, 2f, 1f);

            Transform decorativeFrame = thumbnailFrame.Find("Frame");
            if (decorativeFrame != null)
            {
                ApplyImage(decorativeFrame.gameObject, SpriteAt($"{LobbyUiPath}/T_UI_Lobby_ThumbnailFrame_OceanBlend.png"), Color.white, false, true);
                RectTransform decorativeFrameRect = decorativeFrame as RectTransform;
                if (decorativeFrameRect != null)
                {
                    decorativeFrameRect.sizeDelta = new Vector2(162f, 92f);
                    decorativeFrameRect.localScale = new Vector3(1.14f, 1.25f, 1f);
                }
            }

            Transform existing = thumbnailFrame.Find("ThumbnailViewport");
            GameObject viewportObject = existing != null
                ? existing.gameObject
                : new GameObject("ThumbnailViewport", typeof(RectTransform), typeof(ChamferedMaskGraphic), typeof(Mask));
            viewportObject.transform.SetParent(thumbnailFrame, false);

            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            SetRect(viewport, Vector2.zero, new Vector2(184f, 98f));
            RectMask2D rectangularMask = viewportObject.GetComponent<RectMask2D>();
            if (rectangularMask != null) rectangularMask.enabled = false;

            ChamferedMaskGraphic maskShape = viewportObject.GetComponent<ChamferedMaskGraphic>();
            if (maskShape == null) maskShape = viewportObject.AddComponent<ChamferedMaskGraphic>();
            maskShape.CornerCut = 9f;
            maskShape.color = Color.white;
            maskShape.raycastTarget = false;

            Mask mask = viewportObject.GetComponent<Mask>();
            if (mask == null) mask = viewportObject.AddComponent<Mask>();
            mask.enabled = true;
            mask.showMaskGraphic = false;
            viewport.SetAsFirstSibling();

            thumbnailImage.SetParent(viewport, false);
            thumbnailImage.anchoredPosition = Vector2.zero;
            Image thumbnail = thumbnailImage.GetComponent<Image>();
            if (thumbnail != null)
            {
                thumbnail.preserveAspect = false;
                thumbnail.maskable = true;
                thumbnail.raycastTarget = false;
            }

            AspectRatioFitter fitter = thumbnailImage.GetComponent<AspectRatioFitter>();
            if (fitter == null) fitter = thumbnailImage.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            RectTransform overlay = FindRect(thumbnailFrame, "Frame");
            if (overlay == null) return;
            Image overlayImage = overlay.GetComponent<Image>();
            if (overlayImage != null)
            {
                overlayImage.maskable = false;
                overlayImage.raycastTarget = false;
            }
            overlay.SetAsLastSibling();
        }

        private static void ApplyGameplayScene()
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            ApplyGameplayHud();
            ApplyGameplaySettings();
            ReplaceJudgementFrameWithSprites();

            SaveActiveScene();
        }

        private static void ApplyGameplayHud()
        {
            GameObject hud = FindGameObject("HUDCanvas");
            if (hud == null) return;

            GameObject oxygen = FindDirect(hud.transform, "OxygenBar");
            if (oxygen != null) ApplyOxygenBarUi(oxygen);

            GameObject fever = FindDirect(hud.transform, "FeverGauge");
            ApplyFeverOutlineGauge(fever, oxygen);

            GameObject score = FindDirect(hud.transform, "ScoreDisplay");
            if (score != null)
            {
                ApplyImage(score, SpriteAt($"{GameplayUiPath}/T_AO_UI_StatusFrameSmall_RefGlass_Alpha_RawPreserve_NoRed_C01.png"), Color.white, false, false);
                SetTextStyle(FindDirectTransform(score.transform, "Value"), Color.white, 30f);
            }

            GameObject combo = FindDirect(hud.transform, "ComboCounter");
            if (combo != null)
            {
                ApplyImage(combo, SpriteAt($"{GameplayUiPath}/T_AO_UI_TopCenterFrame_RefGlass_Alpha_RawPreserve_NoRed_C01.png"), Color.white, false, false);
                SetChildActive(combo.transform, "Top", false);
                SetChildActive(combo.transform, "Bottom", false);
                SetChildActive(combo.transform, "Left", false);
                SetChildActive(combo.transform, "Right", false);
                SetTextStyle(FindDirectTransform(combo.transform, "ComboText"), Color.white, 36f);
                SetTextStyle(FindDirectTransform(combo.transform, "MultiplierText"), new Color(0.78f, 0.94f, 1f, 0.92f), 22f);
            }

            GameObject judgement = FindDirect(hud.transform, "JudgementPopup");
            if (judgement != null)
            {
                RectTransform backplate = EnsureSiblingImageBefore(
                    judgement.transform,
                    "JudgementPopup_Backplate",
                    SpriteAt($"{GameplayUiPath}/T_UI_HUD_JudgementPopup_Backplate.png"),
                    new Color(1f, 1f, 1f, 0.86f));

                if (backplate != null)
                {
                    SetRectIfTiny(backplate, Vector2.zero, new Vector2(360f, 112f));
                }
            }
        }

        private static void ApplyOxygenBarUi(GameObject oxygen)
        {
            if (oxygen == null) return;

            ApplyImage(oxygen, null, new Color(1f, 1f, 1f, 0f), false, false);
            RectTransform oxygenRect = oxygen.transform as RectTransform;
            if (oxygenRect != null) oxygenRect.sizeDelta = new Vector2(80f, 240f);

            RectTransform fillMask = EnsureRectChild(oxygen.transform, "FillMask", Vector2.zero, Vector2.zero);
            Stretch(fillMask, new Vector2(9f, 18f), new Vector2(-9f, -18f));
            RectMask2D rectMask = fillMask.GetComponent<RectMask2D>();
            if (rectMask == null) rectMask = fillMask.gameObject.AddComponent<RectMask2D>();

            RectTransform water = EnsureImageChild(fillMask, "WaterBody_Image", SpriteAt($"{GameplayUiPath}/T_AO_UI_OxygenFill_WaterBody_VR_C01.png"), Vector2.zero, Vector2.zero, Color.white);
            Stretch(water, Vector2.zero, Vector2.zero);
            Image waterImage = water.GetComponent<Image>();
            waterImage.type = Image.Type.Filled;
            waterImage.fillMethod = Image.FillMethod.Vertical;
            waterImage.fillOrigin = (int)Image.OriginVertical.Bottom;
            waterImage.fillAmount = 1f;

            RectTransform legacyFill = FindRect(oxygen.transform, "Fill");
            if (legacyFill != null) legacyFill.gameObject.SetActive(false);

            RectTransform wave = EnsureImageChild(fillMask, "WaveHighlight_Image", SpriteAt($"{GameplayUiPath}/T_AO_UI_OxygenFill_WaveHighlight_Additive_C02.png"), new Vector2(0f, 150f), new Vector2(46f, 28f), new Color(1f, 1f, 1f, 0.82f));
            wave.SetAsLastSibling();

            RectTransform frame = EnsureImageChild(oxygen.transform, "Frame_Image", SpriteAt(OxygenFrameSpritePath), Vector2.zero, Vector2.zero, Color.white);
            Stretch(frame, Vector2.zero, Vector2.zero);
            frame.localScale = Vector3.one * 1.35f;
            Image frameImage = frame.GetComponent<Image>();
            if (frameImage != null)
            {
                frameImage.raycastTarget = false;
                frameImage.preserveAspect = true;
            }

            OrderOxygenGaugeVisuals(oxygen.transform);

            ParticleSystem bubbles = EnsureOxygenBubbleParticles(fillMask);
            OxygenBar bar = oxygen.GetComponent<OxygenBar>();
            if (bar != null)
            {
                SetObjectReference(bar, "_fillImage", waterImage);
                SetBool(bar, "_resizeFillRectToRatio", false);
            }

            Component visual = oxygen.GetComponent("OxygenGaugeVisual");
            if (visual == null)
            {
                System.Type visualType = System.Type.GetType("AO.UI.OxygenGaugeVisual, Assembly-CSharp");
                if (visualType != null) visual = oxygen.AddComponent(visualType);
            }

            if (visual != null)
            {
                SetObjectReference(visual, "_waterBodyImage", waterImage);
                SetObjectReference(visual, "_fillMask", fillMask);
                SetObjectReference(visual, "_waveHighlight", wave);
                SetObjectReference(visual, "_bubbleParticles", bubbles);
            }
        }

        private static void ApplyFeverOutlineGauge(GameObject fever, GameObject oxygen)
        {
            if (fever == null) return;

            ApplyImage(fever, null, new Color(1f, 1f, 1f, 0f), false, false);

            RectTransform feverRect = fever.transform as RectTransform;
            RectTransform oxygenRect = oxygen != null ? oxygen.transform as RectTransform : null;
            if (feverRect != null && oxygenRect != null)
            {
                CopyRect(oxygenRect, feverRect);
            }

            GameObject label = FindDirect(fever.transform, "Label");
            if (label != null) label.SetActive(false);

            GameObject marker = FindDirect(fever.transform, "Marker");
            if (marker != null) marker.SetActive(false);

            GameObject legacyFill = FindDirect(fever.transform, "Fill");
            if (legacyFill != null) legacyFill.SetActive(false);

            Transform visualParent = oxygen != null ? oxygen.transform : fever.transform;
            Sprite frameSprite = SpriteAt(FeverFrameAlphaMaskPath);
            if (frameSprite == null) frameSprite = SpriteAt(OxygenFrameSpritePath);
            RectTransform glow = EnsureMovedImageChild(
                visualParent,
                fever.transform,
                "FeverFrameGlow_Image",
                frameSprite,
                Vector2.zero,
                Vector2.zero,
                new Color(1f, 0.72f, 0.22f, 0.16f));
            Stretch(glow, Vector2.zero, Vector2.zero);
            glow.localScale = Vector3.one * 1.5f;

            RectTransform fill = EnsureMovedImageChild(
                visualParent,
                fever.transform,
                "FeverFrameFill_Image",
                frameSprite,
                Vector2.zero,
                Vector2.zero,
                new Color(1f, 0.82f, 0.42f, 0.52f));
            Stretch(fill, Vector2.zero, Vector2.zero);
            fill.localScale = Vector3.one * 1.39f;

            Image glowImage = ConfigureFeverOutlineImage(glow, 0f);
            Image fillImage = ConfigureFeverOutlineImage(fill, 0f);

            OrderOxygenGaugeVisuals(visualParent);

            FeverGauge gauge = fever.GetComponent<FeverGauge>();
            if (gauge != null)
            {
                SetObjectReference(gauge, "_fillImage", fillImage);
                SetObjectReference(gauge, "_glowImage", glowImage);
                SetObjectReference(gauge, "_marker", null);
                SetBool(gauge, "_useRadialOutline", true);
                SetBool(gauge, "_transparentOverlay", false);
                SetBool(gauge, "_showLabel", false);
            }
        }

        private static void OrderOxygenGaugeVisuals(Transform oxygen)
        {
            if (oxygen == null) return;

            RectTransform fillMask = FindRect(oxygen, "FillMask");
            RectTransform glow = FindRect(oxygen, "FeverFrameGlow_Image");
            RectTransform fill = FindRect(oxygen, "FeverFrameFill_Image");
            RectTransform frame = FindRect(oxygen, "Frame_Image");
            RectTransform spark = FindRect(oxygen, "FeverLeadingSpark_Image");

            int index = 0;
            if (fillMask != null) fillMask.SetSiblingIndex(index++);
            if (glow != null) glow.SetSiblingIndex(index++);
            if (fill != null) fill.SetSiblingIndex(index++);
            if (frame != null) frame.SetSiblingIndex(index++);
            if (spark != null) spark.SetSiblingIndex(index);
        }

        private static Image ConfigureFeverOutlineImage(RectTransform rect, float fillAmount)
        {
            if (rect == null) return null;

            Image image = rect.GetComponent<Image>();
            if (image == null) image = rect.gameObject.AddComponent<Image>();
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.fillAmount = fillAmount;
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        private static void ApplyGameplaySettings()
        {
            GameObject settingsCanvas = FindGameObject("SettingsCanvas");
            if (settingsCanvas != null)
            {
                Canvas canvas = settingsCanvas.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = 500;
                }
            }

            GameObject panel = FindGameObject("SettingsPanel");
            if (panel == null) return;

            RectTransform panelRect = panel.transform as RectTransform;
            if (panelRect != null) SetRect(panelRect, panelRect.anchoredPosition, new Vector2(700f, 500f));
            ApplyImage(panel, null, new Color(1f, 1f, 1f, 0f), false, false);
            Transform background = panel.transform.Find("Background");
            if (background != null)
            {
                ApplyImage(background.gameObject, SpriteAt($"{SettingsUiPath}/T_UI_Settings_Panel_Frame_OceanBlend.png"), Color.white, false, false);
                SetChildActive(panel.transform, "Background", true);
            }
            SetChildActive(panel.transform, "RowBackplate_Audio", false);
            SetChildActive(panel.transform, "RowBackplate_Speed", false);
            SetChildActive(panel.transform, "RowBackplate_Offset", false);
            RectTransform divider = EnsureImageChild(panel.transform, "CenterDivider", null, new Vector2(0f, 3f), new Vector2(3f, 278f), new Color(0.68f, 0.78f, 0.92f, 0.36f));
            Image dividerImage = divider != null ? divider.GetComponent<Image>() : null;
            if (dividerImage != null) dividerImage.raycastTarget = false;

            ApplyButtonSprite(FindDirect(panel.transform, "ResumeButton"), SpriteAt($"{SettingsUiPath}/T_UI_Settings_Button_Secondary_AuroraLavender.png"));
            ApplyButtonSprite(FindDirect(panel.transform, "LobbyButton"), SpriteAt($"{SettingsUiPath}/T_UI_Settings_Button_Secondary_AuroraLavender.png"));
            ApplyButtonSprite(FindDirect(panel.transform, "TimingResetButton"), SpriteAt($"{SettingsUiPath}/T_UI_Settings_Button_Secondary_AuroraLavender.png"));
            SetRect(FindRect(panel.transform, "ResumeButton"), new Vector2(-80f, -132f), new Vector2(140f, 40f));
            SetRect(FindRect(panel.transform, "LobbyButton"), new Vector2(80f, -132f), new Vector2(140f, 40f));

            ConfigureSlider(panel.transform, "BgmSlider", new Vector2(-155f, 75f), new Vector2(220f, 22f));
            ConfigureSlider(panel.transform, "SfxSlider", new Vector2(-155f, -25f), new Vector2(220f, 22f));
            ConfigureSlider(panel.transform, "NoteSpeedSlider", new Vector2(145f, 75f), new Vector2(210f, 22f));
            ConfigureSlider(panel.transform, "OffsetSlider", new Vector2(145f, -5f), new Vector2(210f, 22f));

            GameObject oldPlaybackSlider = FindDirect(panel.transform, "PlaybackSpeedSlider");
            if (oldPlaybackSlider != null) oldPlaybackSlider.SetActive(false);
            SetChildActive(panel.transform, "PlaybackSpeedSliderLabel", false);

            EnsureText(panel.transform, "PlaybackSpeedLabel", "PLAYBACK", new Vector2(125f, -50f), new Vector2(170f, 24f), 15f, TextAlignmentOptions.Left);
            EnsureStepper(panel.transform, "PlaybackSpeedDownButton", $"{SettingsUiPath}/T_UI_Settings_Stepper_Left_AuroraLavender.png", new Vector2(72f, -83f));
            EnsureStepper(panel.transform, "PlaybackSpeedUpButton", $"{SettingsUiPath}/T_UI_Settings_Stepper_Right_AuroraLavender.png", new Vector2(208f, -83f));
            EnsureText(panel.transform, "PlaybackSpeedValue", "1.0x", new Vector2(140f, -83f), new Vector2(70f, 26f), 15f, TextAlignmentOptions.Center);

            SetRect(FindRect(panel.transform, "Title"), new Vector2(0f, 172f), new Vector2(320f, 42f));
            SetRect(FindRect(panel.transform, "TimingResetButton"), new Vector2(190f, 172f), new Vector2(104f, 36f));
            SetRect(FindRect(panel.transform, "BgmSliderLabel"), new Vector2(-170f, 105f), new Vector2(190f, 26f));
            SetRect(FindRect(panel.transform, "BgmValue"), new Vector2(-82f, 105f), new Vector2(80f, 26f));
            SetRect(FindRect(panel.transform, "SfxSliderLabel"), new Vector2(-170f, 5f), new Vector2(190f, 26f));
            SetRect(FindRect(panel.transform, "SfxValue"), new Vector2(-82f, 5f), new Vector2(80f, 26f));
            SetRect(FindRect(panel.transform, "NoteSpeedSliderLabel"), new Vector2(125f, 105f), new Vector2(165f, 24f));
            SetRect(FindRect(panel.transform, "NoteSpeedValue"), new Vector2(220f, 105f), new Vector2(75f, 24f));
            SetRect(FindRect(panel.transform, "OffsetSliderLabel"), new Vector2(125f, 25f), new Vector2(165f, 24f));
            SetRect(FindRect(panel.transform, "OffsetValue"), new Vector2(220f, 25f), new Vector2(75f, 24f));

            SetTextStyle(FindDirectTransform(panel.transform, "Title"), Color.white, 28f);
            SetTextStyle(FindDirectTransform(panel.transform, "BgmSliderLabel"), new Color(0.82f, 0.95f, 1f, 1f), 15f);
            SetTextStyle(FindDirectTransform(panel.transform, "SfxSliderLabel"), new Color(0.82f, 0.95f, 1f, 1f), 15f);
            SetTextStyle(FindDirectTransform(panel.transform, "NoteSpeedSliderLabel"), new Color(0.82f, 0.95f, 1f, 1f), 15f);
            SetTextStyle(FindDirectTransform(panel.transform, "OffsetSliderLabel"), new Color(0.82f, 0.95f, 1f, 1f), 15f);
        }

        private static void ReplaceJudgementFrameWithSprites()
        {
            GameObject hitAnchor = FindGameObject("HitAnchor");
            if (hitAnchor == null) return;

            Transform judgementFrame = FindDirectTransform(hitAnchor.transform, "JudgementFrame");
            if (judgementFrame != null)
            {
                JudgementFrame frame = judgementFrame.GetComponent<JudgementFrame>();
                if (frame != null) DestroyImmediateSafely(frame);

                string[] lineNames =
                {
                    "FrameLine", "CenterHorizontal", "CenterVertical",
                    "LaneMarker_Up", "LaneMarker_Down", "LaneMarker_Left", "LaneMarker_Right", "LaneMarker_Center"
                };

                foreach (string lineName in lineNames)
                {
                    Transform child = judgementFrame.Find(lineName);
                    if (child != null) DestroyImmediateSafely(child.gameObject);
                }
            }

            RectTransform canvasRect = EnsureWorldCanvas(hitAnchor.transform, "HitTargetCanvas", new Vector2(900f, 900f), new Vector3(0f, 0f, -0.01f), Vector3.one * 0.001f);
            foreach (Transform child in canvasRect)
            {
                child.gameObject.SetActive(false);
            }

            EnsureHitAnchorArrow(canvasRect, "HitAnchorArrow_Up", HitAnchorArrowUpperLeftSpritePath);
            EnsureHitAnchorArrow(canvasRect, "HitAnchorArrow_Right", HitAnchorArrowUpperRightSpritePath);
            EnsureHitAnchorArrow(canvasRect, "HitAnchorArrow_Left", HitAnchorArrowLowerLeftSpritePath);
            EnsureHitAnchorArrow(canvasRect, "HitAnchorArrow_Down", HitAnchorArrowLowerRightSpritePath);

            EnsureImageChild(canvasRect, "CenterDiamondMarker", SpriteAt(HitTargetCenterSpritePath), Vector2.zero, new Vector2(200f, 200f), new Color(1f, 1f, 1f, 0.88f));
            ConfigureHitTargetCanvasAlwaysOnTop(canvasRect.gameObject);
        }

        private static void ApplyResultScene()
        {
            EditorSceneManager.OpenScene(ResultScenePath, OpenSceneMode.Single);

            GameObject canvas = FindGameObject("ResultCanvas");
            if (canvas != null)
            {
                RectTransform clear = EnsureImageChild(canvas.transform, "ClearGlow", SpriteAt($"{ResultUiPath}/T_UI_Result_ClearGlow.png"), new Vector2(-275f, 15f), new Vector2(420f, 420f), new Color(1f, 1f, 1f, 0.48f));
                RectTransform mist = EnsureImageChild(canvas.transform, "GameOverMist", SpriteAt($"{ResultUiPath}/T_UI_Result_GameOverMist.png"), new Vector2(-260f, 36f), new Vector2(460f, 460f), new Color(1f, 1f, 1f, 0.46f));
                clear.SetAsFirstSibling();
                mist.SetSiblingIndex(1);
                clear.gameObject.SetActive(false);
                mist.gameObject.SetActive(false);

                RectTransform rankRing = EnsureImageChild(canvas.transform, "RankRingImage", SpriteAt($"{ResultUiPath}/T_UI_Result_RankRing.png"), new Vector2(-275f, 15f), new Vector2(370f, 370f), Color.white);
                SetRect(rankRing, new Vector2(-275f, 15f), new Vector2(370f, 370f));
                MoveSiblingBefore(rankRing, canvas.transform, "Rank");

                RectTransform rankText = FindRect(canvas.transform, "Rank");
                SetRect(rankText, new Vector2(-275f, 15f), new Vector2(370f, 370f));

                RectTransform stats = EnsureImageChild(canvas.transform, "StatPanelFrame", SpriteAt($"{ResultUiPath}/T_UI_Result_StatPanel_Frame.png"), new Vector2(172f, 15f), new Vector2(590f, 390f), Color.white);
                SetRect(stats, new Vector2(172f, 15f), new Vector2(590f, 390f));
                Image statsImage = stats.GetComponent<Image>();
                if (statsImage != null) statsImage.preserveAspect = false;
                MoveSiblingBefore(stats, canvas.transform, "MainStats");

                RectTransform mainStats = FindRect(canvas.transform, "MainStats");
                SetRect(mainStats, new Vector2(260f, 53f), new Vector2(440f, 150f));

                TMP_Text judgementBreakdown = EnsureText(
                    canvas.transform,
                    "JudgementBreakdown",
                    "P/G/M<pos=150>0 / 0 / 0\nFish/Fever<pos=150>0/0 / 0",
                    new Vector2(260f, -51f),
                    new Vector2(440f, 58f),
                    22f,
                    TextAlignmentOptions.Left);
                SetRect(judgementBreakdown.rectTransform, new Vector2(260f, -51f), new Vector2(440f, 58f));

                TMP_Text handRangeStats = EnsureText(
                    canvas.transform,
                    "HandRangeStats",
                    "Hand range (world)\nL no samples\nR no samples",
                    new Vector2(122f, -132f),
                    new Vector2(520f, 78f),
                    18f,
                    TextAlignmentOptions.Left);
                SetRect(handRangeStats.rectTransform, new Vector2(122f, -132f), new Vector2(520f, 78f));
                handRangeStats.gameObject.SetActive(false);

                RectTransform unlock = EnsureImageChild(canvas.transform, "UnlockBadgeFrame", SpriteAt($"{ResultUiPath}/T_UI_Result_UnlockBadge_Frame.png"), new Vector2(0f, -150f), new Vector2(450f, 86f), Color.white);
                MoveSiblingBefore(unlock, canvas.transform, "Unlock");
                unlock.gameObject.SetActive(false);

                Sprite secondaryButtonSprite = SpriteAt($"{ResultUiPath}/T_UI_Result_ActionButton_Secondary.png");
                ApplyButtonSprite(FindDirect(canvas.transform, "RetryButton"), secondaryButtonSprite);
                ApplyButtonSprite(FindDirect(canvas.transform, "LobbyButton"), secondaryButtonSprite);
                SetRect(FindRect(canvas.transform, "RetryButton"), new Vector2(-120f, -264f), new Vector2(210f, 72f));
                SetRect(FindRect(canvas.transform, "LobbyButton"), new Vector2(120f, -264f), new Vector2(210f, 72f));

                SetTextStyle(FindDirectTransform(canvas.transform, "Title"), Color.white, 46f);
                SetTextStyle(FindDirectTransform(canvas.transform, "Rank"), Color.white, 112f);
                SetTextStyle(FindDirectTransform(canvas.transform, "MainStats"), new Color(0.88f, 0.98f, 1f, 1f), 30f, TextAlignmentOptions.Left);
                SetTextStyle(FindDirectTransform(canvas.transform, "JudgementBreakdown"), Color.white, 22f, TextAlignmentOptions.Left);
            }

            SaveActiveScene();
        }

        private static void ApplyArrowButton(Transform parent, string name, string spritePath, Vector2 position)
        {
            GameObject button = FindDirect(parent, name);
            ApplyButtonSprite(button, SpriteAt(spritePath));
            SetRectIfTiny(button != null ? button.transform as RectTransform : null, position, new Vector2(96f, 96f));
            SetLabelAlpha(button, 0f);
        }

        private static void ApplyButtonSprite(GameObject go, Sprite sprite)
        {
            if (go == null || sprite == null) return;
            Image image = ApplyImage(go, sprite, Color.white, true, false);
            Button button = go.GetComponent<Button>();
            if (button == null) return;

            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.78f, 0.92f, 1f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.42f);
            button.colors = colors;
        }

        private static Image ApplyImage(GameObject go, Sprite sprite, Color color, bool raycastTarget, bool preserveAspect)
        {
            if (go == null) return null;

            Image image = GetOrCreateImage(go);
            if (image == null) return null;

            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycastTarget;
            image.preserveAspect = preserveAspect || ShouldPreserveAspect(go, sprite);
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            return image;
        }

        private static Image GetOrCreateImage(GameObject go)
        {
            Image image = go.GetComponent<Image>();
            if (image != null) return image;

            Graphic graphic = go.GetComponent<Graphic>();
            if (graphic == null) return go.AddComponent<Image>();

            // Unity UI allows only one Graphic per GameObject. TextMeshProUGUI is already
            // a Graphic, so sprite backplates must live on a child/sibling instead.
            RectTransform rect = EnsureImageChild(go.transform, $"{go.name}_Image", null, Vector2.zero, Vector2.zero, Color.white);
            Stretch(rect, Vector2.zero, Vector2.zero);
            rect.SetAsFirstSibling();
            return rect.GetComponent<Image>();
        }

        private static bool ShouldPreserveAspect(GameObject go, Sprite sprite)
        {
            if (!PreserveDecorativeSpriteAspect || go == null || sprite == null) return false;

            string name = go.name;
            if (name.Contains("Fill") || name.Contains("WaterBody")) return false;
            if (name.Contains("LaneGuide")) return false;
            if (name.Contains("Slider") && !name.Contains("Knob")) return false;

            Image image = go.GetComponent<Image>();
            if (image != null && image.type == Image.Type.Filled) return false;

            return true;
        }

        private static RectTransform EnsureImageChild(Transform parent, string name, Sprite sprite, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.transform as RectTransform;
            if (existing == null)
            {
                SetRect(rect, anchoredPosition, size);
            }
            ApplyImage(go, sprite, color, false, false);
            go.SetActive(true);
            return rect;
        }

        private static RectTransform EnsureMovedImageChild(Transform parent, Transform legacyParent, string name, Sprite sprite, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            if (existing == null && legacyParent != null && legacyParent != parent)
            {
                existing = legacyParent.Find(name);
            }

            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
            bool created = existing == null;
            go.transform.SetParent(parent, false);

            RectTransform rect = go.transform as RectTransform;
            if (created)
            {
                SetRect(rect, anchoredPosition, size);
            }

            ApplyImage(go, sprite, color, false, false);
            go.SetActive(true);
            return rect;
        }

        private static RectTransform EnsureSiblingImageBefore(Transform target, string name, Sprite sprite, Color color)
        {
            if (target == null || target.parent == null) return null;

            Transform parent = target.parent;
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.transform as RectTransform;
            RectTransform targetRect = target as RectTransform;
            if (rect != null && targetRect != null)
            {
                CopyRect(targetRect, rect);
            }

            ApplyImage(go, sprite, color, false, false);
            go.SetActive(true);
            go.transform.SetSiblingIndex(target.GetSiblingIndex());
            return rect;
        }

        private static RectTransform EnsureRectChild(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.transform as RectTransform;
            if (existing == null)
            {
                SetRect(rect, anchoredPosition, size);
            }
            go.SetActive(true);
            return rect;
        }

        private static ParticleSystem EnsureOxygenBubbleParticles(Transform parent)
        {
            Transform existing = parent.Find("BubbleParticleSystem");
            GameObject go = existing != null ? existing.gameObject : new GameObject("BubbleParticleSystem", typeof(RectTransform), typeof(ParticleSystem));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.transform as RectTransform;
            if (existing == null)
            {
                SetRect(rect, Vector2.zero, Vector2.zero);
            }
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            ParticleSystem particles = go.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0f);
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 12f);
            main.maxParticles = 24;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.72f, 0.96f, 1f, 0.45f), new Color(1f, 1f, 1f, 0.75f));

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 5f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(22f, 140f, 0.01f);

            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.y = new ParticleSystem.MinMaxCurve(10f, 20f);
            velocity.x = new ParticleSystem.MinMaxCurve(-2f, 2f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 4;
            renderer.sharedMaterial = EnsureOxygenBubbleMaterial();

            go.SetActive(true);
            return particles;
        }

        private static Material EnsureOxygenBubbleMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(OxygenBubbleMaterialPath);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            Material material = new Material(shader)
            {
                name = "M_OxygenGaugeBubble"
            };

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GameplayUiPath}/T_AO_UI_OxygenBubble_Single_Additive_C02.png");
            if (texture != null) material.mainTexture = texture;
            material.color = Color.white;

            AssetDatabase.CreateAsset(material, OxygenBubbleMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void EnsureHitAnchorArrow(RectTransform parent, string name, string spritePath)
        {
            RectTransform rect = EnsureImageChild(parent, name, SpriteAt(spritePath), Vector2.zero, parent.sizeDelta, new Color(1f, 1f, 1f, 0.72f));
            rect.localRotation = Quaternion.identity;
            rect.SetAsFirstSibling();
        }

        private static void ConfigureHitTargetCanvasAlwaysOnTop(GameObject canvasObject)
        {
            if (canvasObject == null) return;

            Material material = HitTargetCanvasVisualSetup.EnsureAlwaysOnTopMaterial();
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = 120;
                EditorUtility.SetDirty(canvas);
            }

            HitTargetCanvasAlwaysOnTop alwaysOnTop = canvasObject.GetComponent<HitTargetCanvasAlwaysOnTop>();
            if (alwaysOnTop == null) alwaysOnTop = canvasObject.AddComponent<HitTargetCanvasAlwaysOnTop>();

            SerializedObject serializedObject = new SerializedObject(alwaysOnTop);
            serializedObject.FindProperty("_alwaysOnTopMaterial").objectReferenceValue = material;
            serializedObject.FindProperty("_sortingOrder").intValue = 120;
            serializedObject.FindProperty("_overrideCanvasSorting").boolValue = true;
            serializedObject.FindProperty("_applyToInactiveChildren").boolValue = true;
            serializedObject.FindProperty("_disableRaycastTargets").boolValue = true;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            alwaysOnTop.Apply();
            EditorUtility.SetDirty(alwaysOnTop);
        }

        private static RectTransform EnsureWorldCanvas(Transform parent, string name, Vector2 size, Vector3 localPosition, Vector3 localScale)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.transform as RectTransform;
            rect.sizeDelta = size;
            rect.localPosition = localPosition;
            rect.localRotation = Quaternion.identity;
            rect.localScale = localScale;

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 25;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 80f;
            scaler.referencePixelsPerUnit = 100f;

            if (go.GetComponent<WorldSpaceVrCanvas>() == null) go.AddComponent<WorldSpaceVrCanvas>();
            return rect;
        }

        private static void EnsureRowBackplate(Transform panel, string name, Vector2 pos, Vector2 size)
        {
            RectTransform row = EnsureImageChild(panel, name, SpriteAt($"{SettingsUiPath}/T_UI_Settings_Row_Backplate.png"), pos, size, new Color(1f, 1f, 1f, 0.72f));
            row.SetAsFirstSibling();
        }

        private static void ConfigureSlider(Transform panel, string name, Vector2 pos, Vector2 size)
        {
            Transform sliderTransform = FindDirectTransform(panel, name);
            RectTransform slider = sliderTransform as RectTransform;
            if (slider == null) return;

            if (slider.sizeDelta.sqrMagnitude <= 0.01f)
            {
                SetRect(slider, pos, size);
            }
            ApplyImage(slider.gameObject, SpriteAt($"{SettingsUiPath}/T_UI_Settings_SliderTrack_AuroraLavender.png"), Color.white, true, false);

            RectTransform fillArea = FindRect(slider, "Fill Area");
            if (fillArea != null) Stretch(fillArea, new Vector2(18f, 7f), new Vector2(-24f, -7f));

            RectTransform fill = FindRect(fillArea, "Fill");
            if (fill != null) ApplyImage(fill.gameObject, null, new Color(0.48f, 0.72f, 0.86f, 0.84f), false, false);

            RectTransform handleArea = FindRect(slider, "Handle Slide Area");
            if (handleArea != null) Stretch(handleArea, new Vector2(24f, 0f), new Vector2(-24f, 0f));

            RectTransform handle = FindRect(handleArea, "Handle");
            if (handle != null)
            {
                SetRect(handle, Vector2.zero, new Vector2(22f, 22f));
                ApplyImage(handle.gameObject, SpriteAt($"{SettingsUiPath}/T_UI_Settings_SliderKnob_Gradient.png"), Color.white, true, true);
            }

            Slider sliderComponent = slider.GetComponent<Slider>();
            if (sliderComponent != null)
            {
                if (fill != null) sliderComponent.fillRect = fill;
                if (handle != null) sliderComponent.handleRect = handle;
                Image handleImage = handle != null ? handle.GetComponent<Image>() : null;
                if (handleImage != null) sliderComponent.targetGraphic = handleImage;
            }
        }

        private static void EnsureStepper(Transform panel, string name, string spritePath, Vector2 pos)
        {
            GameObject go = FindDirect(panel, name);
            bool created = go == null;
            if (go == null)
            {
                go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(panel, false);
            }

            if (created) SetRect(go.transform as RectTransform, pos, new Vector2(42f, 42f));
            ApplyButtonSprite(go, SpriteAt(spritePath));
            SetLabelAlpha(go, 0f);
        }

        private static TMP_Text EnsureText(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            if (existing == null) SetRect(go.transform as RectTransform, pos, size);

            TMP_Text tmp = go.GetComponent<TMP_Text>();
            if (tmp == null) tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            if (_uiFont != null) tmp.font = _uiFont;
            tmp.fontSize = fontSize;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = Mathf.Max(8f, fontSize * 0.55f);
            tmp.fontSizeMax = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            go.SetActive(true);
            return tmp;
        }

        private static void SetLabelAlpha(GameObject button, float alpha)
        {
            Transform label = button != null ? button.transform.Find("Label") : null;
            TMP_Text text = label != null ? label.GetComponent<TMP_Text>() : null;
            if (text == null) return;

            Color color = text.color;
            color.a = alpha;
            text.color = color;
        }

        private static void SetTextStyle(Transform transform, Color color, float fontSize, TextAlignmentOptions? alignment = null)
        {
            TMP_Text text = transform != null ? transform.GetComponent<TMP_Text>() : null;
            if (text == null) return;

            if (_uiFont != null) text.font = _uiFont;
            text.color = color;
            text.fontSize = fontSize;
            text.fontSizeMax = fontSize;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(8f, fontSize * 0.55f);
            if (alignment.HasValue) text.alignment = alignment.Value;
        }

        private static void SetChildActive(Transform parent, string name, bool active)
        {
            Transform child = parent != null ? parent.Find(name) : null;
            if (child != null) child.gameObject.SetActive(active);
        }

        private static void MoveSiblingBefore(RectTransform moving, Transform parent, string targetName)
        {
            if (moving == null || parent == null) return;
            Transform target = parent.Find(targetName);
            if (target == null)
            {
                moving.SetAsFirstSibling();
                return;
            }

            moving.SetSiblingIndex(Mathf.Max(0, target.GetSiblingIndex()));
        }

        private static GameObject FindGameObject(string name)
        {
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go.name == name) return go;
            }

            return null;
        }

        private static GameObject FindDirect(Transform parent, string name)
        {
            Transform child = FindDirectTransform(parent, name);
            return child != null ? child.gameObject : null;
        }

        private static Transform FindDirectTransform(Transform parent, string name)
        {
            return parent != null ? parent.Find(name) : null;
        }

        private static RectTransform FindRect(Transform parent, string name)
        {
            Transform child = FindDirectTransform(parent, name);
            return child != null ? child as RectTransform : null;
        }

        private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void SetRectIfTiny(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            if (rect == null) return;
            if (rect.sizeDelta.sqrMagnitude > 0.01f) return;
            SetRect(rect, anchoredPosition, size);
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void CopyRect(RectTransform source, RectTransform destination)
        {
            if (source == null || destination == null) return;

            destination.anchorMin = source.anchorMin;
            destination.anchorMax = source.anchorMax;
            destination.pivot = source.pivot;
            destination.anchoredPosition = source.anchoredPosition;
            destination.sizeDelta = source.sizeDelta;
            destination.localScale = source.localScale;
            destination.localRotation = source.localRotation;
        }

        private static void SetObjectReference(Object target, string propertyName, Object value)
        {
            if (target == null) return;
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference) return;

            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static void SetBool(Object target, string propertyName, bool value)
        {
            if (target == null) return;
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Boolean) return;

            property.boolValue = value;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static void DestroyImmediateSafely(Object target)
        {
            if (target == null) return;
            MoveSelectionAwayFrom(target);
            Object.DestroyImmediate(target);
        }

        private static void MoveSelectionAwayFrom(Object target)
        {
            Transform targetTransform = TargetTransform(target);
            if (targetTransform == null) return;

            bool selectionTouchesTarget = false;
            foreach (Object selected in Selection.objects)
            {
                if (selected == null) continue;
                if (selected == target)
                {
                    selectionTouchesTarget = true;
                    break;
                }

                Transform selectedTransform = TargetTransform(selected);
                if (selectedTransform != null && selectedTransform.IsChildOf(targetTransform))
                {
                    selectionTouchesTarget = true;
                    break;
                }
            }

            if (!selectionTouchesTarget) return;

            if (target is Component && !(target is Transform))
            {
                Selection.objects = new Object[] { targetTransform.gameObject };
                Selection.activeObject = targetTransform.gameObject;
                return;
            }

            Transform safeParent = targetTransform.parent;
            if (safeParent != null)
            {
                Selection.objects = new Object[] { safeParent.gameObject };
                Selection.activeObject = safeParent.gameObject;
            }
            else
            {
                Selection.objects = new Object[0];
                Selection.activeObject = null;
            }
        }

        private static Transform TargetTransform(Object target)
        {
            if (target is GameObject go) return go.transform;
            if (target is Component component) return component.transform;
            return null;
        }

        private static Sprite SpriteAt(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[AO] Missing UI sprite: {path}");
            return sprite;
        }

        private static void SaveActiveScene()
        {
            ApplyFontToSceneText();
            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ApplyFontToSceneText()
        {
            if (_uiFont == null) return;
            foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (text.gameObject.scene != SceneManager.GetActiveScene()) continue;
                text.font = _uiFont;
            }
        }

    }
}
