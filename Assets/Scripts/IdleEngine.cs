using System;
using UnityEngine;

/// <summary>
/// Scene wrapper around <see cref="GameState"/>: taps, delta-time accumulation, ad reward effects, save/load.
/// Place on a single persistent GameObject. UI reads values via GameEvents or public getters.
/// IC-04: game rules moved into GameState (unit-tested); this class keeps the IC-01 public API.
/// </summary>
public class IdleEngine : MonoBehaviour
{
    public static IdleEngine I { get; private set; }

    [Header("Balance")]
    public double tapBase = 1;
    public double tapIncomeShare = 0.05;   // tap += 5% of income/sec
    public double boostHoursPerAd = 2;
    public double boostMaxHours = 8;
    public double timeWarpHours = 4;
    public float uiEventInterval = 0.1f;   // throttle UI updates (10 Hz)

    GameState state = new GameState();
    public GameState State => state;

    public UpgradeSystem Upgrades => state.Upgrades;
    public double Coins => state.Coins;
    public double Lifetime => state.Lifetime;
    public int Gems => state.Gems;
    public double BoostEndUtc => state.BoostEndUtc; // unix seconds

    public bool BoostActive => state.BoostActive(Now());
    public double BoostRemaining => state.BoostRemaining(Now());
    public double PrestigeMult => state.PrestigeMult;
    public double IncomePerSec => state.Income(Now());
    public double TapValue => (tapBase + tapIncomeShare * IncomePerSec) * (BoostActive ? 2 : 1);
    /// <summary>Base income without the ad boost — used for offline/time-warp so ads don't double-dip.</summary>
    public double BaseIncomePerSec => state.BaseIncome;

    float uiTimer, saveTimer;
    const string SaveKey = "ic_save_v1";   // key kept from IC-01 so existing saves load; format is versioned inside

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    void OnEnable()  => GameEvents.RewardGranted += OnReward;
    void OnDisable() => GameEvents.RewardGranted -= OnReward;

    void Update()
    {
        // Clamp: long frames after resume must not double-dip with offline earnings.
        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f);
        state.Add(IncomePerSec * dt);

        uiTimer += Time.unscaledDeltaTime;
        if (uiTimer >= uiEventInterval)
        {
            uiTimer = 0;
            GameEvents.RaiseCoins(Coins);
            GameEvents.RaiseIncome(IncomePerSec);
        }

        saveTimer += Time.unscaledDeltaTime;
        if (saveTimer >= 10f) { saveTimer = 0; Save(); }
    }

    // ---------- Public API (called by UI) ----------
    public void Tap() => state.Add(TapValue);

    public bool Buy(int index, int amount = 1)
    {
        if (state.Buy(index, amount) <= 0) return false;
        GameEvents.RaiseCoins(Coins);
        GameEvents.RaiseIncome(IncomePerSec);
        return true;
    }

    public bool BuyMax(int index) => Buy(index, state.MaxAffordable(index));

    /// <summary>Seconds until the cheapest next building is affordable (for Time Warp prompt).</summary>
    public double SecondsToNextPurchase()
    {
        double cheapest = double.MaxValue;
        for (int i = 0; i < Upgrades.Defs.Length; i++) cheapest = Math.Min(cheapest, Upgrades.CostOf(i));
        if (Coins >= cheapest) return 0;
        double inc = IncomePerSec;
        return inc <= 0 ? double.MaxValue : (cheapest - Coins) / inc;
    }

    public int PendingGems() => state.PendingGems;

    public void Prestige()
    {
        if (!state.Prestige()) return;
        Save();
        GameEvents.RaiseCoins(Coins);
        GameEvents.RaiseIncome(IncomePerSec);
        GameEvents.RaisePrestige(); // AdManager may show interstitial here (cooldown-guarded)
    }

    public void AddOffline(double amount) => state.Add(amount);

    // ---------- Rewards ----------
    void OnReward(RewardType t)
    {
        switch (t)
        {
            case RewardType.Boost2x:
                state.ExtendBoost(Now(), boostHoursPerAd, boostMaxHours);
                break;
            case RewardType.TimeWarp:
                state.Add(state.TimeWarpReward(timeWarpHours));
                break;
            // OfflineDouble is handled by OfflineEarnings
        }
        Save();
    }

    // ---------- Persistence ----------
    public static double Now() => GameClock.Current.UtcNow;

    public void Save()
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (DebugSaveBlocked) return;
#endif
        PlayerPrefs.SetString(SaveKey, state.ToJson());
        OfflineEarnings.StampNow();
        PlayerPrefs.Save();
    }

    void Load()
    {
        state = GameState.FromJson(PlayerPrefs.GetString(SaveKey, ""), out string error);
        if (error != null) Debug.LogWarning("[Save] " + error + " — starting from a clean or sanitized state");
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    // ---------- IC-04 debug hooks (compiled out of release builds) ----------
    public static bool DebugSaveBlocked;

    public void DebugAddCoins(double amount)
    {
        state.Add(amount);
        Save();
        GameEvents.RaiseCoins(Coins);
    }
#endif

    void OnApplicationPause(bool paused) { if (paused) Save(); }
    void OnApplicationQuit() => Save();
}
