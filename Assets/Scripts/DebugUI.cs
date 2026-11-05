using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>TEMPORARY debug UI for IC-01 (built at runtime, no assets). Final UI = IC-02. Only uses GameEvents + public APIs.</summary>
public class DebugUI : MonoBehaviour
{
    Font font;
    Canvas canvas;
    Text coinsTxt, infoTxt, offlineTxt, statusTxt;
    Button boostBtn, warpBtn, offlineX2Btn;
    RectTransform bottomPad;
    readonly List<Text> rowTxt = new List<Text>();
    GameObject offlinePopup;
    OfflineEarnings offline;
    double offAmount, offSecs;

    void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        offline = FindFirstObjectByType<OfflineEarnings>();
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Build();
    }

    void OnEnable()
    {
        GameEvents.OfflineEarned += OnOffline;
        GameEvents.RewardGranted += OnRewardGranted;
    }

    void OnDisable()
    {
        GameEvents.OfflineEarned -= OnOffline;
        GameEvents.RewardGranted -= OnRewardGranted;
    }

    void OnOffline(double amt, double secs)
    {
        offAmount = amt; offSecs = secs;
        if (offlinePopup != null) ShowOffline();
    }

    void OnRewardGranted(RewardType t)
    {
        if (t == RewardType.OfflineDouble && offlinePopup) offlinePopup.SetActive(false);
        if (statusTxt) statusTxt.text = "Reward granted: " + t;
    }

    void ShowOffline()
    {
        offlineTxt.text = "Welcome back! Away " + BigNum.Time(offSecs) + "\nEarned " + BigNum.Format(offAmount) + " coins";
        offlinePopup.SetActive(true);
    }

    void Update()
    {
        if (canvas == null) return;
        var e = IdleEngine.I; var ad = AdManager.I;
        if (e == null) return;

        coinsTxt.text = BigNum.Format(e.Coins) + " coins";
        infoTxt.text = "+" + BigNum.Format(e.IncomePerSec) + "/s   Tap " + BigNum.Format(e.TapValue) +
                       "   Gems " + e.Gems + " (+" + (e.PendingGems() - e.Gems) + ")\n" +
                       (e.BoostActive ? "BOOST 2x: " + BigNum.Time(e.BoostRemaining) : "Boost off");
        for (int i = 0; i < rowTxt.Count; i++)
        {
            var d = e.Upgrades.Defs[i];
            rowTxt[i].text = d.name + " x" + e.Upgrades.Owned[i] + "\n" + BigNum.Format(e.Upgrades.CostOf(i));
        }
        bool ready = ad != null && ad.RewardedReady;
        boostBtn.interactable = ready;
        warpBtn.interactable = ad != null && ad.TimeWarpReady;
        offlineX2Btn.interactable = ready;

        // Banner inset: keep the panel clear of the banner (+16dp approx).
        float px = ad != null ? ad.BannerHeightPx : 0;
        bottomPad.GetComponent<LayoutElement>().minHeight = px / canvas.scaleFactor + 16;
    }

    // ---------------- builders ----------------
    void Build()
    {
        var cgo = new GameObject("DebugCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1080, 1920);
        sc.matchWidthOrHeight = 0.5f;
        cgo.transform.SetParent(transform, false);

        var root = Panel(cgo.transform, "Root", new Color(0.08f, 0.08f, 0.1f, 1));
        Stretch(root);
        var v = root.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(24, 24, 60, 0); v.spacing = 12;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;

        coinsTxt = Label(root, "", 56, 80);
        infoTxt = Label(root, "", 30, 90);
        statusTxt = Label(root, "", 26, 40);
        MakeButton(root, "TAP", 100, () => IdleEngine.I.Tap(), new Color(0.9f, 0.6f, 0.1f));

        var row = Row(root, 90);
        boostBtn = MakeButton(row, "Boost 2x (ad)", 90, () => Ad(RewardType.Boost2x));
        warpBtn = MakeButton(row, "Time Warp (ad)", 90, () => Ad(RewardType.TimeWarp));
        row = Row(root, 90);
        MakeButton(row, "Prestige", 90, () => IdleEngine.I.Prestige());
        MakeButton(row, "Milestone", 90, () => GameEvents.RaiseMilestoneMenu());

        // scrollable buy list
        var scrollGo = Panel(root, "Scroll", new Color(0.12f, 0.12f, 0.15f));
        var le = scrollGo.gameObject.AddComponent<LayoutElement>(); le.flexibleHeight = 1; le.minHeight = 300;
        var sr = scrollGo.gameObject.AddComponent<ScrollRect>(); sr.horizontal = false;
        scrollGo.gameObject.AddComponent<RectMask2D>();
        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
        content.SetParent(scrollGo, false);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
        content.sizeDelta = Vector2.zero;
        var cv = content.GetComponent<VerticalLayoutGroup>();
        cv.spacing = 8; cv.childControlWidth = cv.childControlHeight = true;
        cv.childForceExpandWidth = true; cv.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = content;

        int n = UpgradeSystem.DefaultDefs().Length;
        for (int i = 0; i < n; i++)
        {
            int idx = i;
            var r = Row(content, 100);
            var t = Label(r, "", 28, 100); t.alignment = TextAnchor.MiddleLeft;
            t.GetComponent<LayoutElement>().flexibleWidth = 2;
            rowTxt.Add(t);
            MakeButton(r, "x1", 100, () => IdleEngine.I.Buy(idx, 1));
            MakeButton(r, "Max", 100, () => IdleEngine.I.BuyMax(idx));
        }

        // spacer reserved for the adaptive banner (Update() resizes it)
        var padGo = new GameObject("BannerPad", typeof(RectTransform), typeof(LayoutElement));
        padGo.transform.SetParent(root, false);
        bottomPad = padGo.GetComponent<RectTransform>();
        padGo.GetComponent<LayoutElement>().minHeight = 16;

        BuildOfflinePopup(cgo.transform);
    }

    void BuildOfflinePopup(Transform parent)
    {
        var dim = Panel(parent, "OfflinePopup", new Color(0, 0, 0, 0.85f));
        Stretch(dim);
        offlinePopup = dim.gameObject;
        var box = Panel(dim, "Box", new Color(0.2f, 0.2f, 0.25f));
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f); box.sizeDelta = new Vector2(900, 560);
        var v = box.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(30, 30, 30, 30); v.spacing = 20;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        offlineTxt = Label(box, "", 38, 220);
        MakeButton(box, "Collect", 110, () => { offline.Collect(); offlinePopup.SetActive(false); });
        offlineX2Btn = MakeButton(box, "Collect x2 (watch ad)", 110, () => Ad(RewardType.OfflineDouble));
        offlinePopup.SetActive(false);

        // OfflineEarnings.Start may already have fired before this UI was built
        if (offline != null && offline.PendingAmount > 0)
        {
            offAmount = offline.PendingAmount; offSecs = offline.AwaySeconds; ShowOffline();
        }
    }

    void Ad(RewardType t)
    {
        bool ok = AdManager.I != null && AdManager.I.ShowRewarded(t);
        statusTxt.text = ok ? "Showing ad: " + t : "Ad not ready: " + t;
    }

    // ---------------- ui helpers ----------------
    RectTransform Panel(Transform p, string name, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(p, false);
        go.GetComponent<Image>().color = c;
        return go.GetComponent<RectTransform>();
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    RectTransform Row(Transform p, float h)
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(p, false);
        var hg = go.GetComponent<HorizontalLayoutGroup>(); hg.spacing = 10;
        hg.childControlWidth = hg.childControlHeight = true;
        hg.childForceExpandWidth = true; hg.childForceExpandHeight = true;
        var le = go.GetComponent<LayoutElement>(); le.minHeight = le.preferredHeight = h;
        return go.GetComponent<RectTransform>();
    }

    Text Label(Transform p, string s, int size, float h)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(p, false);
        var t = go.GetComponent<Text>();
        t.font = font; t.fontSize = size; t.text = s; t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.raycastTarget = false;
        var le = go.GetComponent<LayoutElement>(); le.minHeight = le.preferredHeight = h;
        return t;
    }

    Button MakeButton(Transform p, string label, float h, UnityEngine.Events.UnityAction act, Color? col = null)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(p, false);
        go.GetComponent<Image>().color = col ?? new Color(0.25f, 0.45f, 0.75f);
        var le = go.GetComponent<LayoutElement>(); le.minHeight = le.preferredHeight = h;
        var b = go.GetComponent<Button>(); b.onClick.AddListener(act);
        var t = Label(go.transform, label, 30, h);
        t.GetComponent<LayoutElement>().ignoreLayout = true;
        Stretch(t.rectTransform);
        return b;
    }
}
