using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class CoinForgeUI
{
    const float Width = 1080;
    const float BannerReferencePx = 167;
    const string GoldButton = "btn_gold.png";
    const string GrayButton = "btn_gray.png";
    const string GreenButton = "btn_green.png";
    static readonly int[] ModeCounts = { 1, 10, 0 };
    static readonly Color Gold = new Color32(242, 179, 61, 255);
    static readonly Color Ivory = new Color32(255, 241, 217, 255);
    static readonly Color Muted = new Color32(222, 199, 169, 255);
    static readonly Color Ink = new Color32(38, 24, 16, 255);
    static readonly Color ShopRow = new Color32(54, 38, 27, 255);
    static readonly Color MutedRow = new Color32(55, 50, 47, 255);

    sealed class Row
    {
        public Image Background, Icon, Locked, BuyGold, BuyGray;
        public Button Buy;
        public Text Name, Count, Rate, Price, BuyLabel, Milestone;
        public RectTransform Progress;
        public bool Known;
        public int Owned = -1, BuyCount = -1;
        public double Production = -1, PriceValue = -1;
    }

    readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    readonly List<(Text label, int size)> labels = new List<(Text, int)>();
    readonly Image[] modes = new Image[3];
    Font font;
    Canvas canvas;
    RectTransform safe, design, stage, shop, modeBar, viewport, list, statsLink, prestigeLink;
    RectTransform tapButton, tapHint, adRail, modal, offlineCard, prestigeCard, statsCard;
    Text coins, income, gems, boostState, boostAdStatus, warpAdStatus, offlineTime, offlineCoins;
    Text offlineAdText, offlineAdStatus, prestigeGems, statsLifetime, statsTime, statsTop;
    Button boostAd, warpAd, offlineAd, offlineCollect, prestigeCancel, prestigeConfirm, statsClose;
    CanvasGroup boostBadge;
    Row[] rows;
    int shownBoostSeconds = -1, screenW, screenH;
    Rect screenSafe;
    float lastBanner = -1, lastScale, density, scaledDensity;
    float rowHeight, targetSize;

    void Build()
    {
        foreach (Sprite sprite in artwork) sprites.Add(sprite.name + ".png", sprite);
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        canvas = GetComponent<Canvas>();
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("Coin Forge Input", typeof(EventSystem), typeof(StandaloneInputModule))
                .transform.SetParent(transform, false);

        var backdrop = Art(transform, "bg_main.png");
        backdrop.color = new Color32(219, 211, 204, 255);
        var crop = backdrop.gameObject.AddComponent<AspectRatioFitter>();
        crop.aspectRatio = 1080f / 2340;
        crop.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

        safe = Box(transform, "Safe area above banner");
        safe.gameObject.AddComponent<RectMask2D>();
        design = Box(safe, "Portrait layout");
        design.anchorMin = design.anchorMax = new Vector2(0.5f, 1);
        design.pivot = new Vector2(0.5f, 1);

        BuildHud();
        BuildStage();
        BuildShop();
        BuildDialogs();
    }

    void BuildHud()
    {
        var hud = Box(design, "HUD");
        hud.gameObject.AddComponent<Canvas>();
        Place(hud, 24, 22, 1032, 240);
        Art(hud, "panel_frame.png", true).color = new Color(0.77f, 0.69f, 0.64f);
        Text title = Label(hud, "COIN FORGE", 36, Gold, TextAnchor.MiddleLeft);
        Place(title.rectTransform, 35, 18, 490, 49);
        Place(Art(hud, "coin_icon.png").rectTransform, 35, 82, 92, 92);
        coins = Label(hud, "", 78, Ivory, TextAnchor.MiddleLeft);
        Place(coins.rectTransform, 140, 62, 530, 100);
        income = Label(hud, "", 42, new Color32(155, 235, 182, 255), TextAnchor.MiddleLeft);
        Place(income.rectTransform, 145, 162, 520, 52);

        var pocket = Box(hud, "Gems");
        Place(pocket, 688, 24, 307, 78);
        Solid(pocket, "Gem well", new Color32(48, 30, 31, 255));
        Place(Art(pocket, "gem_icon.png").rectTransform, 14, 8, 65, 62);
        gems = Label(pocket, "", 49, Ivory, TextAnchor.MiddleLeft);
        Place(gems.rectTransform, 93, 0, 195, 78);

        var badge = Box(hud, "Boost status");
        Place(badge, 654, 115, 341, 95);
        boostBadge = badge.gameObject.AddComponent<CanvasGroup>();
        Solid(badge, "Boost well", new Color32(53, 34, 20, 255));
        Place(Art(badge, "ic_boost2x.png").rectTransform, 9, 18, 60, 58);
        boostState = Label(badge, "", 35, Gold, TextAnchor.MiddleLeft);
        Place(boostState.rectTransform, 72, 0, 258, 95);
    }

    void BuildStage()
    {
        stage = Box(design, "Forge");
        tapButton = Box(stage, "Tap coin");
        var hit = Solid(tapButton, "Touch target", Color.clear);
        hit.raycastTarget = true;
        var animation = Box(tapButton, "Coin animation");
        animation.gameObject.AddComponent<Canvas>();
        var coin = Art(animation, "coin_main.png");
        var tap = tapButton.gameObject.AddComponent<Button>();
        tap.targetGraphic = hit;
        tap.transition = Selectable.Transition.None;
        tap.onClick.AddListener(Tap);
        tapHint = Label(stage, "TAP TO FORGE", 39, Gold).rectTransform;
        var shadow = tapHint.gameObject.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(0, -3);
        shadow.effectColor = Ink;

        adRail = Box(stage, "Rewards rail");
        Place(Label(adRail, "AD REWARDS", 35, Muted).rectTransform, 0, 0, 388, 58);
        boostAd = BuildAd(adRail, "Boost ad", "ic_boost2x.png", "2x Boost for 2h", out boostAdStatus,
            () => ShowAd(RewardType.Boost2x));
        warpAd = BuildAd(adRail, "Time Warp ad", "ic_timewarp.png", "Time Warp +4h", out warpAdStatus,
            () => ShowAd(RewardType.TimeWarp));
        BuildTapEffects(stage, coin.rectTransform);
    }

    Button BuildAd(Transform parent, string name, string icon, string title, out Text status, UnityAction action)
    {
        Button button = ActionButton(parent, name, GreenButton, action);
        var root = (RectTransform)button.transform;
        Area(Art(root, icon).rectTransform, 0.045f, 0.50f, 0.194f, 0.92f);
        Area(Label(root, title, 40, Ink).rectTransform, 0.05f, 0.07f, 0.96f, 0.51f);
        status = Label(root, "", 34, Ink, TextAnchor.MiddleLeft);
        Area(status.rectTransform, 0.21f, 0.51f, 0.96f, 0.90f);
        return button;
    }

    void BuildShop()
    {
        shop = Box(design, "Upgrade shop");
        Art(shop, "panel_frame.png", true);
        var heading = Label(shop, "UPGRADES", 61, Ivory, TextAnchor.MiddleLeft);
        Place(heading.rectTransform, 40, 22, 445, 118);
        statsLink = FeatureButton(shop, "Stats", "ic_milestone.png", OpenStats);
        prestigeLink = FeatureButton(shop, "Prestige", "ic_prestige.png", () => Open(Dialog.Prestige));

        modeBar = Box(shop, "Purchase quantity");
        for (int i = 0; i < modes.Length; i++)
        {
            int count = ModeCounts[i];
            string name = count == 0 ? "Max" : "x" + BigNum.Format(count);
            Button mode = ActionButton(modeBar, name, GrayButton, () => Choose(count));
            var rect = (RectTransform)mode.transform;
            modes[i] = Art(rect, GoldButton, true);
            modes[i].enabled = count == quantity;
            Label(rect, name, 43, Ink);
            Place(rect, i * 329, 0, 315, 120);
        }

        viewport = Box(shop, "Clipped upgrade list");
        Solid(viewport, "Scroll input", new Color(0, 0, 0, 0.12f)).raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        list = Box(viewport, "Building rows");
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = list;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 96;

        string[] icons = {
            "bld_01_coin_press.png", "bld_02_mini_forge.png", "bld_03_smelter.png", "bld_04_foundry.png",
            "bld_05_mint.png", "bld_06_vault.png", "bld_07_bank.png", "bld_08_exchange.png",
            "bld_09_treasury.png", "bld_10_galaxy_mint.png"
        };
        rows = new Row[icons.Length];
        for (int i = 0; i < rows.Length; i++) rows[i] = BuildRow(i, icons[i]);
    }

    RectTransform FeatureButton(Transform parent, string name, string icon, UnityAction action)
    {
        var root = (RectTransform)ActionButton(parent, name, GrayButton, action).transform;
        Area(Art(root, icon).rectTransform, 0.04f, 0.16f, 0.32f, 0.84f);
        Area(Label(root, name, 36, Ink, TextAnchor.MiddleLeft).rectTransform, 0.32f, 0.08f, 0.97f, 0.88f);
        return root;
    }

    Row BuildRow(int index, string icon)
    {
        var root = Box(list, "Building " + (index + 1));
        var row = new Row { Background = Solid(root, "Row background", MutedRow) };
        row.Icon = Art(root, icon);
        row.Locked = Art(root, "bld_locked.png");
        row.Icon.enabled = false;
        Area(row.Icon.rectTransform, 0.009f, 0.13f, 0.14f, 0.87f);
        Area(row.Locked.rectTransform, 0.009f, 0.13f, 0.14f, 0.87f);
        row.Name = Label(root, "???", 46, Ivory, TextAnchor.MiddleLeft);
        row.Count = Label(root, "", 36, Gold, TextAnchor.MiddleRight);
        row.Rate = Label(root, "", 37, Muted, TextAnchor.MiddleLeft);
        row.Milestone = Label(root, "", 34, Muted, TextAnchor.MiddleRight);
        Area(row.Name.rectTransform, 0.152f, 0.06f, 0.56f, 0.39f);
        Area(row.Count.rectTransform, 0.56f, 0.06f, 0.678f, 0.39f);
        Area(row.Rate.rectTransform, 0.152f, 0.38f, 0.56f, 0.74f);
        Area(row.Milestone.rectTransform, 0.55f, 0.38f, 0.678f, 0.74f);
        var track = Solid(root, "Milestone track", new Color32(30, 24, 20, 255));
        Area(track.rectTransform, 0.152f, 0.80f, 0.678f, 0.875f);
        row.Progress = Solid(track.transform, "Milestone fill", Gold).rectTransform;

        row.Buy = ActionButton(root, "Buy", null, () => Buy(index));
        var buy = (RectTransform)row.Buy.transform;
        Area(buy, 0.699f, 0.084f, 0.988f, 0.916f);
        row.BuyGold = Art(buy, GoldButton, true);
        row.BuyGray = Art(buy, GrayButton, true);
        row.Price = Label(buy, "", 50, Ink);
        row.BuyLabel = Label(buy, "", 34, Ink);
        Area(row.Price.rectTransform, 0.06f, 0.06f, 0.94f, 0.56f);
        Area(row.BuyLabel.rectTransform, 0.06f, 0.54f, 0.94f, 0.91f);
        return row;
    }

    void BuildDialogs()
    {
        modal = Box(design, "Modal layer");
        Solid(modal, "Modal shield", new Color(0.075f, 0.045f, 0.03f, 0.88f)).raycastTarget = true;

        offlineCard = Card("Welcome Back", "ic_offline.png", "WELCOME BACK");
        offlineTime = DialogLabel(offlineCard, "", 42, 224, 62, Muted);
        offlineCoins = DialogLabel(offlineCard, "", 72, 300, 92, Gold);
        offlineAd = ActionButton(offlineCard, "Double offline coins", GreenButton, () => ShowAd(RewardType.OfflineDouble));
        Place((RectTransform)offlineAd.transform, 54, 426, 812, 138);
        Area(Art(offlineAd.transform, "ic_ad_badge.png").rectTransform, 0.03f, 0.18f, 0.14f, 0.78f);
        offlineAdText = Label(offlineAd.transform, "", 43, Ink);
        Area(offlineAdText.rectTransform, 0.15f, 0.06f, 0.96f, 0.53f);
        offlineAdStatus = Label(offlineAd.transform, "", 37, Ink);
        Area(offlineAdStatus.rectTransform, 0.15f, 0.52f, 0.96f, 0.90f);
        offlineCollect = DialogButton(offlineCard, "Collect", GrayButton, 54, 580, 812, Collect);

        prestigeCard = Card("Prestige", "ic_prestige.png", "PRESTIGE");
        prestigeGems = DialogLabel(prestigeCard, "", 64, 227, 95, Gold);
        DialogLabel(prestigeCard, "Prestige resets your coins and buildings. Gems increase future production.",
            44, 332, 180, Ivory);
        prestigeCancel = DialogButton(prestigeCard, "Cancel", GrayButton, 54, 559, 394, CloseDialog);
        prestigeConfirm = DialogButton(prestigeCard, "Prestige", GoldButton, 472, 559, 394, () => IdleEngine.I.Prestige());

        statsCard = Card("Milestone and stats", "ic_milestone.png", "MILESTONES / STATS");
        statsLifetime = Stat(statsCard, "Lifetime coins", 226);
        statsTime = Stat(statsCard, "Play time", 342);
        statsTop = Stat(statsCard, "Top building", 458);
        statsClose = DialogButton(statsCard, "Close", GoldButton, 54, 595, 812, CloseDialog);
        modal.gameObject.SetActive(false);
    }

    RectTransform Card(string name, string icon, string title)
    {
        var card = Box(modal, name);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(920, 780);
        Art(card, "panel_frame.png", true);
        Place(Art(card, icon).rectTransform, 401, 29, 118, 118);
        DialogLabel(card, title, 50, 151, 69, Gold);
        return card;
    }

    Text Stat(Transform parent, string title, float y)
    {
        DialogLabel(parent, title, 38, y, 46, Muted);
        return DialogLabel(parent, "", 53, y + 46, 65, Ivory);
    }

    Text DialogLabel(Transform parent, string text, int size, float y, float height, Color color)
    {
        var label = Label(parent, text, size, color);
        Place(label.rectTransform, 54, y, 812, height);
        return label;
    }

    Button DialogButton(Transform parent, string text, string image, float x, float y, float width, UnityAction action)
    {
        var button = ActionButton(parent, text, image, action);
        Area(Label(button.transform, text, 48, Ink).rectTransform, 0.04f, 0.08f, 0.96f, 0.90f);
        Place((RectTransform)button.transform, x, y, width, 138);
        return button;
    }

    void OnRectTransformDimensionsChange()
    {
        if (modal != null) Reflow();
    }

    void Reflow()
    {
        int width = Screen.width;
        int height = Screen.height;
        Rect bounds = Screen.safeArea;
        if (width <= 0 || height <= 0 || bounds.width <= 0 || bounds.height <= 0) return;
        float banner = AdManager.I != null ? AdManager.I.BannerHeightPx : 0;
        if (width != screenW || height != screenH)
        {
            ReadDensity(width);
            screenW = width;
            screenH = height;
        }
        else if (bounds == screenSafe && banner == lastBanner && canvas.scaleFactor == lastScale) return;

        screenSafe = bounds;
        lastBanner = banner;
        lastScale = canvas.scaleFactor;
        float bottom = bounds.yMin + (banner > 0 ? banner : BannerReferencePx * width / Width) + 16 * density;
        bottom = Mathf.Min(bottom, bounds.yMax);
        safe.anchorMin = new Vector2(bounds.xMin / width, bottom / height);
        safe.anchorMax = new Vector2(bounds.xMax / width, bounds.yMax / height);
        safe.offsetMin = safe.offsetMax = Vector2.zero;

        float safeHeight = bounds.yMax - bottom;
        float pxPerUnit = Mathf.Max(0.01f, Mathf.Min(bounds.width, safeHeight * 0.72f) / Width);
        float designHeight = safeHeight / pxPerUnit;
        design.sizeDelta = new Vector2(Width, designHeight);
        design.localScale = Vector3.one * (pxPerUnit / canvas.scaleFactor);
        targetSize = Mathf.Max(120, Mathf.Ceil(48 * density / pxPerUnit));
        LayoutDialogButtons();
        int minText = Mathf.CeilToInt(14 * scaledDensity / pxPerUnit);
        foreach (var item in labels)
        {
            item.label.resizeTextMinSize = minText;
            item.label.resizeTextMaxSize = Mathf.Max(item.size, minText);
        }

        float shopY = Mathf.Clamp(designHeight * 0.50f, 807, 1060);
        float stageHeight = shopY - 267;
        Place(stage, 24, 267, 1032, stageHeight);
        float coinSize = Mathf.Min(634, stageHeight - 43);
        Place(tapButton, (650 - coinSize) * 0.5f, (stageHeight - coinSize) * 0.44f - 8, coinSize, coinSize);
        Place(tapHint, 0, stageHeight - 73, 650, 62);
        float adHeight = Mathf.Max(142, targetSize);
        float railHeight = 58 + 2 * adHeight + 16;
        Place(adRail, 631, (stageHeight - railHeight) * 0.48f, 388, railHeight);
        Place((RectTransform)boostAd.transform, 0, 58, 388, adHeight);
        Place((RectTransform)warpAd.transform, 0, 74 + adHeight, 388, adHeight);

        Place(shop, 24, shopY, 1032, designHeight - shopY);
        Place(statsLink, 488, 20, 243, targetSize);
        Place(prestigeLink, 744, 20, 249, targetSize);
        Place(modeBar, 28, 32 + targetSize, 973, targetSize);
        foreach (var mode in modes) ((RectTransform)mode.transform.parent).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetSize);
        float listY = 44 + 2 * targetSize;
        float listHeight = Mathf.Max(0, designHeight - shopY - listY - 16);
        Place(viewport, 26, listY, 980, listHeight);
        rowHeight = Mathf.Max(165, targetSize / 0.832f);
        float contentY = Mathf.Clamp(list.anchoredPosition.y, 0, Mathf.Max(0, rows.Length * (rowHeight + 10) - listHeight));
        Place(list, 0, -contentY, 980, rows.Length * (rowHeight + 10) - 10);
        for (int i = 0; i < rows.Length; i++)
            Place(rows[i].Background.rectTransform.parent as RectTransform, 0, i * (rowHeight + 10), 980, rowHeight);
    }

    void LayoutDialogButtons()
    {
        float height = Mathf.Max(138, targetSize);
        float extra = height - 138;
        offlineCard.sizeDelta = new Vector2(920, 780 + 2 * extra);
        prestigeCard.sizeDelta = new Vector2(920, 780 + extra);
        statsCard.sizeDelta = new Vector2(920, 780 + extra);
        Place((RectTransform)offlineAd.transform, 54, 426, 812, height);
        Place((RectTransform)offlineCollect.transform, 54, 580 + extra, 812, height);
        Place((RectTransform)prestigeCancel.transform, 54, 559, 394, height);
        Place((RectTransform)prestigeConfirm.transform, 472, 559, 394, height);
        Place((RectTransform)statsClose.transform, 54, 595, 812, height);
    }

    void ReadDensity(int width)
    {
        density = scaledDensity = width / 393f;
#if UNITY_ANDROID && !UNITY_EDITOR
        using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
        using var resources = activity.Call<AndroidJavaObject>("getResources");
        using var metrics = resources.Call<AndroidJavaObject>("getDisplayMetrics");
        density = metrics.Get<float>("density");
        scaledDensity = metrics.Get<float>("scaledDensity");
#endif
    }

    static RectTransform Box(Transform parent, string name)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Area(rect, 0, 0, 1, 1);
        return rect;
    }

    static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    // Normalized rectangles use a top-left origin, like Place.
    static void Area(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = new Vector2(left, 1 - bottom);
        rect.anchorMax = new Vector2(right, 1 - top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static Image Solid(Transform parent, string name, Color color)
    {
        Image image = Box(parent, name).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    Image Art(Transform parent, string file, bool sliced = false)
    {
        var image = Solid(parent, file, Color.white);
        image.sprite = sprites[file];
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.preserveAspect = !sliced;
        return image;
    }

    Text Label(Transform parent, string value, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var label = Box(parent, "Label").gameObject.AddComponent<Text>();
        label.font = font;
        label.fontStyle = FontStyle.Bold;
        label.color = color;
        label.text = value;
        label.fontSize = size;
        label.alignment = align;
        label.resizeTextForBestFit = true;
        label.raycastTarget = false;
        labels.Add((label, size));
        return label;
    }

    Button ActionButton(Transform parent, string name, string sprite, UnityAction action)
    {
        var root = Box(parent, name);
        Image image = sprite == null ? Solid(root, "Touch target", Color.clear) : Art(root, sprite, true);
        image.raycastTarget = true;
        var button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(0.67f, 0.64f, 0.61f);
        colors.pressedColor = new Color(0.79f, 0.67f, 0.53f);
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(action);
        return button;
    }
}
