using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class CoinForgeUI
{
    const float HelpWidth = 798;
    const float HelpTextWidth = HelpWidth - 168;
    const float HelpFadeHeight = 52;
    static readonly Color HelpSurface = new Color32(49, 34, 24, 255);
    readonly List<(RectTransform root, Text title, Text body, float iconHeight)> helpTopics = new();

    RectTransform helpLink, helpCard, helpViewport, helpScrollBar;
    ScrollRect helpScroll;
    Button helpReplay, helpClose;
    Text helpOffline, helpBoosts;
    RawImage helpFade;
    Texture2D helpFadeTexture;

    void BuildHelp()
    {
        helpCard = Box(modal, "How to Play");
        helpCard.anchorMin = helpCard.anchorMax = new Vector2(0.5f, 0.5f);
        Art(helpCard, "panel_frame.png", true);
        DialogLabel(helpCard, "HOW TO PLAY", 57, 27, 91, Gold);

        helpViewport = Box(helpCard, "Help viewport");
        Solid(helpViewport, "Help scroll input", new Color(0.09f, 0.06f, 0.04f, 0.37f)).raycastTarget = true;
        helpViewport.gameObject.AddComponent<RectMask2D>();
        var content = Box(helpViewport, "Help topics");
        helpScroll = helpViewport.gameObject.AddComponent<ScrollRect>();
        helpScroll.horizontal = false;
        helpScroll.movementType = ScrollRect.MovementType.Clamped;
        helpScroll.viewport = helpViewport;
        helpScroll.content = content;

        HelpTopic(content, "coin_icon.png", "Tap", "Tap the coin to earn coins.");
        HelpTopic(content, "bld_01_coin_press.png", "Build",
            "Buy buildings to earn coins automatically. Prices rise with each purchase.");
        HelpTopic(content, "ic_milestone.png", "Milestones",
            "Every 25 of a building doubles its output.");
        helpOffline = HelpTopic(content, "ic_offline.png", "Offline", "");
        helpBoosts = HelpTopic(content, "ic_boost2x.png", "Boosts (optional ads)", "", "ic_timewarp.png");
        HelpTopic(content, "ic_prestige.png", "Prestige",
            "Reset coins and buildings to earn Gems. Each Gem gives +2% income forever.");

        BuildHelpScrollCues();
        helpReplay = DialogButton(helpCard, "Replay tutorial", null, 48, 0, 473, ReplayTutorial);
        helpClose = DialogButton(helpCard, "Close", GoldButton, 538, 0, 334, CloseDialog);
    }

    Text HelpTopic(Transform parent, string icon, string title, string body, string extraIcon = null)
    {
        var topic = Box(parent, title);
        Solid(topic, "Topic surface", HelpSurface);
        Place(Art(topic, icon).rectTransform, 20, 25, 100, 100);
        if (extraIcon != null) Place(Art(topic, extraIcon).rectTransform, 25, 144, 90, 90);
        var heading = Label(topic, title, 46, Gold, TextAnchor.UpperLeft);
        var detail = Label(topic, body, 42, Ivory, TextAnchor.UpperLeft);
        heading.resizeTextForBestFit = detail.resizeTextForBestFit = false;
        heading.verticalOverflow = detail.verticalOverflow = VerticalWrapMode.Overflow;
        helpTopics.Add((topic, heading, detail, extraIcon == null ? 141 : 254));
        return detail;
    }

    void LayoutHelpTopics()
    {
        float y = 0;
        foreach (var topic in helpTopics)
        {
            float titleHeight = PlaceHelpText(topic.title, 16, 46);
            float bodyHeight = PlaceHelpText(topic.body, 22 + titleHeight, 42);
            float height = Mathf.Max(topic.iconHeight, 42 + titleHeight + bodyHeight);
            Place(topic.root, 0, y, HelpWidth, height);
            y += height + 8;
        }
        var content = helpScroll.content;
        float scrollY = Mathf.Clamp(content.anchoredPosition.y, 0, Mathf.Max(0, y - helpViewport.rect.height));
        Place(content, 0, -scrollY, HelpWidth, y);
        UpdateHelpFade(helpScroll.normalizedPosition);
    }

    static float PlaceHelpText(Text text, float y, int fontSize)
    {
        text.fontSize = Mathf.Max(fontSize, text.resizeTextMinSize);
        Place(text.rectTransform, 144, y, HelpTextWidth, 0);
        float height = Mathf.Ceil(text.preferredHeight) + 4;
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        return height;
    }

    void BuildHelpScrollCues()
    {
        helpScrollBar = Box(helpCard, "Help scrollbar");
        Solid(helpScrollBar, "Scrollbar hit area", Color.clear).raycastTarget = true;
        var rail = Solid(helpScrollBar, "Scroll track", new Color32(38, 26, 17, 255));
        Area(rail.rectTransform, 0.33f, 0, 0.67f, 1);
        var handle = Solid(rail.transform, "Scroll thumb", Gold);
        var bar = helpScrollBar.gameObject.AddComponent<Scrollbar>();
        bar.handleRect = handle.rectTransform;
        bar.targetGraphic = handle;
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.navigation = new Navigation { mode = Navigation.Mode.None };
        helpScroll.verticalScrollbar = bar;
        helpScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        helpFadeTexture = new Texture2D(1, 32, TextureFormat.RGBA32, false)
        {
            name = "Help scroll fade",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        for (int i = 0; i < helpFadeTexture.height; i++)
            helpFadeTexture.SetPixel(0, i, new Color(1, 1, 1, 1 - i / 31f));
        helpFadeTexture.Apply(false, true);

        var fadeRect = Box(helpViewport, "More topics fade");
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.right;
        fadeRect.pivot = new Vector2(0.5f, 0);
        fadeRect.anchoredPosition = Vector2.zero;
        fadeRect.sizeDelta = new Vector2(0, HelpFadeHeight);
        helpFade = fadeRect.gameObject.AddComponent<RawImage>();
        helpFade.texture = helpFadeTexture;
        helpFade.raycastTarget = false;
        helpScroll.onValueChanged.AddListener(UpdateHelpFade);
    }

    void UpdateHelpFade(Vector2 scroll)
    {
        float overflow = Mathf.Max(0, helpScroll.content.rect.height - helpViewport.rect.height);
        Color color = HelpSurface;
        color.a = Mathf.Clamp01(scroll.y * overflow / HelpFadeHeight);
        helpFade.color = color;
    }

    void OpenHelp()
    {
        if (dialog != Dialog.None || InTutorial || adInFlight) return;
        var engine = IdleEngine.I;
        Set(helpOffline, "You keep earning while away (up to " + HelpNumber(offline.maxOfflineHours) +
            "h at " + HelpNumber(offline.offlineEfficiency * 100) + "%).");
        Set(helpBoosts, "2x income for " + HelpNumber(engine.boostHoursPerAd) + "h (stacks up to " +
            HelpNumber(engine.boostMaxHours) + "h). Time Warp instantly gives " +
            HelpNumber(engine.timeWarpHours) + "h of income. You can also double your offline earnings.");
        Open(Dialog.Help);
        helpScroll.StopMovement();
        LayoutHelpTopics();
        helpScroll.content.anchoredPosition = Vector2.zero;
        helpScroll.verticalNormalizedPosition = 1;
        UpdateHelpFade(Vector2.up);
    }

    // Keep fractional Inspector values visible in the rules.
    static string HelpNumber(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    void LayoutHelp(float designHeight)
    {
        Place(helpLink, 509, 25, targetSize, targetSize);
        float height = Mathf.Min(1490, designHeight - 64);
        helpCard.sizeDelta = new Vector2(920, height);
        float viewportHeight = Mathf.Max(0, height - targetSize - 199);
        Place(helpViewport, 48, 135, HelpWidth, viewportHeight);
        Place(helpScrollBar, 852, 135, 24, viewportHeight);
        Place((RectTransform)helpReplay.transform, 48, height - targetSize - 36, 473, targetSize);
        Place((RectTransform)helpClose.transform, 538, height - targetSize - 36, 334, targetSize);
        if (dialog == Dialog.Help) LayoutHelpTopics();
    }

    void OnDestroy()
    {
        if (helpFadeTexture != null) Destroy(helpFadeTexture);
    }
}
