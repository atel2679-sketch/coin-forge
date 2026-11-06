using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using UnityEngine;

/// <summary>
/// AdMob manager (Google Mobile Ads Unity plugin v9+).
/// - Rewarded: reward granted ONLY inside OnUserEarnedReward, then forwarded via GameEvents.
/// - Interstitial: 5-min global cooldown, 3-min session grace, 60s after any rewarded.
/// - Banner: anchored adaptive at bottom; UI must add BannerHeightPx inset to upgrade panel.
/// NOTE: Run UMP consent (GDPR) before calling Init() in production.
/// </summary>
public class AdManager : MonoBehaviour
{
    public static AdManager I { get; private set; }

    [Header("Ad Unit IDs (Google TEST IDs by default — replace before release)")]
    public string rewardedId     = "ca-app-pub-3940256099942544/5224354917";
    public string interstitialId = "ca-app-pub-3940256099942544/1033173712";
    public string bannerId       = "ca-app-pub-3940256099942544/9214589741";

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

    public bool RewardedReady => rewarded != null && rewarded.CanShowAd();
    public bool TimeWarpReady => RewardedReady && Pacing.TimeWarpOffCooldown;
    AdPacing Pacing => pacing ??= new AdPacing(GameClock.Current)
    {
        InterstitialCooldown = interstitialCooldown,
        SessionGrace = sessionGrace,
        AfterRewardedGrace = afterRewardedGrace,
        TimeWarpCooldown = timeWarpCooldown
    };
    public float BannerHeightPx => banner != null ? banner.GetHeightInPixels() : 0;

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
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

    /// <summary>
    /// CONSENT HOOK: production must run the UMP consent flow here and call Init() only
    /// once ConsentInformation.CanRequestAds() is true. WO IC-01 calls Init() directly (test IDs only).
    /// </summary>
    void StartAfterConsent() => Init();

    bool initStarted;
    public void Init()
    {
        if (initStarted) return;
        initStarted = true;
        // Plugin v11: every SDK callback may arrive on a background thread -> marshal with OnMain().
        MobileAds.Initialize(_ => OnMain(() =>
        {
            LoadRewarded();
            LoadInterstitial();
            ShowBanner();
        }));
    }

    static void OnMain(Action a) => MobileAdsEventExecutor.ExecuteInUpdate(a);

    // ================= REWARDED =================
    void LoadRewarded()
    {
        rewarded?.Destroy();
        rewarded = null;
        GameEvents.RaiseRewardedReady(false);

        RewardedAd.Load(rewardedId, new AdRequest(), (ad, err) => OnMain(() =>
        {
            if (err != null || ad == null)
            {
                Debug.LogWarning($"[Ads] Rewarded load failed: {err}");
                Invoke(nameof(LoadRewarded), 30f); // retry
                return;
            }
            rewarded = ad;
            ad.OnAdFullScreenContentClosed += () => OnMain(OnRewardedClosed);
            ad.OnAdFullScreenContentFailed += e => OnMain(() => { Debug.LogWarning($"[Ads] Rewarded show failed: {e}"); LoadRewarded(); });
            GameEvents.RaiseRewardedReady(true);
        }));
    }

    /// <summary>Called by UI buttons: Boost2x, TimeWarp, OfflineDouble.</summary>
    public bool ShowRewarded(RewardType type)
    {
        if (!RewardedReady) return false;
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
        InterstitialAd.Load(interstitialId, new AdRequest(), (ad, err) => OnMain(() =>
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
        banner = new BannerView(bannerId, size, AdPosition.Bottom);
        banner.OnBannerAdLoaded += () => OnMain(() => Debug.Log($"[Ads] Banner height px: {BannerHeightPx}"));
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
