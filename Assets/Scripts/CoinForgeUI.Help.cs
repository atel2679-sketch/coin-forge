using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class CoinForgeUI
{
    RectTransform helpLink, helpCard, helpViewport;
    ScrollRect helpScroll;
    Button helpReplay, helpClose;
    Text helpOffline, helpBoosts;

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

        float y = 0;
        HelpTopic(content, ref y, 164, "coin_icon.png", "Tap", "Tap the coin to earn coins.");
        HelpTopic(content, ref y, 221, "bld_01_coin_press.png", "Build",
            "Buy buildings to earn coins automatically. Prices rise with each purchase.");
        HelpTopic(content, ref y, 199, "ic_milestone.png", "Milestones",
            "Every 25 of a building doubles its output.");
        helpOffline = HelpTopic(content, ref y, 218, "ic_offline.png", "Offline", "");
        helpBoosts = HelpTopic(content, ref y, 367, "ic_boost2x.png", "Boosts (optional ads)", "", "ic_timewarp.png");
        HelpTopic(content, ref y, 255, "ic_prestige.png", "Prestige",
            "Reset coins and buildings to earn Gems. Each Gem gives +2% income forever.");
        Place(content, 0, 0, 824, y);

        helpReplay = DialogButton(helpCard, "Replay tutorial", null, 48, 0, 473, ReplayTutorial);
        helpClose = DialogButton(helpCard, "Close", GoldButton, 538, 0, 334, CloseDialog);
    }

    Text HelpTopic(Transform parent, ref float y, float height, string icon, string title, string body, string extraIcon = null)
    {
        var topic = Box(parent, title);
        Place(topic, 0, y, 824, height);
        y += height + 8;
        Solid(topic, "Topic surface", new Color32(49, 34, 24, 255));
        Place(Art(topic, icon).rectTransform, 20, 25, 100, 100);
        if (extraIcon != null) Place(Art(topic, extraIcon).rectTransform, 25, 144, 90, 90);
        Place(Label(topic, title, 46, Gold, TextAnchor.MiddleLeft).rectTransform, 144, 15, 656, 58);
        var detail = Label(topic, body, 42, Ivory, TextAnchor.UpperLeft);
        Place(detail.rectTransform, 144, 77, 656, height - 90);
        return detail;
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
        helpScroll.content.anchoredPosition = Vector2.zero;
        helpScroll.verticalNormalizedPosition = 1;
    }

    // Keep fractional Inspector values visible in the rules.
    static string HelpNumber(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    void LayoutHelp(float designHeight)
    {
        Place(helpLink, 509, 25, targetSize, targetSize);
        float height = Mathf.Min(1490, designHeight - 64);
        helpCard.sizeDelta = new Vector2(920, height);
        Place(helpViewport, 48, 135, 824, Mathf.Max(0, height - targetSize - 199));
        Place((RectTransform)helpReplay.transform, 48, height - targetSize - 36, 473, targetSize);
        Place((RectTransform)helpClose.transform, 538, height - targetSize - 36, 334, targetSize);
    }
}
