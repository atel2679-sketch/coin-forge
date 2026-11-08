using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
public sealed class CoinForgeIntro : MonoBehaviour, IPointerDownHandler
{
    public const string SeenKey = "ic_intro_seen";
    const float FadeSeconds = 0.3f;
    const float MaximumSeconds = 4;
    [SerializeField] Sprite background, coin, hammer, logo, bar;
    [SerializeField] Sprite[] coinBurst, sparkle;

    Canvas canvas;
    CanvasGroup overlay;
    RectTransform safe, composition, coinRect, hammerRect, logoRect, loading, fillClip;
    Image backgroundImage, flash, burstImage, sparkleImage;
    float startedAt, contentEnd;
    bool shortVersion, skipRequested, finished;
    public bool IsAnimating { get; private set; }
    int burstFrame = -2, sparkleFrame = -2, phase = -1;
    float lastCanvasScale, nextSafeAreaCheck;
    Vector2 lastSize;
    Rect lastSafeArea;

    void Awake()
    {
        CoinForgePresentation.StartupPending = true;
        CoinForgePresentation.IntroActive = true;
        shortVersion = PlayerPrefs.GetInt(SeenKey, 0) == 1;
        contentEnd = shortVersion ? 0.9f : 2;
        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        overlay = gameObject.AddComponent<CanvasGroup>();
        Build();
        WarmArtwork();
        Layout();
    }

