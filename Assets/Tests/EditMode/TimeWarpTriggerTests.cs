using NUnit.Framework;

/// <summary>WO IC-04b: Time Warp trigger based on the next goal instead of the cheapest building.</summary>
public class TimeWarpTriggerTests
{
    const double Hours = 4;
    // Balance-independent: values come from the shipped table (IC-05 changed it).
    static readonly BuildingDef[] Defs = UpgradeSystem.DefaultDefs();
    static double PressProd => Defs[0].baseProd;          // income of one Coin Press
    static double ForgeCost => Defs[1].baseCost;          // next goal after the first Coin Press

    static GameState State(double coins, params int[] owned)
    {
        var full = new int[10];
        for (int i = 0; i < owned.Length; i++) full[i] = owned[i];
        var state = new GameState(UpgradeSystem.DefaultDefs(), full);
        state.Add(coins);
        return state;
    }

    static bool Show(GameState s, bool rewardedReady = true, bool timeWarpReady = true) =>
        TimeWarpRule.ShouldShow(rewardedReady, timeWarpReady, s, Hours);

    [Test] // (a)
    public void NoIncome_Hidden()
    {
        var s = State(0);                         // nothing owned: income 0, next goal Coin Press
        Assert.AreEqual(double.MaxValue, s.SecondsToNextGoal());
        Assert.IsFalse(Show(s), "reward would be 0");
    }

    [Test] // (b)
    public void NewTierMoreThanThreeMinutesAway_Shown()
    {
        var s = State(0, 1);                      // 1 Coin Press; next goal is the first Mini Forge
        Assert.AreEqual(ForgeCost, s.NextGoalCost, 1e-9);
        Assert.AreEqual(ForgeCost / PressProd, s.SecondsToNextGoal(), 1e-6);
        Assert.Greater(s.SecondsToNextGoal(), TimeWarpRule.MinWaitSeconds, "table assumption for this test");
        Assert.IsTrue(Show(s));
        Assert.Greater(s.TimeWarpReward(Hours), 0);
    }

    [Test] // (b) boundary: the cheap Coin Press no longer suppresses the button
    public void CheapOwnedBuildingAffordable_StillShownForNewTier()
    {
        var s = State(20, 1);                     // Coin Press #2 (17.25) affordable; Mini Forge still > 3 min away
        Assert.LessOrEqual(s.Upgrades.CostOf(0), s.Coins);
        Assert.AreEqual((ForgeCost - 20) / PressProd, s.SecondsToNextGoal(), 1e-6);
        Assert.Greater(s.SecondsToNextGoal(), TimeWarpRule.MinWaitSeconds);
        Assert.IsTrue(Show(s));
    }

    [Test] // (b) close to the goal -> hidden
    public void NewTierWithinThreeMinutes_Hidden()
    {
        double coins = ForgeCost - 150 * PressProd;       // exactly 150 s from the Mini Forge
        var s = State(coins, 1);
        Assert.AreEqual(150, s.SecondsToNextGoal(), 1e-6);
        Assert.IsFalse(Show(s));
    }

    [Test]
    public void GoalAffordable_ReturnsZero()
    {
        var s = State(ForgeCost, 1);
        Assert.AreEqual(0, s.SecondsToNextGoal());
        Assert.IsFalse(Show(s));
    }

    [Test]
    public void GoalIsLowestUnownedIndex_NotCheapest()
    {
        var s = State(0, 5, 0, 3);                // Mini Forge (index 1) unowned, Smelter owned
        Assert.AreEqual(ForgeCost, s.NextGoalCost, 1e-9);
    }

    [Test] // (c)
    public void AllOwned_UsesMostExpensiveBuilding()
    {
        var s = State(0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2);
        Assert.AreEqual(s.Upgrades.CostOf(9), s.NextGoalCost, 1e-6);
        Assert.AreEqual(Defs[9].baseCost * Defs[9].rate * Defs[9].rate, s.NextGoalCost, 1);
        Assert.Greater(s.SecondsToNextGoal(), 180);
        Assert.IsTrue(Show(s));
    }

    [Test] // (d)
    public void CooldownActive_Hidden()
    {
        var s = State(0, 1);
        Assert.IsFalse(Show(s, timeWarpReady: false));
        Assert.IsFalse(Show(s, rewardedReady: false));
    }

    [Test] // (d) end to end with the real cooldown
    public void CooldownFromAdPacing_HidesForTenMinutes()
    {
        var clock = new FakeClock();
        var pacing = new AdPacing(clock);
        var s = State(0, 1);
        pacing.MarkTimeWarpGranted();
        Assert.IsFalse(Show(s, timeWarpReady: pacing.TimeWarpOffCooldown));
        clock.Advance(600);
        Assert.IsTrue(Show(s, timeWarpReady: pacing.TimeWarpOffCooldown));
    }

    [Test]
    public void BoostDoesNotChangeTheGoalTime()
    {
        var s = State(0, 1);
        double before = s.SecondsToNextGoal();
        s.ExtendBoost(1000, 2, 8);
        Assert.AreEqual(before, s.SecondsToNextGoal(), 1e-9, "uses base income, like the reward");
    }
}
