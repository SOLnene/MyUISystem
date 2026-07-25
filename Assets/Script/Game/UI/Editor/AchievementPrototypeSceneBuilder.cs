#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
static class AchievementPrototypeSceneBuilder
{
    const string TargetScenePath = "Assets/Scenes/UICreate.unity";
    const string RootName = "AchievementView_Prototype";
    const string RequestRelativePath = ".codex_tmp_frame_analysis/achievement_prototype.request";
    const string ResultRelativePath = ".codex_tmp_frame_analysis/achievement_prototype_result.txt";
    const string PreviewRelativePath = ".codex_tmp_frame_analysis/achievement_ui_preview.png";
    const string FontPath = "Assets/Fonts/static/NotoSansSC-ExtraBold SDF.asset";

    static readonly Color BackdropColor = new(0.075f, 0.12f, 0.145f, 1f);
    static readonly Color PanelDarkColor = new(0.105f, 0.18f, 0.21f, 0.98f);
    static readonly Color PanelDarkLightColor = new(0.145f, 0.245f, 0.275f, 0.98f);
    static readonly Color PaperColor = new(0.94f, 0.925f, 0.875f, 1f);
    static readonly Color PaperLightColor = new(0.985f, 0.975f, 0.94f, 1f);
    static readonly Color DarkTextColor = new(0.20f, 0.235f, 0.25f, 1f);
    static readonly Color MutedTextColor = new(0.47f, 0.49f, 0.50f, 1f);
    static readonly Color GoldColor = new(0.80f, 0.61f, 0.25f, 1f);
    static readonly Color GoldLightColor = new(0.94f, 0.79f, 0.45f, 1f);
    static readonly Color ProgressColor = new(0.23f, 0.55f, 0.52f, 1f);
    static readonly Color ClaimRedColor = new(0.78f, 0.22f, 0.18f, 1f);

    static TMP_FontAsset font;
    static Sprite roundedSprite;
    static Sprite circleSprite;
    static Sprite paperSprite;
    static Sprite rewardSprite;
    static Sprite medalSprite;

    static AchievementPrototypeSceneBuilder()
    {
        EditorApplication.delayCall += TryBuild;
    }

    static void TryBuild()
    {
        string requestPath = ProjectPath(RequestRelativePath);
        if (!File.Exists(requestPath))
        {
            return;
        }

        try
        {
            Build();
            File.WriteAllText(ProjectPath(ResultRelativePath), "SUCCESS");
            File.Delete(requestPath);
        }
        catch (Exception exception)
        {
            File.WriteAllText(ProjectPath(ResultRelativePath), $"FAILED\n{exception}");
            Debug.LogException(exception);
        }
    }

    static void Build()
    {
        ImportAchievementSprites();
        LoadAssets();

        Scene scene = SceneManager.GetSceneByPath(TargetScenePath);
        bool openedByBuilder = !scene.IsValid() || !scene.isLoaded;
        if (openedByBuilder)
        {
            scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Additive);
        }
        else if (scene.isDirty)
        {
            throw new InvalidOperationException(
                "UICreate scene has unsaved changes. Save or discard them before building the achievement prototype.");
        }

        Canvas canvas = FindCanvas(scene);
        if (canvas == null)
        {
            throw new InvalidOperationException("Cannot find Canvas in UICreate scene.");
        }

        Transform existing = canvas.transform.Find(RootName);
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        BuildPrototype(canvas.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new IOException("Failed to save UICreate scene.");
        }

        CapturePreview(canvas);