    IEnumerator Start()
    {
        yield return null; // Let Main finish Start and render the opaque intro backdrop first.
        startedAt = Time.realtimeSinceStartup;
        IsAnimating = true;
        Debug.Log("[Intro] begin " + (shortVersion ? "short" : "full"));
        while (!skipRequested && Time.realtimeSinceStartup - startedAt < contentEnd)
        {
            float elapsed = Time.realtimeSinceStartup - startedAt;
            Layout();
            Animate(elapsed);
            yield return null;
        }
        CoinForgePresentation.IntroFading = true;
        fillClip.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 628);
        float fadeStarted = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - fadeStarted < FadeSeconds &&
               Time.realtimeSinceStartup - startedAt < MaximumSeconds)
        {
            overlay.alpha = 1 - Mathf.Clamp01((Time.realtimeSinceStartup - fadeStarted) / FadeSeconds);
            yield return null;
        }
        Finish();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsAnimating && Time.realtimeSinceStartup - startedAt >= 0.5f) skipRequested = true;
    }

    void Finish()
    {
        if (finished) return;
        finished = true;
        IsAnimating = false;
        overlay.alpha = 0;
        overlay.blocksRaycasts = false;
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
        CoinForgePresentation.IntroActive = false;
        Debug.Log("[Intro] complete elapsed=" + (Time.realtimeSinceStartup - startedAt).ToString("F3",
            System.Globalization.CultureInfo.InvariantCulture) + " skipped=" + skipRequested);
        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        if (!finished) Finish();
    }

    void Animate(float time)
    {
        float impact = shortVersion ? 0.25f : 0.9f;
        float swing = shortVersion ? 0.05f : 0.7f;
        float logoStart = impact;
        float logoEnd = shortVersion ? 0.65f : 1.4f;
        float loadingStart = logoEnd;
        backgroundImage.color = new Color(0.75f, 0.69f, 0.63f, Mathf.Clamp01(time / 0.3f));

        float drop = shortVersion ? 1 : Mathf.Clamp01((time - 0.3f) / 0.4f);
        coinRect.gameObject.SetActive(shortVersion || time >= 0.3f);
        float dropY = drop < 0.72f ? Mathf.Lerp(500, 0, (drop / 0.72f) * (drop / 0.72f))
            : Mathf.Sin((drop - 0.72f) / 0.28f * Mathf.PI) * 48;
        coinRect.anchoredPosition = new Vector2(-60, 60 + dropY);
        float hitAge = time - impact;
        float punch = hitAge >= 0 && hitAge < 0.18f ? Mathf.Sin(hitAge / 0.18f * Mathf.PI) * 0.12f : 0;
        coinRect.localScale = new Vector3(1 + punch, 1 - punch, 1);

        hammerRect.gameObject.SetActive(time >= swing && hitAge < 0.22f);
        float swingProgress = Mathf.Clamp01((time - swing) / (impact - swing));
        hammerRect.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(-38, 40, swingProgress * swingProgress));
        flash.color = new Color(1, 1, 1, hitAge >= 0 && hitAge < 0.08f ? (1 - hitAge / 0.08f) * 0.82f : 0);

        // Composition is in Canvas units. Convert the shake so its amplitude stays at or below 8 screen pixels.
        float pixelScale = composition.lossyScale.x;
        float shake = hitAge >= 0 && hitAge < 0.16f ? 8 * (1 - hitAge / 0.16f) / Mathf.Max(0.01f, pixelScale) : 0;
        composition.anchoredPosition = new Vector2(Mathf.Sin(hitAge * 130), Mathf.Cos(hitAge * 110)).normalized * shake;
        SetFrame(burstImage, coinBurst, ref burstFrame, hitAge, 20);
        SetFrame(sparkleImage, sparkle, ref sparkleFrame, time - logoStart, 15);

        float reveal = Mathf.Clamp01((time - logoStart) / (logoEnd - logoStart));
        logoRect.gameObject.SetActive(time >= logoStart);
        float scale = reveal < 0.78f ? Mathf.Lerp(0.62f, 1.055f, Mathf.SmoothStep(0, 1, reveal / 0.78f))
            : Mathf.Lerp(1.055f, 1, (reveal - 0.78f) / 0.22f);
        logoRect.localScale = Vector3.one * scale;
        loading.gameObject.SetActive(time >= loadingStart);
        float progress = Mathf.Clamp01((time - loadingStart) / (contentEnd - loadingStart));
        fillClip.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 628 * progress);

        int nextPhase = time < impact ? 0 : time < logoEnd ? 1 : 2;
        if (phase != nextPhase)
        {
            phase = nextPhase;
            Debug.Log("[Intro] phase=" + (phase == 0 ? "forge" : phase == 1 ? "impact-logo" : "loading"));
        }
    }

    static void SetFrame(Image image, Sprite[] frames, ref int previous, float age, float fps)
    {
        int index = age < 0 ? -1 : Mathf.FloorToInt(age * fps);
        if (index >= frames.Length) index = -1;
        if (index == previous) return;
        previous = index;
        image.enabled = index >= 0;
        if (index >= 0) image.sprite = frames[index];
    }

    void Build()
    {
        MakeImage(transform, "Intro input shield", null, new Color32(30, 26, 22, 255)).raycastTarget = true;
        backgroundImage = MakeImage(transform, "bg_main.png", background, Color.clear);
        var crop = backgroundImage.gameObject.AddComponent<AspectRatioFitter>();
        crop.aspectRatio = background.rect.width / background.rect.height;
        crop.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

        safe = Box(transform, "Intro safe area");
        composition = Box(safe, "Forge composition");
        composition.anchorMin = composition.anchorMax = new Vector2(0.5f, 0.5f);
        composition.sizeDelta = new Vector2(1080, 1500);
        coinRect = Centered(composition, "coin_main.png", coin, new Vector2(-60, 60), new Vector2(420, 420));
        hammerRect = Centered(composition, "hammer.png", hammer, new Vector2(185, 50), new Vector2(450, 450));
        hammerRect.pivot = new Vector2(0.5f, 0.12f);
        burstImage = MakeImage(composition, "vfx_coin_burst_01.png", null, Color.white);
        Center(burstImage.rectTransform, new Vector2(-60, 100), new Vector2(730, 730));
        logoRect = Centered(composition, "logo_coinforge.png", logo, new Vector2(0, -280), new Vector2(900, 300));
        sparkleImage = MakeImage(composition, "vfx_sparkle_01.png", null, Color.white);
        Center(sparkleImage.rectTransform, new Vector2(230, -205), new Vector2(350, 350));

        loading = Box(composition, "Loading bar");
        Center(loading, new Vector2(0, -515), new Vector2(640, 44));
        MakeImage(loading, "btn_gold.png", bar, new Color(0.24f, 0.18f, 0.1f, 1), true);
        fillClip = Box(loading, "Loading fill clip");
        fillClip.anchorMin = fillClip.anchorMax = new Vector2(0, 0.5f);
        fillClip.pivot = new Vector2(0, 0.5f);
        fillClip.anchoredPosition = new Vector2(6, 0);
        fillClip.sizeDelta = new Vector2(0, 32);
        fillClip.gameObject.AddComponent<RectMask2D>();
        var fill = MakeImage(fillClip, "btn_gold.png", bar, Color.white, true);
        fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0, 0.5f);
        fill.rectTransform.pivot = new Vector2(0, 0.5f);
        fill.rectTransform.sizeDelta = new Vector2(628, 32);
        var caption = Box(loading, "Loading caption").gameObject.AddComponent<Text>();
        Center(caption.rectTransform, new Vector2(0, -70), new Vector2(900, 65));
        caption.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        caption.fontStyle = FontStyle.Bold;
        caption.fontSize = 36;
        caption.alignment = TextAnchor.MiddleCenter;
        caption.color = new Color32(255, 230, 189, 255);
        caption.text = "FORGING YOUR FORTUNE";
        caption.raycastTarget = false;
        flash = MakeImage(transform, "Impact flash", null, Color.clear);
        Animate(0);
    }

    void WarmArtwork()
    {
        // Upload the animation textures before the first visible frame, avoiding first-use stalls on the strike.
        RenderTexture previous = RenderTexture.active;
        var target = RenderTexture.GetTemporary(8, 8, 0);
        try
        {
            Graphics.Blit(background.texture, target);
            Graphics.Blit(coin.texture, target);
            Graphics.Blit(hammer.texture, target);
            Graphics.Blit(logo.texture, target);
            Graphics.Blit(bar.texture, target);
            foreach (Sprite frame in coinBurst) Graphics.Blit(frame.texture, target);
            foreach (Sprite frame in sparkle) Graphics.Blit(frame.texture, target);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    void Layout()
    {
        var size = new Vector2(Screen.width, Screen.height);
        if (size == lastSize && canvas.scaleFactor == lastCanvasScale &&
            Time.realtimeSinceStartup < nextSafeAreaCheck) return;
        nextSafeAreaCheck = Time.realtimeSinceStartup + 0.1f;
        Rect bounds = Screen.safeArea;
        if (size == lastSize && bounds == lastSafeArea && canvas.scaleFactor == lastCanvasScale) return;
        if (size.x <= 0 || size.y <= 0) return;
        lastSize = size;
        lastCanvasScale = canvas.scaleFactor;
        lastSafeArea = bounds;
        safe.anchorMin = new Vector2(bounds.xMin / size.x, bounds.yMin / size.y);
        safe.anchorMax = new Vector2(bounds.xMax / size.x, bounds.yMax / size.y);
        safe.offsetMin = safe.offsetMax = Vector2.zero;
        float scale = Mathf.Min(bounds.width / 1080, bounds.height / 1500);
        composition.localScale = Vector3.one * (scale / canvas.scaleFactor);
    }

    static RectTransform Box(Transform parent, string name)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    static Image MakeImage(Transform parent, string name, Sprite sprite, Color color, bool sliced = false)
    {
        var image = Box(parent, name).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = !sliced;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        return image;
    }

    static RectTransform Centered(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        var image = MakeImage(parent, name, sprite, Color.white);
        Center(image.rectTransform, position, size);
        return image.rectTransform;
    }

    static void Center(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
