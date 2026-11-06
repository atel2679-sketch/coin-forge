/// <summary>
/// Rewarded-ad state machine. The SDK's "earned" and "closed" callbacks can arrive in either order on Android.
/// The reward is granted when the second of the two arrives, exactly once per <see cref="Begin"/>.
/// Closed without earned (ad skipped) grants nothing.
/// </summary>
public sealed class RewardFlow
{
    bool active, earned, closed, granted;

    public RewardType Pending { get; private set; }
    public bool InFlight => active && !closed;

    public void Begin(RewardType type)
    {
        Pending = type;
        active = true;
        earned = closed = granted = false;
    }

    /// <returns>True if the reward must be granted now.</returns>
    public bool OnEarned()
    {
        if (!active) return false;
        earned = true;
        return TryGrant();
    }

    /// <returns>True if the reward must be granted now.</returns>
    public bool OnClosed()
    {
        if (!active) return false;
        closed = true;
        return TryGrant();
    }

    bool TryGrant()
    {
        if (!earned || !closed || granted) return false;
        granted = true;
        return true;
    }
}
