using System;
using UnityEngine;

/// <summary>
/// Core idle engine: taps, delta-time accumulation, multipliers, ad reward effects, save/load.
/// Place on a single persistent GameObject. UI reads values via GameEvents or public getters.
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

    public UpgradeSystem Upgrades { get; private set; }
    public double Coins { get; private set; }
    public double Lifetime { get; private set; }
    public int Gems { get; private set; }
    public double BoostEndUtc { get; private set; } // unix seconds

    public bool BoostActive => Now() < BoostEndUtc;
    public double BoostRemaining => Math.Max(0, BoostEndUtc - Now());
    public double PrestigeMult => 1 + 0.02 * Gems;
    public double IncomePerSec => Upgrades.TotalProd() * PrestigeMult * (BoostActive ? 2 : 1);
    public double TapValue => (tapBase + tapIncomeShare * IncomePerSec) * (BoostActive ? 2 : 1);
    /// <summary>Base income without the ad boost — used for offline/time-warp so ads don't double-dip.</summary>
    public double BaseIncomePerSec => Upgrades.TotalProd() * PrestigeMult;

    float uiTimer, saveTimer;
    const string SaveKey = "ic_save_v1";

    [Serializable] class SaveData { public double coins, lifetime, boostEnd; public int gems; public int[] owned; }

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
        // Delta-time accumulation (unscaled so pause menus using timeScale don't stop income)
        // Clamp: long frames after resume must not double-dip with offline earnings.
        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f);
        Add(IncomePerSec * dt);

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
    public void Tap() => Add(TapValue);

    public bool Buy(int index, int amount = 1)
    {
        double spent = Upgrades.TryBuy(index, amount, Coins);
        if (spent <= 0) return false;
        Coins -= spent;
        GameEvents.RaiseCoins(Coins);
        GameEvents.RaiseIncome(IncomePerSec);
        return true;
    }

    public bool BuyMax(int index) => Buy(index, Upgrades.MaxAffordable(index, Coins));

    /// <summary>Seconds until the cheapest next building is affordable (for Time Warp prompt).</summary>
    public double SecondsToNextPurchase()
    {
        double cheapest = double.MaxValue;
        for (int i = 0; i < Upgrades.Defs.Length; i++) cheapest = Math.Min(cheapest, Upgrades.CostOf(i));
        if (Coins >= cheapest) return 0;
        double inc = IncomePerSec;
        return inc <= 0 ? double.MaxValue : (cheapest - Coins) / inc;
    }

    public int PendingGems() => (int)Math.Floor(Math.Sqrt(Lifetime / 1e9));

    public void Prestige()
    {
        int g = PendingGems();
        if (g <= Gems) return;
        Gems = g;
        Coins = 0;
        Upgrades = new UpgradeSystem(UpgradeSystem.DefaultDefs());
        Save();
        GameEvents.RaisePrestige(); // AdManager may show interstitial here (cooldown-guarded)
    }

    public void AddOffline(double amount) => Add(amount);

    // ---------- Rewards ----------
    void OnReward(RewardType t)
    {
        switch (t)
        {
            case RewardType.Boost2x:
                double start = Math.Max(Now(), BoostEndUtc);           // stack
                BoostEndUtc = Math.Min(start + boostHoursPerAd * 3600, Now() + boostMaxHours * 3600);
                break;
            case RewardType.TimeWarp:
                Add(BaseIncomePerSec * timeWarpHours * 3600);
                break;
            // OfflineDouble is handled by OfflineEarnings
        }
        Save();
    }

    void Add(double v)
    {
        if (v <= 0 || double.IsNaN(v) || double.IsInfinity(v)) return;
        Coins += v;
        Lifetime += v;
    }

    // ---------- Persistence ----------
    public static double Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

    public void Save()
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (DebugSaveBlocked) return;
#endif
        var d = new SaveData { coins = Coins, lifetime = Lifetime, boostEnd = BoostEndUtc, gems = Gems, owned = Upgrades.Owned };
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(d));
        OfflineEarnings.StampNow();
        PlayerPrefs.Save();
    }

    void Load()
    {
        var defs = UpgradeSystem.DefaultDefs();
        string json = PlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(json)) { Upgrades = new UpgradeSystem(defs); return; }
        var d = JsonUtility.FromJson<SaveData>(json);
        Coins = d.coins; Lifetime = d.lifetime; Gems = d.gems; BoostEndUtc = d.boostEnd;
        Upgrades = new UpgradeSystem(defs, d.owned);
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    // ---------- IC-04 debug hooks (compiled out of release builds) ----------
    public static bool DebugSaveBlocked;

    public void DebugAddCoins(double amount)
    {
        Add(amount);
        Save();
        GameEvents.RaiseCoins(Coins);
    }
#endif

    void OnApplicationPause(bool paused) { if (paused) Save(); }
    void OnApplicationQuit() => Save();
}
