using System;

public enum RewardType { Boost2x, TimeWarp, OfflineDouble }

/// <summary>Static event bus: systems publish/subscribe without direct references.</summary>
public static class GameEvents
{
    public static event Action<double> CoinsChanged;          // new balance
    public static event Action<double> IncomeChanged;         // coins per second
    public static event Action<int, int> BuildingBought;      // index, new owned
    public static event Action<double, double> OfflineEarned; // amount, seconds away
    public static event Action<RewardType> RewardGranted;     // fired ONLY from OnUserEarnedReward
    public static event Action<bool> RewardedReady;
    public static event Action MilestoneMenuOpened;
    public static event Action PrestigeDone;

    public static void RaiseCoins(double v) => CoinsChanged?.Invoke(v);
    public static void RaiseIncome(double v) => IncomeChanged?.Invoke(v);
    public static void RaiseBought(int i, int n) => BuildingBought?.Invoke(i, n);
    public static void RaiseOffline(double a, double s) => OfflineEarned?.Invoke(a, s);
    public static void RaiseReward(RewardType t) => RewardGranted?.Invoke(t);
    public static void RaiseRewardedReady(bool r) => RewardedReady?.Invoke(r);
    public static void RaiseMilestoneMenu() => MilestoneMenuOpened?.Invoke();
    public static void RaisePrestige() => PrestigeDone?.Invoke();
}
