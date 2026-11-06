using System;
using UnityEngine;

/// <summary>
/// Computes background earnings on launch/resume.
/// Safeguards: UTC timestamps, clock-rollback detection, hard cap, minimum away time,
/// and a high-water-mark timestamp so moving the clock back then forward can't farm time.
/// For stronger protection, replace Now() with server/NTP time.
/// </summary>
public class OfflineEarnings : MonoBehaviour
{
    public double maxOfflineHours = 8;
    public double offlineEfficiency = 0.5;   // 50% of live income while away (upsell for doubler)
    public double minAwaySeconds = 60;

    const string LastKey = "ic_last_utc";
    const string MaxKey  = "ic_max_utc";   // highest timestamp ever seen

    public double PendingAmount { get; private set; }
    public double AwaySeconds { get; private set; }
    bool doubled;

    void OnEnable()  => GameEvents.RewardGranted += OnReward;
    void OnDisable() => GameEvents.RewardGranted -= OnReward;

    void Start() => Compute();                                 // cold start
    void OnApplicationPause(bool paused) { if (!paused) Compute(); } // resume

    public static void StampNow()
    {
        double now = IdleEngine.Now();
        double max = Math.Max(now, GetD(MaxKey));
        PlayerPrefs.SetString(LastKey, now.ToString("R"));
        PlayerPrefs.SetString(MaxKey, max.ToString("R"));
    }

    void Compute()
    {
        if (PendingAmount > 0) return;             // unclaimed popup still open: don't overwrite it
        double now = IdleEngine.Now();
        double last = GetD(LastKey);
        double max = GetD(MaxKey);
        StampNow();
        if (last <= 0) return;                     // first launch

        // Clock rolled back below the highest-ever seen time -> award nothing.
        if (now < last || now < max) { Debug.LogWarning("[Offline] Clock rollback detected"); return; }

        // Measure from the high-water mark, never from a lower stamp.
        double away = now - Math.Max(last, max);
        if (away < minAwaySeconds) return;

        AwaySeconds = Math.Min(away, maxOfflineHours * 3600);
        PendingAmount = IdleEngine.I.BaseIncomePerSec * AwaySeconds * offlineEfficiency;
        doubled = false;
        if (PendingAmount <= 0) return;

        // Show popup: "You earned X while away" [Collect] [Watch ad: x2]
        GameEvents.RaiseOffline(PendingAmount, AwaySeconds);
    }

    /// <summary>UI "Collect" button (no ad).</summary>
    public void Collect()
    {
        if (PendingAmount <= 0) return;
        IdleEngine.I.AddOffline(PendingAmount);
        PendingAmount = 0;
        IdleEngine.I.Save();
    }

    void OnReward(RewardType t)
    {
        if (t != RewardType.OfflineDouble || PendingAmount <= 0 || doubled) return;
        doubled = true;
        PendingAmount *= 2;
        Collect();
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    /// <summary>IC-04 debug: pretend the player was away. Moves both stamps back, then runs the normal resume path.</summary>
    public void DebugSkip(double seconds)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        PlayerPrefs.SetString(LastKey, (GetD(LastKey) - seconds).ToString("R", inv));
        PlayerPrefs.SetString(MaxKey, (GetD(MaxKey) - seconds).ToString("R", inv));
        Compute();
    }
#endif

    static double GetD(string key) =>
        double.TryParse(PlayerPrefs.GetString(key, "0"), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
}
