using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// IC-06: the single place for AdMob IDs (Assets/Resources/AdConfig.asset).
/// Editor and Development Builds always use Google's test IDs; only a release build uses the production IDs.
/// The release build script refuses to build when the production IDs are missing or invalid (AdIds.ReleaseProblems).
/// </summary>
[CreateAssetMenu(menuName = "CoinForge/Ad Config", fileName = "AdConfig")]
public sealed class AdConfig : ScriptableObject
{
    public const string ResourcePath = "AdConfig";

    [Header("Production IDs (release builds only)")]
    public string appId = "";
    public string rewardedId = "";
    public string interstitialId = "";
    public string bannerId = "";

    [Header("Test devices: real ad units serve test ads here (all builds)")]
    [Tooltip("MD5 of the device's ANDROID_ID, as printed in logcat by the Ads/UMP SDK.")]
    public List<string> testDeviceIds = new List<string> { "3EEB92147C2A16BE07C8D9854A3AB576" }; // Redmi Note 7 c890abc

    public AdIds Production => new AdIds(appId, rewardedId, interstitialId, bannerId);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    public const bool UsesTestIds = true;
    public AdIds Active => AdIds.Test;
#else
    public const bool UsesTestIds = false;
    public AdIds Active => Production;
#endif

    static AdConfig cached;
    public static AdConfig Load()
    {
        if (cached == null) cached = Resources.Load<AdConfig>(ResourcePath);
        if (cached == null)
        {
            Debug.LogError("[Ads] Resources/AdConfig.asset missing; using test IDs");
            cached = CreateInstance<AdConfig>();
            cached.appId = AdIds.Test.AppId;
            cached.rewardedId = AdIds.Test.Rewarded;
            cached.interstitialId = AdIds.Test.Interstitial;
            cached.bannerId = AdIds.Test.Banner;
        }
        return cached;
    }
}
