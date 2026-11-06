using System;
using NUnit.Framework;

/// <summary>WO IC-04 B5 (Buy Max), B6 (Prestige) and B7 (Save).</summary>
public class EconomyTests
{
    static GameState WithCoins(double coins, int[] owned = null)
    {
        var state = new GameState(UpgradeSystem.DefaultDefs(), owned);
        state.Add(coins);
        return state;
    }

    static double Geometric(BuildingDef d, int owned, int k) =>
        d.baseCost * Math.Pow(d.rate, owned) * (Math.Pow(d.rate, k) - 1) / (d.rate - 1);

    // ---------- B5: Buy Max ----------
    [Test]
    public void BuyMax_MatchesGeometricFormula_AndNeverGoesNegative()
    {
        var defs = UpgradeSystem.DefaultDefs();
        var rng = new Random(4);
        for (int trial = 0; trial < 2000; trial++)
        {
            int index = rng.Next(defs.Length);
            int owned = rng.Next(0, 300);
            double coins = Math.Pow(10, rng.NextDouble() * 30);
            var ownedArr = new int[defs.Length];
            ownedArr[index] = owned;
            var state = WithCoins(coins, ownedArr);

            int k = state.MaxAffordable(index);
            double expectedCost = k == 0 ? 0 : Geometric(defs[index], owned, k);
            Assert.LessOrEqual(expectedCost, coins * (1 + 1e-12), "k too large");
            Assert.Greater(Geometric(defs[index], owned, k + 1), coins * (1 - 1e-12), "k too small");

            double spent = k == 0 ? 0 : state.Buy(index, k);
            Assert.AreEqual(expectedCost, spent, Math.Max(1e-9, expectedCost * 1e-12));
            Assert.GreaterOrEqual(state.Coins, 0, "coins went negative");
            Assert.AreEqual(owned + k, state.Upgrades.Owned[index]);
        }
    }

    [Test]
    public void BuyMax_ExactlyEnoughCoins_BuysAll()
    {
        var defs = UpgradeSystem.DefaultDefs();
        for (int k = 1; k <= 100; k++)
        {
            var state = WithCoins(Geometric(defs[0], 0, k));
            Assert.AreEqual(k, state.MaxAffordable(0), "k=" + k);
            Assert.Greater(state.Buy(0, state.MaxAffordable(0)), 0);
            Assert.GreaterOrEqual(state.Coins, 0);
        }
    }

    [Test]
    public void Buy_CannotAfford_SpendsNothing()
    {
        var state = WithCoins(14);
        Assert.AreEqual(0, state.MaxAffordable(0));
        Assert.AreEqual(0, state.Buy(0, 1));
        Assert.AreEqual(0, state.Buy(0, 0));
        Assert.AreEqual(14, state.Coins);
    }

