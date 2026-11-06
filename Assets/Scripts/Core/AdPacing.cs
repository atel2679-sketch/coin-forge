/// <summary>
/// Ad frequency rules (GDD 2.1/2.2), measured on the monotonic clock:
/// interstitial not in the first 3 min of a session, at least 5 min apart, not within 60 s after a rewarded ad;
/// Time Warp rewarded at most once per 10 min.
/// </summary>
public sealed class AdPacing
{
    public double InterstitialCooldown = 300;
    public double SessionGrace = 180;
    public double AfterRewardedGrace = 60;
    public double TimeWarpCooldown = 600;

    readonly IClock clock;
    readonly double sessionStart;
    double lastInterstitial = double.NegativeInfinity;
    double lastRewarded = double.NegativeInfinity;
    double lastTimeWarp = double.NegativeInfinity;

    public AdPacing(IClock clock)
    {
        this.clock = clock;
        sessionStart = clock.Realtime;
    }

    public bool InterstitialAllowed
    {
        get
        {
            double t = clock.Realtime;
            return t - sessionStart >= SessionGrace
                && t - lastInterstitial >= InterstitialCooldown
                && t - lastRewarded >= AfterRewardedGrace;
        }
    }

    public bool TimeWarpOffCooldown => clock.Realtime - lastTimeWarp >= TimeWarpCooldown;
    public double TimeWarpCooldownLeft => System.Math.Max(0, TimeWarpCooldown - (clock.Realtime - lastTimeWarp));

    public void MarkInterstitialShown() => lastInterstitial = clock.Realtime;
    public void MarkRewardedClosed() => lastRewarded = clock.Realtime;
    public void MarkTimeWarpGranted() => lastTimeWarp = clock.Realtime;
}
