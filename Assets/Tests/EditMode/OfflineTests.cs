using System.Globalization;
using System.Threading;
using NUnit.Framework;

/// <summary>WO IC-04 B4: rollback, cap, efficiency, doubler once. Plus the locale stamp bug found in IC-04.</summary>
public class OfflineTests
{
    const double Hour = 3600;
    const double Now = 1_800_000_000;
    const double Income = 10;   // coins/sec base

    static OfflineResult Compute(double now, double last, double max) =>
        OfflineCalc.Compute(now, last, max, Income, capHours: 8, efficiency: 0.5, minAwaySeconds: 60);

    [Test]
    public void ClockRolledBack_PaysZero()
    {
        var r = Compute(Now - Hour, last: Now, max: Now);
        Assert.IsTrue(r.Rollback);
        Assert.AreEqual(0, r.Amount);
    }

    [Test]
    public void RollbackThenForward_MeasuresFromHighWaterMark()
    {
        // Last stamp written after a rollback is low, but the high-water mark is not.
        var r = Compute(Now + 2 * Hour, last: Now - 5 * Hour, max: Now);
        Assert.AreEqual(2 * Hour, r.Seconds, 1e-6);
        Assert.AreEqual(2 * Hour * Income * 0.5, r.Amount, 1e-6);
    }

    [Test]
    public void StillBelowHighWaterMark_PaysZero()
    {
        var r = Compute(Now - 10, last: Now - 3 * Hour, max: Now);
        Assert.IsTrue(r.Rollback);
        Assert.AreEqual(0, r.Amount);
    }

    [Test]
    public void CappedAtEightHours()
    {
        var r = Compute(Now + 30 * Hour, last: Now, max: Now);
        Assert.AreEqual(8 * Hour, r.Seconds, 1e-6);
        Assert.AreEqual(8 * Hour * Income * 0.5, r.Amount, 1e-6);
    }

    [Test]
    public void EfficiencyIsFiftyPercent()
    {
        var r = Compute(Now + Hour, last: Now, max: Now);
        Assert.AreEqual(Hour * Income * 0.5, r.Amount, 1e-6);
    }

    [Test]
    public void ShortAbsenceAndFirstLaunch_PayZero()
    {
        Assert.AreEqual(0, Compute(Now + 59, Now, Now).Amount);
        Assert.AreEqual(0, Compute(Now, last: 0, max: 0).Amount);
    }

    [Test]
    public void OfflineDouble_OnlyOnce()
    {
        var claim = new OfflineClaim();
        claim.Offer(100, Hour);
        Assert.AreEqual(200, claim.CollectDoubled());
        Assert.AreEqual(0, claim.CollectDoubled());
        Assert.AreEqual(0, claim.Collect());
    }

    [Test]
    public void OfflineDouble_AfterCollect_PaysNothing()
    {
        var claim = new OfflineClaim();
        claim.Offer(100, Hour);
        Assert.AreEqual(100, claim.Collect());
        Assert.AreEqual(0, claim.CollectDoubled());
    }

    [Test]
    public void Stamp_RoundTripsUnderCommaDecimalLocale()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "id-ID", "de-DE", "en-US" })
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                const double stamp = 1793876804.972;
                Assert.AreEqual(stamp, OfflineCalc.ParseStamp(OfflineCalc.FormatStamp(stamp)), 1e-6, culture);
            }
        }
        finally { Thread.CurrentThread.CurrentCulture = previous; }
    }

    [Test]
    public void Stamp_GarbageParsesAsZero()
    {
        Assert.AreEqual(0, OfflineCalc.ParseStamp("not a number"));
        Assert.AreEqual(0, OfflineCalc.ParseStamp(null));
        Assert.AreEqual(0, OfflineCalc.ParseStamp("NaN"));
    }
}