    [Test]
    public void Cost_IsBaseTimesRateToTheN()
    {
        var state = new GameState(UpgradeSystem.DefaultDefs(), new[] { 3, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
        Assert.AreEqual(15 * Math.Pow(1.15, 3), state.Upgrades.CostOf(0), 1e-9);
    }

    // ---------- B6: Prestige ----------
    [TestCase(0.99e9, 0)]
    [TestCase(1e9, 1)]
    [TestCase(3.99e9, 1)]
    [TestCase(4e9, 2)]
    [TestCase(9e9, 3)]
    public void GemsFromLifetime(double lifetime, int gems) => Assert.AreEqual(gems, GameState.GemsFor(lifetime));

    [Test]
    public void Prestige_ResetsCoinsAndBuildings_KeepsGemsAndMultiplier()
    {
        var state = WithCoins(1e9, new[] { 50, 40, 30, 20, 10, 5, 0, 0, 0, 0 });
        Assert.IsTrue(state.Prestige());
        Assert.AreEqual(1, state.Gems);
        Assert.AreEqual(0, state.Coins);
        CollectionAssert.AreEqual(new int[10], state.Upgrades.Owned);
        Assert.AreEqual(1.02, state.PrestigeMult, 1e-12);
        Assert.AreEqual(1e9, state.Lifetime, "lifetime is kept");

        Assert.IsFalse(state.Prestige(), "no new gems yet");
        state.Add(3e9);                                     // lifetime 4e9
        Assert.IsTrue(state.Prestige());
        Assert.AreEqual(2, state.Gems);
        Assert.AreEqual(1.04, state.PrestigeMult, 1e-12);
    }

    [Test]
    public void Prestige_KeepsBoost()
    {
        var state = WithCoins(1e9);
        state.ExtendBoost(1000, 2, 8);
        double end = state.BoostEndUtc;
        state.Prestige();
        Assert.AreEqual(end, state.BoostEndUtc);
    }

    // ---------- B7: Save ----------
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("{")]
    [TestCase("not json at all")]
    [TestCase("null")]
    [TestCase("{\"coins\":\"abc\"}")]
    [TestCase("[1,2,3]")]
    public void CorruptOrEmptySave_StartsFreshWithoutThrowing(string json)
    {
        GameState state = null;
        Assert.DoesNotThrow(() => state = GameState.FromJson(json, out _));
        Assert.IsNotNull(state);
        Assert.AreEqual(0, state.Coins);
        Assert.AreEqual(10, state.Upgrades.Owned.Length);
    }

    [Test]
    public void Save_RoundTrips_WithSaveVersion()
    {
        var state = WithCoins(12345.5, new[] { 7, 3, 0, 0, 0, 0, 0, 0, 0, 1 });
        state.ExtendBoost(2000, 2, 8);
        string json = state.ToJson();
        StringAssert.Contains("\"saveVersion\":" + GameState.SaveVersion, json);

        var loaded = GameState.FromJson(json, out string error);
        Assert.IsNull(error);
        Assert.AreEqual(state.Coins, loaded.Coins);
        Assert.AreEqual(state.Lifetime, loaded.Lifetime);
        Assert.AreEqual(state.BoostEndUtc, loaded.BoostEndUtc);
        CollectionAssert.AreEqual(state.Upgrades.Owned, loaded.Upgrades.Owned);
    }

    [Test]
    public void LegacyIC01Save_LoadsWithoutVersion()
    {
        const string ic01 = "{\"coins\":56.0,\"lifetime\":80.0,\"boostEnd\":1793883000.0,\"gems\":0,\"owned\":[1,0,0,0,0,0,0,0,0,0]}";
        var loaded = GameState.FromJson(ic01, out string error);
        Assert.IsNull(error);
        Assert.AreEqual(56, loaded.Coins);
        Assert.AreEqual(1, loaded.Upgrades.Owned[0]);
    }

    [Test]
    public void SaveWithBadValues_IsSanitized()
    {
        const string bad = "{\"saveVersion\":2,\"coins\":-5,\"lifetime\":-1,\"gems\":-3,\"owned\":[-2,1,0,0,0,0,0,0,0,0]}";
        var loaded = GameState.FromJson(bad, out _);
        Assert.AreEqual(0, loaded.Coins);
        Assert.AreEqual(0, loaded.Gems);
        Assert.AreEqual(0, loaded.Upgrades.Owned[0]);
        Assert.AreEqual(1, loaded.Upgrades.Owned[1]);
    }

    [Test]
    public void SaveWithWrongBuildingCount_ResetsBuildingsOnly()
    {
        const string json = "{\"saveVersion\":2,\"coins\":500,\"lifetime\":900,\"gems\":1,\"owned\":[1,2]}";
        var loaded = GameState.FromJson(json, out _);
        Assert.AreEqual(500, loaded.Coins);
        Assert.AreEqual(1, loaded.Gems);
        CollectionAssert.AreEqual(new int[10], loaded.Upgrades.Owned);
    }
}
