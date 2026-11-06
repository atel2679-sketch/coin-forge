using System;
using UnityEngine;

/// <summary>Persisted save payload. <see cref="saveVersion"/> is 0 in saves written before IC-04.</summary>
[Serializable]
public sealed class SaveData
{
    public int saveVersion;
    public double coins, lifetime, boostEnd;
    public int gems;
    public int[] owned;
}

/// <summary>
/// Pure game model: coins, buildings, boost, prestige and save (de)serialization.
/// No Unity lifecycle and no clock of its own, so it can be unit-tested directly.
/// <see cref="IdleEngine"/> wraps it for the scene.
/// </summary>
public sealed class GameState
{
    public const int SaveVersion = 2;  // 1 = IC-01 layout without the field, 2 = IC-04
    public const double GemDivisor = 1e9;
    public const double GemBonus = 0.02;

    public UpgradeSystem Upgrades { get; private set; }
    public double Coins { get; private set; }
    public double Lifetime { get; private set; }
    public int Gems { get; private set; }
    public double BoostEndUtc { get; private set; }

    public GameState() : this(UpgradeSystem.DefaultDefs()) { }

    public GameState(BuildingDef[] defs, int[] owned = null)
    {
        Upgrades = new UpgradeSystem(defs, owned);
    }

    // ---------- Income ----------
    public double PrestigeMult => 1 + GemBonus * Gems;
    public double BaseIncome => Upgrades.TotalProd() * PrestigeMult;
    public bool BoostActive(double now) => now < BoostEndUtc;
    public double BoostRemaining(double now) => Math.Max(0, BoostEndUtc - now);
    public double Income(double now) => BaseIncome * (BoostActive(now) ? 2 : 1);

    /// <summary>Adds earned coins (also counted toward lifetime). Ignores non-positive and non-finite values.</summary>
    public void Add(double amount)
    {
        if (!(amount > 0) || double.IsInfinity(amount)) return;
        Coins += amount;
        Lifetime += amount;
    }

    // ---------- Buildings ----------
    /// <returns>Coins spent, or 0 if not affordable. Coins never go negative.</returns>
    public double Buy(int index, int amount)
    {
        double spent = Upgrades.TryBuy(index, amount, Coins);
        if (spent <= 0) return 0;
        Coins = Math.Max(0, Coins - spent);
        return spent;
    }

    public int MaxAffordable(int index) => Upgrades.MaxAffordable(index, Coins);

    // ---------- Rewards ----------
    /// <summary>2x boost: adds hoursPerAd, stacking, but never more than maxHours remaining from now.</summary>
    public void ExtendBoost(double now, double hoursPerAd, double maxHours)
    {
        double start = Math.Max(now, BoostEndUtc);
        BoostEndUtc = Math.Min(start + hoursPerAd * 3600, now + maxHours * 3600);
    }

    /// <summary>Time Warp pays base income (no ad boost, so ads don't double-dip).</summary>
    public double TimeWarpReward(double hours) => BaseIncome * hours * 3600;

    // ---------- Prestige ----------
    public static int GemsFor(double lifetime) =>
        lifetime > 0 && !double.IsInfinity(lifetime) ? (int)Math.Floor(Math.Sqrt(lifetime / GemDivisor)) : 0;

    public int PendingGems => GemsFor(Lifetime);

    /// <summary>Resets coins and buildings; keeps lifetime, gems and boost. False if no new gems.</summary>
    public bool Prestige()
    {
        int gems = PendingGems;
        if (gems <= Gems) return false;
        Gems = gems;
        Coins = 0;
        Upgrades = new UpgradeSystem(Upgrades.Defs);
        return true;
    }

    // ---------- Save ----------
    public string ToJson() => JsonUtility.ToJson(new SaveData
    {
        saveVersion = SaveVersion,
        coins = Coins,
        lifetime = Lifetime,
        boostEnd = BoostEndUtc,
        gems = Gems,
        owned = (int[])Upgrades.Owned.Clone()
    });

    /// <summary>
    /// Never throws. Empty, corrupt or out-of-range data yields a fresh game (or sanitized values) and an error note.
    /// </summary>
    public static GameState FromJson(string json, out string error)
    {
        error = null;
        var defs = UpgradeSystem.DefaultDefs();
        if (string.IsNullOrWhiteSpace(json)) return new GameState(defs);

        SaveData data;
        try { data = JsonUtility.FromJson<SaveData>(json); }
        catch (Exception e)
        {
            error = "corrupt save: " + e.GetType().Name;
            return new GameState(defs);
        }
        if (data == null) { error = "corrupt save: null"; return new GameState(defs); }
        if (data.saveVersion > SaveVersion) error = "save from newer version " + data.saveVersion;

        int[] owned = data.owned;
        if (owned != null && owned.Length == defs.Length)
            for (int i = 0; i < owned.Length; i++) owned[i] = Math.Max(0, owned[i]);
        else owned = null;

        var state = new GameState(defs, owned)
        {
            Coins = Finite(data.coins),
            Lifetime = Finite(data.lifetime),
            Gems = Math.Max(0, data.gems),
            BoostEndUtc = Finite(data.boostEnd)
        };
        if (state.Lifetime < state.Coins) state.Lifetime = state.Coins;
        return state;
    }

    static double Finite(double v) => double.IsNaN(v) || double.IsInfinity(v) || v < 0 ? 0 : v;
}
