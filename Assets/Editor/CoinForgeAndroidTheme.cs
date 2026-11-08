#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;

/// <summary>
/// IC-06b: Unity's generated activity theme paints the window black until the first Unity frame, and the
/// Android 12+ system splash uses its own colour.
/// - Window background (all Android versions): game colour #1E1A16 with the coin/hammer icon
///   (icon_foreground_safe, no backdrop) at 160dp, placed so its coin sits where the IC-08 intro coin appears.
/// - Android 12+ system splash background: #1E1A16.
/// So a cold start shows one continuous scene from launcher to intro, with no white/black flash.
/// Runs on every Gradle project Unity generates (debug, release, AAB).
/// </summary>
sealed class CoinForgeAndroidTheme : IPostGenerateGradleAndroidProject
{
    public const string BackgroundHex = "#FF1E1A16";
    // 640px copy of Assets/Design/F_Store/icon_foreground_safe.png (sharp up to xxxhdpi at 160dp).
    const string StartupIconSource = "Tools/android-res/coinforge_startup_icon.png";
    // Intro coin (CoinForgeIntro: (-60,+60) ref units, 1080x1920 match 0.5) measured on the Redmi at 25dp left /
    // 12dp above centre; the coin's centre in the icon is at (0.439, 0.516) -> icon centre 15dp left, 15dp up.
    // In a layer-list, a centred item shifts by half its inset: right/bottom 30dp = 15dp left/up.
    const int IconSizeDp = 160, ShiftLeftDp = 15, ShiftUpDp = 15;

    public int callbackOrder => 0;

    public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
    {
        string res = Path.Combine(unityLibraryPath, "src", "main", "res");
        if (!Directory.Exists(res)) return;

        File.WriteAllText(Path.Combine(res, "values", "coinforge_colors.xml"),
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<resources>\n" +
            $"  <color name=\"coinforge_background\">{BackgroundHex}</color>\n</resources>\n");

        string nodpi = Path.Combine(res, "drawable-nodpi");
        Directory.CreateDirectory(nodpi);
        File.Copy(StartupIconSource, Path.Combine(nodpi, "coinforge_startup_icon.png"), true);

        string drawable = Path.Combine(res, "drawable");
        Directory.CreateDirectory(drawable);
        File.WriteAllText(Path.Combine(drawable, "coinforge_window_background.xml"),
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<layer-list xmlns:android=\"http://schemas.android.com/apk/res/android\" android:opacity=\"opaque\">\n" +
            "  <item android:drawable=\"@color/coinforge_background\" />\n" +
            $"  <item android:width=\"{IconSizeDp}dp\" android:height=\"{IconSizeDp}dp\" android:gravity=\"center\"\n" +
            $"        android:right=\"{ShiftLeftDp * 2}dp\" android:bottom=\"{ShiftUpDp * 2}dp\"\n" +
            "        android:drawable=\"@drawable/coinforge_startup_icon\" />\n" +
            "</layer-list>\n");

        int changed = 0;
        foreach (var dir in Directory.GetDirectories(res, "values*"))
        foreach (var file in Directory.GetFiles(dir, "*.xml"))
        {
            string xml = File.ReadAllText(file);
            string patched = Regex.Replace(xml,
                @"(<item name=""android:windowBackground"">)@android:color/black(</item>)",
                "$1@drawable/coinforge_window_background$2");
            patched = Regex.Replace(patched,
                @"(<item name=""android:windowSplashScreenBackground"">)@color/staticSplashScreenBackgroundColor(</item>)",
                "$1@color/coinforge_background$2");
            if (patched == xml) continue;
            File.WriteAllText(file, patched);
            changed++;
        }
        UnityEngine.Debug.Log($"[CoinForge] Android startup window: {BackgroundHex} + icon {IconSizeDp}dp in {changed} file(s)");
    }
}
#endif
