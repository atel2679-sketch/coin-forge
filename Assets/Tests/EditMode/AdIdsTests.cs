using NUnit.Framework;

/// IC-06: the release guard for production AdMob IDs.
public class AdIdsTests
{
    const string App = "ca-app-pub-1234567890123456~1111111111";
    const string R = "ca-app-pub-1234567890123456/2222222222";
    const string I = "ca-app-pub-1234567890123456/3333333333";
    const string B = "ca-app-pub-1234567890123456/4444444444";

    [Test] public void ValidProductionIdsPass() =>
        Assert.IsEmpty(new AdIds(App, R, I, B).ReleaseProblems());

    [Test] public void WhitespaceIsTrimmed() =>
        Assert.IsEmpty(new AdIds(" " + App + "\n", R + " ", I, B).ReleaseProblems());

    [Test] public void GoogleTestIdsAreRejected() =>
        Assert.AreEqual(4, AdIds.Test.ReleaseProblems().Count);

    [Test] public void EmptyIdsAreRejected() =>
        Assert.AreEqual(4, new AdIds(null, "", " ", null).ReleaseProblems().Count);

    [Test] public void AppIdAndUnitIdFormatsAreNotInterchangeable()
    {
        // App ID uses '~', ad units use '/': a swapped paste must not pass.
        var problems = new AdIds(R, App, I, B).ReleaseProblems();
        Assert.AreEqual(2, problems.Count);
    }

    [Test] public void UnitFromAnotherPublisherIsRejected() =>
        Assert.AreEqual(1, new AdIds(App, "ca-app-pub-9999999999999999/2222222222", I, B).ReleaseProblems().Count);

    [Test] public void SameUnitTwiceIsRejected() =>
        Assert.AreEqual(1, new AdIds(App, R, R, B).ReleaseProblems().Count);
}
