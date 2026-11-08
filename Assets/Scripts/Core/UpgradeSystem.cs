using System;
using UnityEngine;

[Serializable]
public class BuildingDef
{
    public string name;
    public double baseCost;
    public double baseProd;   // coins/sec per unit
    public double rate = 1.15; // cost growth r
}

/// <summary>Upgrade inventory: cost math, bulk buy, production totals. Pure data + math, no UI.</summary>
public class UpgradeSystem
{
    public readonly BuildingDef[] Defs;
    public int[] Owned;

    public UpgradeSystem(BuildingDef[] defs, int[] owned = null)
    {
        Defs = defs;
        Owned = owned != null && owned.Length == defs.Length ? owned : new int[defs.Length];
    }

    // Cost = Base * r^n
    public double CostOf(int i) => Defs[i].baseCost * Math.Pow(Defs[i].rate, Owned[i]);

    // Geometric sum for buying k units: Base * r^n * (r^k - 1)/(r - 1)
    public double CostOf(int i, int k)
    {
        double r = Defs[i].rate;
        return CostOf(i) * (Math.Pow(r, k) - 1) / (r - 1);
    }

    // Largest k affordable with given coins
    public int MaxAffordable(int i, double coins)
    {
        double r = Defs[i].rate, c = CostOf(i);
        if (coins < c) return 0;
        double estimate = Math.Floor(Math.Log(coins * (r - 1) / c + 1, r));
        int k = double.IsNaN(estimate) ? 0 : (int)Math.Min(int.MaxValue - 1, Math.Max(0, estimate));
        // The log estimate can be off by one either way at exact boundaries (floating point): correct it.
        while (k > 0 && CostOf(i, k) > coins) k--;
        while (CostOf(i, k + 1) <= coins) k++;
        return k;
    }

    // Owned * BaseProd * 2^(floor(Owned/25))  -> milestone doubling every 25
    public double ProdOf(int i) =>
        Owned[i] * Defs[i].baseProd * Math.Pow(2, Owned[i] / 25);

    public double TotalProd()
    {
        double sum = 0;
        for (int i = 0; i < Defs.Length; i++) sum += ProdOf(i);
        return sum;
    }

    /// <returns>coins spent (0 if failed)</returns>
    public double TryBuy(int i, int k, double coins)
    {
        if (k <= 0) return 0;
        double cost = CostOf(i, k);
        if (cost > coins) return 0;
        Owned[i] += k;
        GameEvents.RaiseBought(i, Owned[i]);
        return cost;
    }

    public static BuildingDef[] DefaultDefs() => new[]
    {
        // IC-05 balance pass (PM). Cost growth, milestones, tap, offline and prestige formulas unchanged.
        new BuildingDef{ name="Coin Press",  baseCost=15,     baseProd=0.5,   rate=1.15 },
        new BuildingDef{ name="Mini Forge",  baseCost=120,    baseProd=3,     rate=1.15 },
        new BuildingDef{ name="Smelter",     baseCost=1.2e3,  baseProd=20,    rate=1.14 },
        new BuildingDef{ name="Foundry",     baseCost=1.3e4,  baseProd=110,   rate=1.13 },
        new BuildingDef{ name="Mint",        baseCost=1.5e5,  baseProd=600,   rate=1.13 },
        new BuildingDef{ name="Vault",       baseCost=1.8e6,  baseProd=3.3e3, rate=1.12 },
        new BuildingDef{ name="Bank",        baseCost=2.5e7,  baseProd=1.8e4, rate=1.12 },
        new BuildingDef{ name="Exchange",    baseCost=4e8,    baseProd=1e5,   rate=1.11 },
        new BuildingDef{ name="Treasury",    baseCost=6e9,    baseProd=6e5,   rate=1.10 },
        new BuildingDef{ name="Galaxy Mint", baseCost=9e10,   baseProd=3.6e6, rate=1.10 },
    };
}
