using System;
using System.Globalization;

public struct OfflineResult
{
    public double Amount, Seconds;
    public bool Rollback;
}

/// <summary>Offline earnings math (pure). Stamps are UTC Unix seconds.</summary>
public static class OfflineCalc
{
    /// <param name="last">Last stamp written while the app ran.</param>
    /// <param name="max">Highest stamp ever written (high-water mark against clock games).</param>
    public static OfflineResult Compute(double now, double last, double max, double baseIncome,
        double capHours, double efficiency, double minAwaySeconds)
    {
        var r = new OfflineResult();
        if (last <= 0) return r;                                // first launch
        if (now < last || now < max) { r.Rollback = true; return r; }

        double away = now - Math.Max(last, max);
        if (away < minAwaySeconds) return r;
        r.Seconds = Math.Min(away, capHours * 3600);
        r.Amount = Math.Max(0, baseIncome * r.Seconds * efficiency);
        return r;
    }

    // Stamps must round-trip regardless of device locale (e.g. id-ID / de-DE use a decimal comma).
    public static string FormatStamp(double v) => v.ToString("R", CultureInfo.InvariantCulture);

    public static double ParseStamp(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && !double.IsNaN(v) ? v : 0;
}

/// <summary>Pending offline reward: collect once, or double once via a rewarded ad.</summary>
public sealed class OfflineClaim
{
    bool doubled;

    public double Pending { get; private set; }
    public double AwaySeconds { get; private set; }

    public void Offer(double amount, double seconds)
    {
        Pending = Math.Max(0, amount);
        AwaySeconds = seconds;
        doubled = false;
    }

    /// <returns>Amount to credit (0 if nothing pending).</returns>
    public double Collect()
    {
        double amount = Pending;
        Pending = 0;
        return amount;
    }

    /// <returns>Twice the pending amount, at most once per offer; 0 otherwise.</returns>
    public double CollectDoubled()
    {
        if (Pending <= 0 || doubled) return 0;
        doubled = true;
        Pending *= 2;
        return Collect();
    }
}
