using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

/// <summary>
/// AdMob manager (Google Mobile Ads Unity plugin v9+).
/// - Rewarded: reward granted ONLY inside OnUserEarnedReward, then forwarded via GameEvents.
/// - Interstitial: 5-min global cooldown, 3-min session grace, 60s after any rewarded.
/// - Banner: anchored adaptive at bottom; UI must add BannerHeightPx inset to upgrade panel.
/// IC-04: UMP consent (GDPR) runs first; ads are initialized only once CanRequestAds() is true.
/// </summary>
public class AdManager : MonoBehaviour
{
    public static AdManager I { get; private set; }

    // IC-06: IDs come from Resources/AdConfig.asset (test IDs in Editor/Development Builds, production in release).
    AdConfig config;
    AdIds ids;

    [Header("Interstitial pacing (seconds)")]
    public float interstitialCooldown = 300;  // mandatory 5 min
    public float sessionGrace = 180;
    public float afterRewardedGrace = 60;

    [Header("Time Warp cooldown (seconds)")]
    public float timeWarpCooldown = 600;

    RewardedAd rewarded;
    InterstitialAd interstitial;
    BannerView banner;

    // IC-04: pacing and reward ordering live in tested Core classes.
    readonly RewardFlow rewardFlow = new RewardFlow();
    AdPacing pacing;

    // IC-04 perf: CanShowAd() and GetHeightInPixels() are JNI calls into the plugin; GetHeightInPixels waits on the
    // Android UI thread (busy with the banner WebView), costing ~50 ms. The UI polls both at 10 Hz (and the tutorial
    // every frame), which caused the P95 spikes. Both are now cached; the banner height only changes when it loads.
    const float ReadyRecheckSeconds = 30f;   // a 1 s recheck still showed ~14 ms spikes every second
    bool readyCache;
    float readyCheckedAt = float.NegativeInfinity;
    float bannerHeightPx;