        if (openedByBuilder)
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void ImportAchievementSprites()
    {
        string[] paths =
        {
            "Assets/Art/Sprite/Icon/Achievement_Category_Adventure.png",
            "Assets/Art/Sprite/Icon/Achievement_Category_Growth.png",
            "Assets/Art/Sprite/Icon/Achievement_Category_Combat.png",
            "Assets/Art/Sprite/Icon/Achievement_Category_Explore.png",
            "Assets/Art/Sprite/Icon/Achievement_Medal.png",
        };

        foreach (string path in paths)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                throw new InvalidOperationException($"Cannot import achievement sprite: {path}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }

    static void LoadAssets()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        paperSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Art/Sprite/Background/UI_Img_PopupBG.png");
        rewardSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/AssetsPackage/UI/Sprite/Item/Currency/UI_ItemIcon_201.png");
        medalSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Art/Sprite/Icon/Achievement_Medal.png");

        if (font == null || rewardSprite == null || medalSprite == null)
        {
            throw new InvalidOperationException("Achievement prototype assets are not ready.");
        }
    }

    static Canvas FindCanvas(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.name == "Canvas")
                {
                    return canvas;
                }
            }
        }

        return null;
    }

    static void BuildPrototype(Transform canvasTransform)
    {
        RectTransform root = CreateRect(
            RootName,
            canvasTransform,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);

        CreateImage("Backdrop", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, BackdropColor);

        RectTransform glow = CreateRect(
            "AmbientGlow",
            root,
            new Vector2(0.64f, 0.46f),
            new Vector2(0.64f, 0.46f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(1050f, 1050f));
        Image glowImage = glow.gameObject.AddComponent<Image>();
        glowImage.sprite = circleSprite;
        glowImage.color = new Color(0.22f, 0.43f, 0.45f, 0.16f);
        glowImage.raycastTarget = false;

        BuildTopBar(root);

        RectTransform body = CreateRect(
            "Body",
            root,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        body.offsetMin = new Vector2(64f, 54f);
        body.offsetMax = new Vector2(-64f, -128f);

        BuildCategoryPanel(body);
        BuildDetailPanel(body);
    }

    static void BuildTopBar(RectTransform root)
    {
        RectTransform topBar = CreateRect(
            "TopBar",
            root,
            new Vector2(0f, 1f),
            Vector2.one,
            new Vector2(0.5f, 1f),
            Vector2.zero,
            new Vector2(0f, 112f));
        AddImage(topBar, new Color(0.055f, 0.095f, 0.115f, 0.92f), roundedSprite, Image.Type.Sliced);

        RectTransform backButton = CreateTopLeftRect("BackButton", topBar, 44f, 23f, 66f, 66f);
        Image backImage = AddImage(backButton, new Color(0.87f, 0.84f, 0.74f, 0.16f), circleSprite);
        backImage.raycastTarget = true;
        backButton.gameObject.AddComponent<Button>().targetGraphic = backImage;
        CreateText("Arrow", backButton, "←", 0f, 0f, 66f, 66f, 34f, Color.white, TextAlignmentOptions.Center);

        Sprite combatIcon = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Art/Sprite/Icon/Achievement_Category_Combat.png");
        CreateSprite("TitleIcon", topBar, combatIcon, 132f, 21f, 68f, 68f, Color.white);
        CreateText("Title", topBar, "成就", 212f, 18f, 360f, 52f, 40f, Color.white);
        CreateText(
            "Subtitle",
            topBar,
            "记录每一次成长与突破",
            214f,
            66f,
            420f,
            28f,
            18f,
            new Color(0.75f, 0.80f, 0.81f, 1f));

        RectTransform totalPanel = CreateTopRightRect("TotalProgress", topBar, 44f, 25f, 330f, 62f);
        AddImage(totalPanel, new Color(0.93f, 0.90f, 0.82f, 0.10f), roundedSprite, Image.Type.Sliced);
        CreateText(
            "Label",
            totalPanel,
            "总进度",
            22f,
            0f,
            105f,
            62f,
            20f,
            new Color(0.78f, 0.82f, 0.82f, 1f),
            TextAlignmentOptions.MidlineLeft);
        CreateText(
            "Value",
            totalPanel,
            "26 / 80",
            124f,
            0f,
            176f,
            62f,
            30f,
            GoldLightColor,
            TextAlignmentOptions.MidlineRight);

        RectTransform divider = CreateRect(
            "Divider",
            topBar,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            Vector2.zero,
            new Vector2(0f, 2f));
        AddImage(divider, new Color(0.81f, 0.66f, 0.34f, 0.55f));
    }

    static void BuildCategoryPanel(RectTransform body)
    {
        RectTransform panel = CreateRect(
            "CategoryPanel",
            body,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            Vector2.zero,
            new Vector2(420f, 0f));
        AddImage(panel, PanelDarkColor, roundedSprite, Image.Type.Sliced);

        CreateText(
            "PanelTitle",
            panel,
            "成就类型",
            26f,
            24f,
            250f,
            42f,
            28f,
            Color.white);
        CreateText(
            "PanelHint",
            panel,
            "选择类型查看详细进度",
            26f,
            62f,
            300f,
            26f,
            17f,
            new Color(0.65f, 0.72f, 0.73f, 1f));

        CreateCategoryCard(
            panel,
            0,
            "战斗",
            "12 / 20",
            0.60f,
            "Assets/Art/Sprite/Icon/Achievement_Category_Combat.png",
            true,
            "2");
        CreateCategoryCard(
            panel,
            1,
            "探索",
            "8 / 25",
            0.32f,
            "Assets/Art/Sprite/Icon/Achievement_Category_Explore.png",
            false,
            null);
        CreateCategoryCard(
            panel,
            2,
            "养成",
            "4 / 15",
            0.27f,
            "Assets/Art/Sprite/Icon/Achievement_Category_Growth.png",
            false,
            "1");
        CreateCategoryCard(
            panel,
            3,
            "收集",
            "2 / 20",
            0.10f,
            "Assets/Art/Sprite/Icon/Achievement_Category_Adventure.png",
            false,
            null);

        CreateText(
            "Footer",
            panel,
            "完成成就后即可领取奖励",
            26f,
            792f,
            368f,
            52f,
            16f,
            new Color(0.58f, 0.67f, 0.68f, 1f),
            TextAlignmentOptions.Center);
    }

    static void CreateCategoryCard(
        RectTransform parent,
        int index,
        string title,
        string value,
        float progress,
        string iconPath,
        bool selected,
        string badge)
    {
        const float cardHeight = 138f;
        float y = 108f + index * 154f;
        RectTransform card = CreateTopLeftRect($"Category_{title}", parent, 18f, y, 384f, cardHeight);
        Color cardColor = selected ? PaperColor : PanelDarkLightColor;
        AddImage(card, cardColor, roundedSprite, Image.Type.Sliced);

        if (selected)
        {
            RectTransform selection = CreateTopLeftRect("SelectedMark", card, 0f, 18f, 6f, 102f);
            AddImage(selection, GoldColor, roundedSprite, Image.Type.Sliced);
        }

        Color titleColor = selected ? DarkTextColor : Color.white;
        Color secondaryColor = selected
            ? MutedTextColor
            : new Color(0.69f, 0.76f, 0.77f, 1f);

        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        CreateSprite("Icon", card, icon, 20f, 22f, 76f, 76f, Color.white);
        CreateText("Name", card, title, 112f, 22f, 172f, 38f, 27f, titleColor);
        CreateText(
            "ProgressValue",
            card,
            value,
            276f,
            24f,
            80f,
            30f,
            18f,
            selected ? new Color(0.33f, 0.39f, 0.40f, 1f) : secondaryColor,
            TextAlignmentOptions.MidlineRight);

        CreateProgressBar(card, "Progress", 112f, 74f, 230f, 10f, progress, selected);
        CreateText(
            "Percent",
            card,
            $"{Mathf.RoundToInt(progress * 100f)}%",
            112f,
            90f,
            230f,
            26f,
            16f,
            secondaryColor,
            TextAlignmentOptions.MidlineRight);

        if (!string.IsNullOrEmpty(badge))
        {
            RectTransform badgeRoot = CreateTopLeftRect("ClaimableBadge", card, 344f, 9f, 30f, 30f);
            AddImage(badgeRoot, ClaimRedColor, circleSprite);
            CreateText(
                "Count",
                badgeRoot,
                badge,
                0f,
                0f,
                30f,
                30f,
                15f,
                Color.white,
                TextAlignmentOptions.Center);
        }
    }

    static void BuildDetailPanel(RectTransform body)
    {
        RectTransform panel = CreateRect(
            "DetailPanel",
            body,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        panel.offsetMin = new Vector2(444f, 0f);

        Image background = AddImage(panel, PaperColor, paperSprite);
        background.type = Image.Type.Simple;

        RectTransform header = CreateTopLeftRect("CategorySummary", panel, 24f, 24f, 1300f, 142f);
        AddImage(header, new Color(0.89f, 0.86f, 0.77f, 0.88f), roundedSprite, Image.Type.Sliced);

        Sprite combatIcon = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Art/Sprite/Icon/Achievement_Category_Combat.png");
        CreateSprite("CategoryIcon", header, combatIcon, 24f, 18f, 98f, 98f, Color.white);
        CreateText("Title", header, "战斗成就", 142f, 20f, 300f, 44f, 34f, DarkTextColor);
        CreateText(
            "Description",
            header,
            "以力量与技巧跨越每一次挑战",
            144f,
            65f,
            480f,
            30f,
            18f,
            MutedTextColor);
        CreateText(
            "Value",
            header,
            "12 / 20",
            1085f,
            22f,
            170f,
            42f,
            28f,
            DarkTextColor,
            TextAlignmentOptions.MidlineRight);
        CreateProgressBar(header, "CategoryProgress", 144f, 106f, 1110f, 12f, 0.60f, true);

        RectTransform viewport = CreateTopLeftRect("AchievementViewport", panel, 24f, 184f, 1300f, 680f);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = CreateRect(
            "Content",
            viewport,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            new Vector2(0f, 594f));

        ScrollRect scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 32f;

        CreateAchievementCard(
            content,
            0,
            "百战不殆",
            "累计击败 100 名敌人",
            "100 / 100",
            1f,
            "领取",
            AchievementVisualState.Claimable);
        CreateAchievementCard(
            content,
            1,
            "锋刃试炼",
            "累计完成 100 次普通攻击命中",
            "68 / 100",
            0.68f,
            "未完成",
            AchievementVisualState.InProgress);
        CreateAchievementCard(
            content,
            2,
            "初露锋芒",
            "将任意角色提升至 20 级",
            "20 / 20",
            1f,
            "已领取",
            AchievementVisualState.Claimed);
        CreateAchievementCard(
            content,
            3,
            "装备收藏家",
            "获得 10 件不同的装备",
            "4 / 10",
            0.40f,
            "未完成",
            AchievementVisualState.InProgress);
    }

    static void CreateAchievementCard(
        RectTransform parent,
        int index,
        string title,
        string description,
        string value,
        float progress,
        string buttonText,
        AchievementVisualState state)
    {
        float y = index * 152f;
        RectTransform border = CreateTopLeftRect($"Achievement_{index + 1}", parent, 0f, y, 1300f, 138f);
        Color borderColor = state == AchievementVisualState.Claimable
            ? GoldColor
            : new Color(0.70f, 0.68f, 0.62f, 1f);
        AddImage(border, borderColor, roundedSprite, Image.Type.Sliced);

        RectTransform card = CreateStretchRect("Card", border, 2f, 2f, 2f, 2f);
        Color cardColor = state == AchievementVisualState.Claimed
            ? new Color(0.89f, 0.88f, 0.84f, 1f)
            : PaperLightColor;
        AddImage(card, cardColor, roundedSprite, Image.Type.Sliced);

        RectTransform iconBackground = CreateTopLeftRect("IconBackground", card, 24f, 27f, 82f, 82f);
        AddImage(
            iconBackground,
            state == AchievementVisualState.Claimed
                ? new Color(0.55f, 0.56f, 0.55f, 1f)
                : new Color(0.33f, 0.43f, 0.45f, 1f),
            circleSprite);
        CreateSprite(
            "Medal",
            iconBackground,
            medalSprite,
            9f,
            10f,
            64f,
            60f,
            state == AchievementVisualState.Claimed
                ? new Color(0.82f, 0.82f, 0.79f, 1f)
                : Color.white);

        CreateText("Title", card, title, 128f, 18f, 560f, 38f, 27f, DarkTextColor);
        CreateText(
            "Description",
            card,
            description,
            128f,
            54f,
            610f,
            30f,
            18f,
            MutedTextColor);
        CreateProgressBar(card, "Progress", 128f, 100f, 430f, 10f, progress, true);
        CreateText(
            "ProgressValue",
            card,
            value,
            572f,
            88f,
            128f,
            34f,
            18f,
            DarkTextColor,
            TextAlignmentOptions.MidlineRight);

        RectTransform reward = CreateTopLeftRect("Reward", card, 770f, 31f, 190f, 76f);
        AddImage(reward, new Color(0.83f, 0.79f, 0.68f, 0.58f), roundedSprite, Image.Type.Sliced);
        CreateSprite("RewardIcon", reward, rewardSprite, 14f, 11f, 54f, 54f, Color.white);
        CreateText(
            "RewardCount",
            reward,
            "× 10",
            78f,
            0f,
            88f,
            76f,
            24f,
            DarkTextColor,
            TextAlignmentOptions.MidlineLeft);

        RectTransform button = CreateTopLeftRect("StatusButton", card, 1058f, 39f, 196f, 60f);
        Color buttonColor = state switch
        {
            AchievementVisualState.Claimable => GoldColor,
            AchievementVisualState.Claimed => new Color(0.49f, 0.52f, 0.52f, 1f),
            _ => new Color(0.67f, 0.67f, 0.63f, 1f),
        };
        Image buttonImage = AddImage(button, buttonColor, roundedSprite, Image.Type.Sliced);
        buttonImage.raycastTarget = true;
        Button buttonComponent = button.gameObject.AddComponent<Button>();
        buttonComponent.targetGraphic = buttonImage;
        buttonComponent.interactable = state == AchievementVisualState.Claimable;
        CreateText(
            "Label",
            button,
            buttonText,
            0f,
            0f,
            196f,
            60f,
            22f,
            state == AchievementVisualState.Claimable ? Color.white : new Color(0.90f, 0.90f, 0.87f, 1f),
            TextAlignmentOptions.Center);
    }

    static void CreateProgressBar(
        RectTransform parent,
        string name,
        float x,
        float y,
        float width,
        float height,
        float progress,
        bool lightBackground)
    {
        RectTransform background = CreateTopLeftRect(name, parent, x, y, width, height);
        AddImage(
            background,
            lightBackground
                ? new Color(0.52f, 0.55f, 0.54f, 0.30f)
                : new Color(0.04f, 0.09f, 0.10f, 0.52f),
            roundedSprite,
            Image.Type.Sliced);

        RectTransform fill = CreateRect(
            "Fill",
            background,
            new Vector2(0f, 0f),
            new Vector2(Mathf.Clamp01(progress), 1f),
            new Vector2(0f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        AddImage(fill, progress >= 1f ? GoldColor : ProgressColor, roundedSprite, Image.Type.Sliced);
    }

    static RectTransform CreateRect(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
        return rect;
    }

    static RectTransform CreateTopLeftRect(
        string name,
        Transform parent,
        float x,
        float y,
        float width,
        float height)
    {
        return CreateRect(
            name,
            parent,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(x, -y),
            new Vector2(width, height));
    }

    static RectTransform CreateTopRightRect(
        string name,
        Transform parent,
        float right,
        float top,
        float width,
        float height)
    {
        return CreateRect(
            name,
            parent,
            Vector2.one,
            Vector2.one,
            Vector2.one,
            new Vector2(-right, -top),
            new Vector2(width, height));
    }

    static RectTransform CreateStretchRect(
        string name,
        Transform parent,
        float left,
        float bottom,
        float right,
        float top)
    {
        RectTransform rect = CreateRect(
            name,
            parent,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    static Image CreateImage(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        Color color)
    {
        RectTransform rect = CreateRect(
            name,
            parent,
            anchorMin,
            anchorMax,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return AddImage(rect, color);
    }

    static Image AddImage(
        RectTransform rect,
        Color color,
        Sprite sprite = null,
        Image.Type type = Image.Type.Simple)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = type;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static RectTransform CreateSprite(
        string name,
        Transform parent,
        Sprite sprite,
        float x,
        float y,
        float width,
        float height,
        Color color)
    {
        RectTransform rect = CreateTopLeftRect(name, parent, x, y, width, height);
        Image image = AddImage(rect, color, sprite);
        image.preserveAspect = true;
        return rect;
    }

    static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string value,
        float x,
        float y,
        float width,
        float height,
        float size,
        Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        RectTransform rect = CreateTopLeftRect(name, parent, x, y, width, height);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.text = value;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    static void CapturePreview(Canvas canvas)
    {
        Camera camera = canvas.worldCamera;
        if (camera == null)
        {
            return;
        }

        const int width = 1920;
        const int height = 1080;
        RenderTexture renderTexture = new(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D screenshot = new(width, height, TextureFormat.RGB24, false);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            Canvas.ForceUpdateCanvases();
            camera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            camera.Render();
            screenshot.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            screenshot.Apply();
            File.WriteAllBytes(ProjectPath(PreviewRelativePath), screenshot.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(screenshot);
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }
    }

    static string ProjectPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
    }

    enum AchievementVisualState
    {
        InProgress,
        Claimable,
        Claimed,
    }
}
#endif
