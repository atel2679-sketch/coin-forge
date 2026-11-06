using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed partial class CoinForgeUI
{
    const string TutorialKey = "ic_tutorial_done";
    const int TutorialTaps = 5;
    const int TutorialSteps = 5;

    RectTransform tutorial, tutorialCard, tutorialHand, tutorialBlock;
    readonly RectTransform[] tutorialShade = new RectTransform[4];
    readonly RectTransform[] tutorialBorder = new RectTransform[4];
    Text tutorialStepText, tutorialMessage, tutorialDetail, tutorialNextText;
    Button tutorialSkip, tutorialNext;
    int tutorialStep, tutorialTapCount, previousQuantity;
    bool replayOwnedPress, waitingForPress, restoreAdManager, tutorialBannerHidden;
    AdManager tutorialAdManager;
    Vector2 handRest;

    bool InTutorial => tutorialStep != 0;
    bool CanAffordPress => IdleEngine.I.Coins >= IdleEngine.I.Upgrades.CostOf(0, 1);

    void CheckFirstTutorial()
    {
        if (PlayerPrefs.GetInt(TutorialKey) == 1) return;
        foreach (int owned in IdleEngine.I.Upgrades.Owned)
        {
            if (owned == 0) continue;
            SaveTutorialDone();
            return;
        }
        BeginTutorial(false);
    }

    static void SaveTutorialDone()
    {
        PlayerPrefs.SetInt(TutorialKey, 1);
        PlayerPrefs.Save();
    }

    void ReplayTutorial()
    {
        if (adInFlight) return;
        CloseDialog();
        PlayerPrefs.DeleteKey(TutorialKey);
        PlayerPrefs.Save();
        BeginTutorial(true);
    }

    void BeginTutorial(bool replay)
    {
        EventSystem.current.SetSelectedGameObject(null);
        previousQuantity = quantity;
        Choose(1);
        upgradeScroll.StopMovement();
        upgradeScroll.verticalNormalizedPosition = 1;
        list.anchoredPosition = Vector2.zero;
        upgradeScroll.enabled = false;
        replayOwnedPress = replay && IdleEngine.I.Upgrades.Owned[0] > 0;
        tutorialTapCount = 0;
        waitingForPress = false;
        tutorial.gameObject.SetActive(true);
        SuppressTutorialAds();
        SetTutorialStep(1);
    }

    void SuppressTutorialAds()
    {
        tutorialAdManager = AdManager.I;
        if (tutorialAdManager == null) return;
        restoreAdManager = tutorialAdManager.enabled;
        tutorialAdManager.enabled = false;
        HideTutorialBanner();
    }

    void HideTutorialBanner()
    {
        if (tutorialAdManager == null) return;
        tutorialBannerHidden = tutorialAdManager.BannerHeightPx > 0;
        tutorialAdManager.HideBanner();
    }

    void ReleaseTutorialAds()
    {
        if (tutorialAdManager == null) return;
        tutorialAdManager.enabled = restoreAdManager;
        tutorialAdManager.RestoreBanner();
        tutorialAdManager = null;
        tutorialBannerHidden = false;
    }

    void FinishTutorial()
    {
        tutorialStep = 0;
        tutorial.gameObject.SetActive(false);
        upgradeScroll.enabled = true;
        SaveTutorialDone();
        ReleaseTutorialAds();
        Choose(previousQuantity);
        if (offline != null && offline.PendingAmount > 0) Open(Dialog.Offline);
    }

    void OnTutorialTap()
    {
        if (tutorialStep != 1) return;
        tutorialTapCount++;
        RefreshTutorial();
    }

    void RefreshTutorial()
    {
        if (tutorialStep != 1) return;
        if (tutorialTapCount >= TutorialTaps && (CanAffordPress || replayOwnedPress))
        {
            RefreshRow(0, IdleEngine.I);
            SetTutorialStep(2);
            return;
        }

        if (tutorialTapCount >= TutorialTaps)
        {
            if (!waitingForPress)
            {
                waitingForPress = true;
                Set(tutorialMessage, "Keep tapping to afford a Coin Press.");
            }
            double remaining = IdleEngine.I.Upgrades.CostOf(0, 1) - IdleEngine.I.Coins;
            Set(tutorialDetail, BigNum.Format(System.Math.Ceiling(remaining)) + " more coins to build");
        }
        else Set(tutorialDetail, BigNum.Format(tutorialTapCount) + " / " + BigNum.Format(TutorialTaps) + " taps");
    }

    void AdvanceTutorial()
    {
        if (tutorialStep == TutorialSteps) FinishTutorial();
        else if (tutorialStep >= 3 || (tutorialStep == 2 && replayOwnedPress)) SetTutorialStep(tutorialStep + 1);
    }

    void SetTutorialStep(int step)
    {
        tutorialStep = step;
        RefreshAdControls();
        Set(tutorialStepText, "STEP " + BigNum.Format(step) + " OF " + BigNum.Format(TutorialSteps));
        Set(tutorialMessage, step switch
        {
            1 => "Tap the coin to forge coins!",
            2 => replayOwnedPress ? "You already own a Coin Press. It forges coins for you automatically."
                : "Buy a Coin Press. It forges coins for you automatically.",
            3 => "Your forge now earns coins every second — even while you're away.",
            4 => "Every 25 of the same building doubles its output.",
            _ => "Want a speed-up? Watch an optional ad for 2x income for 2 hours."
        });
        tutorialDetail.gameObject.SetActive(step == 1);
        tutorialNext.gameObject.SetActive(step >= 3 || (step == 2 && replayOwnedPress));
        Set(tutorialNextText, step == TutorialSteps ? "Got it" : "Next");
        tutorialBlock.gameObject.SetActive(step >= 3);
        if (step == 1) RefreshTutorial();
        LayoutTutorial();
    }

    void BuildTutorial()
    {
        tutorial = Box(design, "Tutorial overlay");
        for (int i = 0; i < tutorialShade.Length; i++)
        {
            Image shade = Solid(tutorial, "Tutorial scrim", new Color(0.045f, 0.028f, 0.02f, 0.86f));
            shade.raycastTarget = true;
            tutorialShade[i] = shade.rectTransform;
            tutorialBorder[i] = Solid(tutorial, "Spotlight border", Gold).rectTransform;
        }
        var block = Solid(tutorial, "Read-only spotlight", Color.clear);
        block.raycastTarget = true;
        tutorialBlock = block.rectTransform;

        tutorialHand = Box(tutorial, "Pointing hand");
        var hand = tutorialHand.gameObject.AddComponent<CoinForgeTutorialHand>();
        hand.color = Ivory;
        hand.raycastTarget = false;
        var stroke = tutorialHand.gameObject.AddComponent<Outline>();
        stroke.effectColor = Ink;
        stroke.effectDistance = new Vector2(4, -4);

        tutorialCard = Box(tutorial, "Tutorial instruction");
        Art(tutorialCard, "panel_frame.png", true);
        tutorialStepText = Label(tutorialCard, "", 36, Gold, TextAnchor.MiddleLeft);
        tutorialMessage = Label(tutorialCard, "", 51, Ivory, TextAnchor.MiddleLeft);
        tutorialDetail = Label(tutorialCard, "", 36, Muted, TextAnchor.MiddleLeft);
        tutorialNext = ActionButton(tutorialCard, "Continue tutorial", GoldButton, AdvanceTutorial);
        tutorialNextText = Label(tutorialNext.transform, "Next", 48, Ink);
        ButtonContent(tutorialNextText.rectTransform, 18, 18);
        tutorialSkip = DialogButton(tutorial, "Skip", null, 0, 0, 190, FinishTutorial);
        tutorial.gameObject.SetActive(false);
    }

    void LayoutTutorial()
    {
        if (!InTutorial) return;
        RectTransform target = tutorialStep switch
        {
            1 => tapButton,
            2 => (RectTransform)rows[0].Background.transform.parent,
            3 => income.rectTransform,
            4 => (RectTransform)rows[0].Progress.parent,
            _ => (RectTransform)boostAd.transform
        };
        Rect focus = TutorialBounds(target);
        if (tutorialStep == 4) focus = Rect.MinMaxRect(focus.xMin - 12, focus.yMin - 16, focus.xMax + 12, focus.yMax + 16);
        float height = design.rect.height;
        Place(tutorialShade[0], 0, 0, Width, focus.yMin);
        Place(tutorialShade[1], 0, focus.yMax, Width, height - focus.yMax);
        Place(tutorialShade[2], 0, focus.yMin, focus.xMin, focus.height);
        Place(tutorialShade[3], focus.xMax, focus.yMin, Width - focus.xMax, focus.height);
        const float rim = 4;
        Place(tutorialBorder[0], focus.xMin - rim, focus.yMin - rim, focus.width + 2 * rim, rim);
        Place(tutorialBorder[1], focus.xMin - rim, focus.yMax, focus.width + 2 * rim, rim);
        Place(tutorialBorder[2], focus.xMin - rim, focus.yMin, rim, focus.height);
        Place(tutorialBorder[3], focus.xMax, focus.yMin, rim, focus.height);
        Place(tutorialBlock, focus.xMin, focus.yMin, focus.width, focus.height);

        const float handSize = 94;
        float pointX = tutorialStep == 2 ? TutorialBounds((RectTransform)rows[0].Buy.transform).center.x : focus.center.x;
        Place(tutorialHand, pointX - handSize * 0.4f, focus.yMax + 9, handSize, handSize);
        handRest = tutorialHand.anchoredPosition;

        bool hasNext = tutorialNext.gameObject.activeSelf;
        float cardHeight = hasNext ? 228 + targetSize : 268;
        float below = focus.yMax + 113;
        float tipY = below + cardHeight < height - 24 ? below : Mathf.Max(164, focus.yMin - cardHeight - 34);
        Place(tutorialCard, 93, tipY, 894, cardHeight);
        Place(tutorialStepText.rectTransform, 42, 22, 810, 53);
        Place(tutorialMessage.rectTransform, 42, 75, 810, 134);
        Place(tutorialDetail.rectTransform, 42, 206, 810, 49);
        Place((RectTransform)tutorialNext.transform, 567, 207, 285, targetSize);
        Place((RectTransform)tutorialSkip.transform, 857, 25, 191, targetSize);
    }

    Rect TutorialBounds(RectTransform rect)
    {
        Vector2 topLeft = design.InverseTransformPoint(rect.TransformPoint(new Vector2(rect.rect.xMin, rect.rect.yMax)));
        Vector2 bottomRight = design.InverseTransformPoint(rect.TransformPoint(new Vector2(rect.rect.xMax, rect.rect.yMin)));
        return Rect.MinMaxRect(topLeft.x - design.rect.xMin, -topLeft.y,
            bottomRight.x - design.rect.xMin, -bottomRight.y);
    }

    void LateUpdate()
    {
        if (!InTutorial) return;
        if (!tutorialBannerHidden) HideTutorialBanner();
        float bob = (Mathf.Sin(Time.unscaledTime * 5.5f) + 1) * 7;
        tutorialHand.anchoredPosition = handRest + Vector2.up * bob;
    }
}