    public bool RewardedReady
    {
        get
        {
            if (rewarded == null) return false;
            float t = Time.unscaledTime;
            if (t - readyCheckedAt >= ReadyRecheckSeconds)
            {
                readyCache = rewarded.CanShowAd();   // ads expire after ~1 h; re-validate every 30 s (taps re-check fresh)
                readyCheckedAt = t;
            }
            return readyCache;
        }
    }
    public bool TimeWarpReady => RewardedReady && Pacing.TimeWarpOffCooldown;
    AdPacing Pacing => pacing ??= new AdPacing(GameClock.Current)
    {
        InterstitialCooldown = interstitialCooldown,
        SessionGrace = sessionGrace,
        AfterRewardedGrace = afterRewardedGrace,
        TimeWarpCooldown = timeWarpCooldown
    };
    public float BannerHeightPx => banner != null ? bannerHeightPx : 0;

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        mainThread = System.Threading.SynchronizationContext.Current;
        DontDestroyOnLoad(gameObject);
        config = AdConfig.Load();
        ids = config.Active;
        _ = Pacing;   // session clock starts at launch, not at first ad
    }

    void OnEnable()
    {
        GameEvents.PrestigeDone += TryShowInterstitial;
        GameEvents.MilestoneMenuOpened += TryShowInterstitial;
    }
    void OnDisable()
    {
        GameEvents.PrestigeDone -= TryShowInterstitial;
        GameEvents.MilestoneMenuOpened -= TryShowInterstitial;
    }

    void Start() => StartAfterConsent();

    // ================= CONSENT (UMP, IC-04) =================
    /// <summary>Raised when the privacy-options requirement becomes known or changes (UI shows/hides its button).</summary>
    public static event Action PrivacyOptionsChanged;

    const float ConsentRetrySeconds = 60;
    bool consentInFlight, consentKnown, consentStarted;

    public bool PrivacyOptionsRequired =>
        consentKnown && ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

    /// <summary>
    /// Start order: ConsentInformation.Update -> LoadAndShowConsentFormIfRequired -> if CanRequestAds -> MobileAds.Initialize.
    /// Everything is asynchronous, so the tutorial and the game keep running. Failures (offline) are retried later.
    /// </summary>
    void StartAfterConsent()
    {
        consentStarted = true;
        // Consent stored by an earlier session already allows ads: start loading now, refresh consent in parallel.
        if (ConsentInformation.CanRequestAds()) Init();
        RequestConsent();
    }

    void RequestConsent()
    {
        if (consentInFlight) return;
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.Log("[Consent] Offline; retrying later");
            ScheduleConsentRetry();
            return;
        }
        consentInFlight = true;
        var request = new ConsentRequestParameters { ConsentDebugSettings = ConsentTestSettings() };
        ConsentInformation.Update(request, updateError => OnMain(() =>
        {
            if (updateError != null)
            {
                consentInFlight = false;
                Debug.LogWarning($"[Consent] Update failed: {updateError.Message}");
                ScheduleConsentRetry();
                return;
            }
            ConsentForm.LoadAndShowConsentFormIfRequired(formError => OnMain(() =>
            {
                consentInFlight = false;
                consentKnown = true;
                if (formError != null) Debug.LogWarning($"[Consent] Form failed: {formError.Message}");
                Debug.Log($"[Consent] status={ConsentInformation.ConsentStatus} canRequestAds={ConsentInformation.CanRequestAds()} " +
                          $"privacyOptions={ConsentInformation.PrivacyOptionsRequirementStatus}");
                PrivacyOptionsChanged?.Invoke();
                if (ConsentInformation.CanRequestAds()) Init();
                else ScheduleConsentRetry();
            }));
        }));
    }

    void ScheduleConsentRetry()
    {
        CancelInvoke(nameof(RequestConsent));
        Invoke(nameof(RequestConsent), ConsentRetrySeconds);
    }

    void OnApplicationPause(bool paused)
    {
        // Back from background (maybe online again): retry right away if ads never started.
        if (!paused && consentStarted && !initStarted && !consentInFlight) RequestConsent();
    }

    /// <summary>"Privacy settings" button: lets the user change or withdraw consent.</summary>
    public void ShowPrivacyOptions()
    {
        ConsentForm.ShowPrivacyOptionsForm(formError => OnMain(() =>
        {
            if (formError != null) Debug.LogWarning($"[Consent] Privacy options failed: {formError.Message}");
            Debug.Log($"[Consent] Privacy options closed; canRequestAds={ConsentInformation.CanRequestAds()}");
            PrivacyOptionsChanged?.Invoke();
            if (ConsentInformation.CanRequestAds()) Init();
        }));
    }

    // IC-06: registered test devices (AdConfig) may force the EEA consent flow in any build, so the release AAB's
    // consent form can be tested outside Europe. UMP ignores these settings on every device not in the list.
    // Release: launch with  am start ... -e unity "-ump-eea".  Dev builds: also the debug panel toggle.
    const string EeaLaunchArg = "-ump-eea";

    ConsentDebugSettings ConsentTestSettings()
    {
        bool eea = Array.IndexOf(Environment.GetCommandLineArgs(), EeaLaunchArg) >= 0;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        eea |= PlayerPrefs.GetInt(DebugEeaKey, 0) == 1;
#endif
        Debug.Log("[Consent] Test geography: " + (eea ? "EEA" : "device default"));
        return new ConsentDebugSettings
        {
            DebugGeography = eea ? DebugGeography.EEA : DebugGeography.Disabled,
            TestDeviceHashedIds = config.testDeviceIds
        };
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    // ---------- IC-04 consent testing (dev builds only) ----------
    public const string DebugEeaKey = "dbg_ump_eea";

    public static void DebugResetConsent()
    {
        ConsentInformation.Reset();
        Debug.Log("[Consent] Reset");
    }
#endif

    bool initStarted;
    public void Init()
    {
        if (initStarted) return;
        initStarted = true;
        CancelInvoke(nameof(RequestConsent));
        Debug.Log("[Ads] MobileAds.Initialize (consent allows ads), test IDs: " + AdConfig.UsesTestIds);
        // IC-06: test devices get test ads even from production units (no invalid clicks on our own ads).
        MobileAds.SetRequestConfiguration(new RequestConfiguration { TestDeviceIds = config.testDeviceIds });
        // Plugin v11: every SDK callback may arrive on a background thread -> marshal with OnMain().
        MobileAds.Initialize(_ => OnMain(() =>
        {
            LoadRewarded();
#if !CF_STORE_SHOTS   // IC-06 store-screenshot build: no banner, no interstitial on screen
            LoadInterstitial();
            ShowBanner();
#endif
        }));
    }

    // SDK callbacks may arrive on background threads. Marshal them to Unity's main thread through the
    // SynchronizationContext captured in Awake. (MobileAdsEventExecutor only exists after MobileAds.Initialize,
    // so it cannot carry the UMP consent callbacks that must run *before* Initialize.)
    static System.Threading.SynchronizationContext mainThread;

    static void OnMain(Action a)
    {
        if (mainThread != null) mainThread.Post(_ => a(), null);
        else a();
    }

    // ================= REWARDED =================
    void LoadRewarded()
    {
        rewarded?.Destroy();
        rewarded = null;
        GameEvents.RaiseRewardedReady(false);

        RewardedAd.Load(ids.Rewarded, new AdRequest(), (ad, err) => OnMain(() =>
        {
            if (err != null || ad == null)
            {
                Debug.LogWarning($"[Ads] Rewarded load failed: {err}");
                Invoke(nameof(LoadRewarded), 30f); // retry
                return;
            }
            rewarded = ad;
            readyCheckedAt = float.NegativeInfinity;   // new ad: validate on next read
            ad.OnAdFullScreenContentClosed += () => OnMain(OnRewardedClosed);
            ad.OnAdFullScreenContentFailed += e => OnMain(() => { Debug.LogWarning($"[Ads] Rewarded show failed: {e}"); LoadRewarded(); });
            GameEvents.RaiseRewardedReady(true);
        }));
    }

    /// <summary>Called by UI buttons: Boost2x, TimeWarp, OfflineDouble.</summary>
    public bool ShowRewarded(RewardType type)
    {
        if (rewarded == null || !rewarded.CanShowAd()) return false;   // fresh check on the user's tap
        if (type == RewardType.TimeWarp && !TimeWarpReady) return false;

        rewardFlow.Begin(type);
        rewarded.Show(_ => OnMain(() => { if (rewardFlow.OnEarned()) Grant(); })); // onUserEarnedReward
        return true;
    }

    void OnRewardedClosed()
    {
        Pacing.MarkRewardedClosed();
        if (rewardFlow.OnClosed()) Grant();
        LoadRewarded();
    }

    // Earned and Closed can arrive in either order on Android (RewardFlow grants on the second, exactly once).
    // Closed without Earned (user skipped) -> nothing is granted.
    void Grant()
    {
        if (rewardFlow.Pending == RewardType.TimeWarp) Pacing.MarkTimeWarpGranted();
        GameEvents.RaiseReward(rewardFlow.Pending);
    }

    // ================= INTERSTITIAL =================
    void LoadInterstitial()
    {
        interstitial?.Destroy();
        interstitial = null;
        InterstitialAd.Load(ids.Interstitial, new AdRequest(), (ad, err) => OnMain(() =>
        {
            if (err != null || ad == null) { Invoke(nameof(LoadInterstitial), 60f); return; }
            interstitial = ad;
            ad.OnAdFullScreenContentClosed += () => OnMain(LoadInterstitial);
            ad.OnAdFullScreenContentFailed += _ => OnMain(LoadInterstitial);
        }));
    }

    public void TryShowInterstitial()
    {
        if (!Pacing.InterstitialAllowed) return;
        if (interstitial == null || !interstitial.CanShowAd()) return;

        Pacing.MarkInterstitialShown();
        interstitial.Show();
    }

    // ================= BANNER =================
    void ShowBanner()
    {
        banner?.Destroy();
        AdSize size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
        banner = new BannerView(ids.Banner, size, AdPosition.Bottom);
        bannerHeightPx = 0;
        banner.OnBannerAdLoaded += () => OnMain(() =>
        {
            bannerHeightPx = banner.GetHeightInPixels();   // read once per load, not per UI refresh
            Debug.Log($"[Ads] Banner height px: {bannerHeightPx}");
        });
        banner.OnBannerAdLoadFailed += e => OnMain(() => Debug.LogWarning($"[Ads] Banner load failed: {e}"));
        banner.LoadAd(new AdRequest());
    }

    public void HideBanner() => banner?.Hide();
    public void RestoreBanner() => banner?.Show();

    void OnDestroy()
    {
        rewarded?.Destroy();
        interstitial?.Destroy();
        banner?.Destroy();
    }
}
