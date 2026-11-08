using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// IC-06: one set of AdMob IDs (App ID + the three ad units). Pure data so the release guard is unit-tested.
/// </summary>
public readonly struct AdIds
{
    public readonly string AppId, Rewarded, Interstitial, Banner;

    public AdIds(string appId, string rewarded, string interstitial, string banner)
    {
        AppId = appId?.Trim() ?? "";
        Rewarded = rewarded?.Trim() ?? "";
        Interstitial = interstitial?.Trim() ?? "";
        Banner = banner?.Trim() ?? "";
    }

    /// <summary>Google's public sample IDs. Used automatically in the Editor and in Development Builds.</summary>
    public static readonly AdIds Test = new AdIds(
        "ca-app-pub-3940256099942544~3347511713",
        "ca-app-pub-3940256099942544/5224354917",
        "ca-app-pub-3940256099942544/1033173712",
        "ca-app-pub-3940256099942544/9214589741");

    const string TestPublisher = "ca-app-pub-3940256099942544";
    static readonly Regex AppIdFormat = new Regex(@"^ca-app-pub-\d{16}~\d{10}$");
    static readonly Regex UnitIdFormat = new Regex(@"^ca-app-pub-\d{16}/\d{10}$");

    /// <summary>
    /// Why these IDs cannot ship in a release build (empty list = OK): missing, malformed, Google test IDs,
    /// units from another publisher than the App ID, or the same unit used twice.
    /// </summary>
    public List<string> ReleaseProblems()
    {
        var problems = new List<string>();
        Check(problems, "App ID", AppId, AppIdFormat);
        Check(problems, "Rewarded", Rewarded, UnitIdFormat);
        Check(problems, "Interstitial", Interstitial, UnitIdFormat);
        Check(problems, "Banner", Banner, UnitIdFormat);
        if (problems.Count > 0) return problems;

        string publisher = AppId.Substring(0, AppId.IndexOf('~'));
        foreach (var (name, id) in new[] { ("Rewarded", Rewarded), ("Interstitial", Interstitial), ("Banner", Banner) })
            if (!id.StartsWith(publisher + "/")) problems.Add(name + " unit belongs to another publisher than the App ID");
        if (Rewarded == Interstitial || Rewarded == Banner || Interstitial == Banner)
            problems.Add("The same ad unit ID is used for two formats");
        return problems;
    }

    static void Check(List<string> problems, string name, string id, Regex format)
    {
        if (id.Length == 0) problems.Add(name + " is empty");
        else if (id.StartsWith(TestPublisher)) problems.Add(name + " is a Google test ID");
        else if (!format.IsMatch(id)) problems.Add(name + " has the wrong format: " + id);
    }
}
