#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Globalization;
using UnityEngine;

/// <summary>
/// IC-05 dev-only playtest bot. Plays in real time on the device so balance can be checked with the real game,
/// ads and consent. Taps through IdleEngine.Tap() at a tapering rate and buys with a "saver" policy:
/// buy the next new tier as soon as it is affordable; otherwise buy the best-payback owned building only if it pays
/// for itself before the new tier would be reached. Logs "IC05_PLAYTEST" lines with time since app start.
/// </summary>
public sealed class CoinForgePlaytestBot : MonoBehaviour
{
    // Tap profile (taps/s by minute of play): active at first, idler later.
    static readonly (float untilSeconds, float tapsPerSecond)[] Profile = { (60, 3f), (240, 2f), (float.MaxValue, 1f) };

    float tapDebt, nextDecision;
    readonly bool[] logged = new bool[10];
    bool timeWarpSeen;
    Transform timeWarpButton;

    public static void Begin()
    {
        if (FindFirstObjectByType<CoinForgePlaytestBot>() != null) return;
        var go = new GameObject("IC-05 Playtest Bot");
        DontDestroyOnLoad(go);
        go.AddComponent<CoinForgePlaytestBot>();
        Log("start profile=3/s<1:00, 2/s<4:00, 1/s after");
    }

    static string Clock(double s) => ((int)s / 60) + ":" + ((int)s % 60).ToString("00", CultureInfo.InvariantCulture);
    static void Log(string msg) => Debug.Log("IC05_PLAYTEST t=" + Clock(Time.realtimeSinceStartupAsDouble) + " " + msg);

    void Start()
    {
        var engine = IdleEngine.I;
        for (int i = 0; i < logged.Length; i++) logged[i] = engine.Upgrades.Owned[i] > 0;
        foreach (var t in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == "Time Warp ad") { timeWarpButton = t; break; }
    }

    void Update()
    {
        var engine = IdleEngine.I;
        if (engine == null) return;
        double now = Time.realtimeSinceStartupAsDouble;

        // Taps.
        float rate = 1;
        foreach (var step in Profile) if (now < step.untilSeconds) { rate = step.tapsPerSecond; break; }
        tapDebt += rate * Time.unscaledDeltaTime;
        while (tapDebt >= 1) { engine.Tap(); tapDebt -= 1; }

        // The real UI button (CoinForgeUI decides its visibility).
        if (!timeWarpSeen && timeWarpButton != null && timeWarpButton.gameObject.activeInHierarchy)
        {
            timeWarpSeen = true;
            Log("timewarp_first_shown income=" + engine.BaseIncomePerSec.ToString("0.##", CultureInfo.InvariantCulture)
                + " goal_s=" + engine.SecondsToNextGoal().ToString("0", CultureInfo.InvariantCulture));
        }

        if (now < nextDecision) return;
        nextDecision = (float)now + 0.25f;
        Decide(engine, rate);
    }

    void Decide(IdleEngine engine, float tapsPerSecond)
    {
        var up = engine.Upgrades;
        for (int guard = 0; guard < 50; guard++)
        {
            int next = -1;
            for (int i = 0; i < up.Owned.Length; i++) if (up.Owned[i] == 0) { next = i; break; }
            if (next >= 0 && engine.Coins >= up.CostOf(next))
            {
                engine.Buy(next, 1);
                continue;
            }
            double income = engine.IncomePerSec + tapsPerSecond * engine.TapValue;
            double toGoal = next >= 0 ? (up.CostOf(next) - engine.Coins) / System.Math.Max(income, 1e-9) : double.MaxValue;
            int best = -1; double bestPayback = double.MaxValue;
            for (int i = 0; i < up.Owned.Length; i++)
            {
                if (up.Owned[i] == 0 || up.CostOf(i) > engine.Coins) continue;
                double gain = (up.Owned[i] + 1) * up.Defs[i].baseProd * System.Math.Pow(2, (up.Owned[i] + 1) / 25)
                            - up.ProdOf(i);
                double payback = up.CostOf(i) / gain;
                if (payback < toGoal && payback < bestPayback) { best = i; bestPayback = payback; }
            }
            if (best < 0) break;
            engine.Buy(best, 1);
        }
        for (int i = 0; i < logged.Length; i++)
        {
            if (logged[i] || up.Owned[i] == 0) continue;
            logged[i] = true;
            Log("unlock T" + (i + 1) + " " + up.Defs[i].name + " coins_income=" +
                engine.IncomePerSec.ToString("0.##", CultureInfo.InvariantCulture));
        }
    }
}
#endif
