using UnityEngine;

/// <summary>
/// Computes background earnings on launch/resume (math in <see cref="OfflineCalc"/>, claim in <see cref="OfflineClaim"/>).
/// Safeguards: UTC timestamps, clock-rollback detection, hard cap, minimum away time,
/// and a high-water-mark timestamp so moving the clock back then forward can't farm time.
/// For stronger protection, replace the clock with server/NTP time.
/// </summary>
public class OfflineEarnings : MonoBehaviour
{
    public double maxOfflineHours = 8;
    public double offlineEfficiency = 0.5;   // 50% of live income while away (upsell for doubler)
    public double minAwaySeconds = 60;

    const string LastKey = "ic_last_utc";
    const string MaxKey  = "ic_max_utc";   // highest timestamp ever seen

    readonly OfflineClaim claim = new OfflineClaim();
    public double PendingAmount => claim.Pending;
    public double AwaySeconds => claim.AwaySeconds;

    void OnEnable()  => GameEvents.RewardGranted += OnReward;
    void OnDisable() => GameEvents.RewardGranted -= OnReward;

    void Start() => Compute();                                 // cold start
    void OnApplicationPause(bool paused) { if (!paused) Compute(); } // resume

    public static void StampNow()
    {
        double now = IdleEngine.Now();
        double max = System.Math.Max(now, GetD(MaxKey));
        PlayerPrefs.SetString(LastKey, OfflineCalc.FormatStamp(now));
        PlayerPrefs.SetString(MaxKey, OfflineCalc.FormatStamp(max));
    }

    void Compute()
    {
        if (PendingAmount > 0) return;             // unclaimed popup still open: don't overwrite it
        double now = IdleEngine.Now();
        double last = GetD(LastKey);
        double max = GetD(MaxKey);
        StampNow();

        var result = OfflineCalc.Compute(now, last, max, IdleEngine.I.BaseIncomePerSec,
            maxOfflineHours, offlineEfficiency, minAwaySeconds);
        if (result.Rollback) { Debug.LogWarning("[Offline] Clock rollback detected"); return; }
        if (result.Amount <= 0) return;

        claim.Offer(result.Amount, result.Seconds);
        // Show popup: "You earned X while away" [Collect] [Watch ad: x2]
        GameEvents.RaiseOffline(result.Amount, result.Seconds);
    }

    /// <summary>UI "Collect" button (no ad).</summary>
    public void Collect() => Credit(claim.Collect());

    void OnReward(RewardType t)
    {
        if (t == RewardType.OfflineDouble) Credit(claim.CollectDoubled());
    }

    void Credit(double amount)
    {
        if (amount <= 0) return;
        IdleEngine.I.AddOffline(amount);
        IdleEngine.I.Save();
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    /// <summary>IC-04 debug: pretend the player was away. Moves both stamps back, then runs the normal resume path.</summary>
    public void DebugSkip(double seconds)
    {
        PlayerPrefs.SetString(LastKey, OfflineCalc.FormatStamp(GetD(LastKey) - seconds));
        PlayerPrefs.SetString(MaxKey, OfflineCalc.FormatStamp(GetD(MaxKey) - seconds));
        Compute();
    }
#endif

    static double GetD(string key) => OfflineCalc.ParseStamp(PlayerPrefs.GetString(key, "0"));
}
