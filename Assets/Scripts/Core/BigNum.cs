using System;
using System.Globalization;

/// <summary>Lightweight big-number formatter for doubles (up to ~1.8e308). Zero allocations beyond the result string.</summary>
public static class BigNum
{
    static readonly string[] Suffix = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };

    public static string Format(double v, int decimals = 2)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return "∞";
        if (v < 0) return "-" + Format(-v, decimals);
        if (v < 1000) return v < 10 && v % 1 != 0 ? v.ToString("0.#", CultureInfo.InvariantCulture) : Math.Floor(v).ToString("0", CultureInfo.InvariantCulture);

        int tier = (int)(Math.Log10(v) / 3);           // 1 = thousands, 2 = millions...
        double scaled = v / Math.Pow(1000, tier);
        if (scaled >= 999.995) { scaled /= 1000; tier++; } // prevent "1000K"

        string num = scaled.ToString(scaled >= 100 ? "0" : scaled >= 10 ? "0.#" : "0.##", CultureInfo.InvariantCulture);
        return num + SuffixFor(tier);
    }

    static string SuffixFor(int tier)
    {
        if (tier < Suffix.Length) return Suffix[tier];
        int i = tier - Suffix.Length;                    // aa, ab, ... zz
        return ((char)('a' + (i / 26) % 26)).ToString() + (char)('a' + i % 26);
    }

    /// <summary>Formats seconds as "2h 05m" / "4m 10s".</summary>
    public static string Time(double s)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, s));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m" : $"{t.Minutes}m {t.Seconds:00}s";
    }
}
