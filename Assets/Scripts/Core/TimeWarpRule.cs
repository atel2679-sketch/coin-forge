/// <summary>
/// IC-04b: when the Time Warp rewarded button is offered.
/// Shown only with a ready rewarded ad, the 10 min cooldown over, some base income (so the reward is never 0),
/// and the next goal (<see cref="GameState.SecondsToNextGoal"/>) more than 3 minutes away.
/// </summary>
public static class TimeWarpRule
{
    public const double MinWaitSeconds = 180;

    /// <param name="timeWarpReady">AdManager.TimeWarpReady: rewarded ad loaded and cooldown over.</param>
    public static bool ShouldShow(bool rewardedReady, bool timeWarpReady, GameState state, double hours)
    {
        if (!rewardedReady || !timeWarpReady || state == null) return false;
        if (!(state.BaseIncome > 0) || !(state.TimeWarpReward(hours) > 0)) return false;
        return state.SecondsToNextGoal() > MinWaitSeconds;
    }
}
