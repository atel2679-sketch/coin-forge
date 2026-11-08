#if CF_STORE_SHOTS
using System.Globalization;
using UnityEngine;

/// <summary>
/// IC-06 Play Store screenshots only. Compiled solely into CoinForgeSetup.BuildStoreShotsApk (define CF_STORE_SHOTS),
/// never into the release AAB. Launch with  am start ... -e unity "-storeshot N"  (N = 1..4) to start from a
/// preset save; the banner is never loaded in this build, so the screen has no ad.
/// </summary>
static class StoreShots
{
    // owned per building, coins, lifetime, gems, boost minutes left, hours offline
    static readonly (int[] owned, double coins, double lifetime, int gems, int boostMin, double offlineH)[] Presets =
    {
        (new[] { 25, 18, 10, 4, 1, 0, 0, 0, 0, 0 }, 4.2e4, 1.1e5, 0, 0, 0),
        (new[] { 50, 42, 30, 25, 14, 6, 2, 0, 0, 0 }, 3.1e7, 9.4e7, 0, 105, 0),
        (new[] { 100, 85, 70, 60, 50, 42, 31, 20, 9, 3 }, 8.5e10, 4.6e12, 12, 340, 0),
        (new[] { 50, 42, 30, 25, 14, 6, 2, 0, 0, 0 }, 3.1e7, 9.4e7, 0, 0, 3),
    };

    public static bool Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Seed()
    {
        Active = true;
        var args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-storeshot");
        if (i < 0 || i + 1 >= args.Length || !int.TryParse(args[i + 1], out int n) || n < 1 || n > Presets.Length) return;
        var p = Presets[n - 1];
        double now = GameClock.Current.UtcNow;
        var inv = CultureInfo.InvariantCulture;
        string owned = string.Join(",", p.owned);
        string boost = (p.boostMin > 0 ? now + p.boostMin * 60 : 0).ToString("R", inv);
        PlayerPrefs.DeleteAll();
        PlayerPrefs.SetString("ic_save_v1",
            $"{{\"saveVersion\":2,\"coins\":{p.coins.ToString("R", inv)},\"lifetime\":{p.lifetime.ToString("R", inv)}," +
            $"\"boostEnd\":{boost},\"gems\":{p.gems},\"owned\":[{owned}]}}");
        PlayerPrefs.SetInt("ic_tutorial_done", 1);
        PlayerPrefs.SetString("ic02_foreground_seconds", "5400");
        string stamp = OfflineCalc.FormatStamp(now - p.offlineH * 3600);
        PlayerPrefs.SetString("ic_last_utc", stamp);
        PlayerPrefs.SetString("ic_max_utc", stamp);
        PlayerPrefs.Save();
        Debug.LogWarning("[StoreShots] preset " + n);
    }
}
#endif
