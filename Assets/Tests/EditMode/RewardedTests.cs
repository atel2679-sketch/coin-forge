using NUnit.Framework;

/// <summary>WO IC-04 B1: rewarded ads grant exactly once, only when earned, in either callback order.</summary>
public class RewardedTests
{
    RewardFlow flow;
    int grants;

    [SetUp]
    public void SetUp()
    {
        flow = new RewardFlow();
        grants = 0;
    }

    void Earned() { if (flow.OnEarned()) grants++; }
    void Closed() { if (flow.OnClosed()) grants++; }

    [Test]
    public void ClosedWithoutEarned_GrantsNothing()
    {
        flow.Begin(RewardType.Boost2x);
        Closed();
        Assert.AreEqual(0, grants);
    }

    [Test]
    public void EarnedBeforeClose_GrantsOnceOnClose()
    {
        flow.Begin(RewardType.Boost2x);
        Earned();
        Assert.AreEqual(0, grants, "must wait for the ad to close");
        Closed();
        Assert.AreEqual(1, grants);
    }

    [Test]
    public void EarnedAfterClose_GrantsOnce()   // IC-01 race fix
    {
        flow.Begin(RewardType.TimeWarp);
        Closed();
        Assert.AreEqual(0, grants);
        Earned();
        Assert.AreEqual(1, grants);
        Assert.AreEqual(RewardType.TimeWarp, flow.Pending);
    }

    [Test]
    public void DuplicateCallbacks_NeverGrantTwice()
    {
        flow.Begin(RewardType.OfflineDouble);
        Earned(); Closed(); Earned(); Closed(); Earned();
        Assert.AreEqual(1, grants);
    }

    [Test]
    public void CallbacksWithoutShow_AreIgnored()
    {
        Earned(); Closed();
        Assert.AreEqual(0, grants);
    }

    [Test]
    public void EachShow_GrantsAtMostOnce()
    {
        for (int i = 0; i < 3; i++)
        {
            flow.Begin(RewardType.Boost2x);
            Earned(); Closed(); Closed();
        }
        Assert.AreEqual(3, grants);
    }
}
