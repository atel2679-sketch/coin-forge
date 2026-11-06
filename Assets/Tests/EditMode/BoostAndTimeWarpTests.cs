using NUnit.Framework;

/// <summary>WO IC-04 B2 (boost cap) and B3 (time warp reward + cooldown).</summary>
public class BoostAndTimeWarpTests
{
    const double Hour = 3600;
    FakeClock clock;
    GameState state;

    [SetUp]
    public void SetUp()
    {
        clock = new FakeClock();
        state = new GameState(UpgradeSystem.DefaultDefs(), new[] { 10, 5, 0, 0, 0, 0, 0, 0, 0, 0 });
    }

    [Test]
    public void FiveBoosts_CapAtEightHours()
    {
        for (int i = 0; i < 5; i++) state.ExtendBoost(clock.UtcNow, 2, 8);
        Assert.AreEqual(8 * Hour, state.BoostRemaining(clock.UtcNow), 1e-6);
    }

    [Test]
    public void BoostStacks_TwoHoursPerAd()
    {
        state.ExtendBoost(clock.UtcNow, 2, 8);
        Assert.AreEqual(2 * Hour, state.BoostRemaining(clock.UtcNow), 1e-6);
        state.ExtendBoost(clock.UtcNow, 2, 8);
        Assert.AreEqual(4 * Hour, state.BoostRemaining(clock.UtcNow), 1e-6);
    }

    [Test]
    public void BoostCap_IsMeasuredFromNow()
    {
        for (int i = 0; i < 4; i++) state.ExtendBoost(clock.UtcNow, 2, 8);  // 8h
        clock.Advance(3 * Hour);                                          // 5h left
        state.ExtendBoost(clock.UtcNow, 2, 8);                            // 7h
        Assert.AreEqual(7 * Hour, state.BoostRemaining(clock.UtcNow), 1e-6);
        state.ExtendBoost(clock.UtcNow, 2, 8);                            // capped
        Assert.AreEqual(8 * Hour, state.BoostRemaining(clock.UtcNow), 1e-6);
    }

    [Test]
    public void BoostDoublesIncome()
    {
        double baseIncome = state.Income(clock.UtcNow);
        state.ExtendBoost(clock.UtcNow, 2, 8);
        Assert.AreEqual(baseIncome * 2, state.Income(clock.UtcNow), 1e-9);
        clock.Advance(2 * Hour);
        Assert.AreEqual(baseIncome, state.Income(clock.UtcNow), 1e-9);
    }

    [Test]
    public void TimeWarp_PaysBaseIncomeTimesFourHours_EvenWhileBoosted()
    {
        double expected = state.BaseIncome * 4 * Hour;
        state.ExtendBoost(clock.UtcNow, 2, 8);
        Assert.AreEqual(expected, state.TimeWarpReward(4), 1e-6);
        Assert.Greater(expected, 0);
    }

    [Test]
    public void TimeWarp_CooldownRejectsSecondShowForTenMinutes()
    {
        var pacing = new AdPacing(clock);
        Assert.IsTrue(pacing.TimeWarpOffCooldown);
        pacing.MarkTimeWarpGranted();
        Assert.IsFalse(pacing.TimeWarpOffCooldown);
        clock.Advance(599);
        Assert.IsFalse(pacing.TimeWarpOffCooldown);
        clock.Advance(1);
        Assert.IsTrue(pacing.TimeWarpOffCooldown);
    }

    [Test]
    public void Interstitial_GraceCooldownAndAfterRewarded()
    {
        var pacing = new AdPacing(clock);
        Assert.IsFalse(pacing.InterstitialAllowed, "first 3 minutes");
        clock.Advance(180);
        Assert.IsTrue(pacing.InterstitialAllowed);
        pacing.MarkInterstitialShown();
        clock.Advance(299);
        Assert.IsFalse(pacing.InterstitialAllowed, "5 minute cooldown");
        clock.Advance(1);
        Assert.IsTrue(pacing.InterstitialAllowed);
        pacing.MarkRewardedClosed();
        clock.Advance(59);
        Assert.IsFalse(pacing.InterstitialAllowed, "60 s after rewarded");
        clock.Advance(1);
        Assert.IsTrue(pacing.InterstitialAllowed);
    }
}
