#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// IC-01 one-shot project setup. Runs automatically on first open (and via menu CoinForge/*).
/// Creates Assets/Scenes/Main.unity, applies Android player settings, App ID, build settings.
/// </summary>
[InitializeOnLoad]
public static class CoinForgeSetup
{
    public const string PackageName = "com.publicvoid.coinforge";
    public const string AdMobTestAppId = "ca-app-pub-3940256099942544~3347511713";
    const string ScenePath = "Assets/Scenes/Main.unity";
    // IC-06 release version.
    public const string VersionName = "1.0.0";
    public const int VersionCode = 1;
    const string AdConfigPath = "Assets/Resources/AdConfig.asset";

    static CoinForgeSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(ScenePath)) RunAll();
        };
    }

    [MenuItem("CoinForge/Run All Setup")]
    public static void RunAll()
    {
        ApplyPlayerSettings();
        CreateMainScene();
        ApplyAdMobAppId();
        Debug.Log("[CoinForge] Setup complete.");
    }

    [MenuItem("CoinForge/Apply Player Settings")]
    public static void ApplyPlayerSettings()
    {
        PlayerSettings.companyName = "PublicVoid";
        PlayerSettings.productName = "CoinForge";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)25; // Unity 6.3 minimum (PM-approved in IC-01)
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto; // highest installed SDK
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.runInBackground = false;
        PlayerSettings.enableFrameTimingStats = true; // IC-04: CPU/GPU frame times for the perf probe
        PlayerSettings.bundleVersion = VersionName;
        PlayerSettings.Android.bundleVersionCode = VersionCode;
        // IC-06: R8 shrinking for release; keep rules in Assets/Plugins/Android/proguard-user.txt.
        PlayerSettings.Android.minifyRelease = true;
        PlayerSettings.Android.minifyDebug = false;
        SetPlayerSetting("useCustomProguardFile", true);
        EditorUserBuildSettings.androidBuildType = AndroidBuildType.Debug;
        EditorUserBuildSettings.buildAppBundle = false;
    }

    [MenuItem("CoinForge/Create Main Scene")]
    public static void CreateMainScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var camera = Camera.main;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(30, 26, 22, 255);
        camera.cullingMask = 0;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.useOcclusionCulling = false;

        var root = new GameObject("GameRoot");
        root.AddComponent<IdleEngine>();
        root.AddComponent<OfflineEarnings>();

        var ads = new GameObject("Ads");
        ads.AddComponent<AdManager>();

        var ui = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CoinForgeUI.prefab");
        if (ui == null) throw new FileNotFoundException("Missing Coin Forge UI prefab");
        PrefabUtility.InstantiatePrefab(ui, scene);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    static void SetPlayerSetting(string name, bool value)
    {
        var so = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
        var prop = so.FindProperty(name);
        if (prop == null) throw new BuildFailedException("PlayerSettings." + name + " not found");
        prop.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // IC-06: the one AdMob config asset (production IDs + test devices). Created empty if missing.
    [MenuItem("CoinForge/Select Ad Config")]
    public static void SelectAdConfig() => Selection.activeObject = EnsureAdConfig();

    public static AdConfig EnsureAdConfig()
    {
        var config = AssetDatabase.LoadAssetAtPath<AdConfig>(AdConfigPath);
        if (config != null) return config;
        Directory.CreateDirectory(Path.GetDirectoryName(AdConfigPath));
        config = ScriptableObject.CreateInstance<AdConfig>();
        AssetDatabase.CreateAsset(config, AdConfigPath);
        AssetDatabase.SaveAssets();
        return config;
    }

    // Writes an App ID into the plugin settings asset. Reflection so this compiles without the plugin.
    // The committed asset always holds the test App ID; a release build swaps in the production one while it builds.
    [MenuItem("CoinForge/Apply AdMob Test App ID")]
    public static void ApplyAdMobAppId() => ApplyAdMobAppId(AdMobTestAppId);

    static void ApplyAdMobAppId(string appId)
    {
        var t = System.Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor");
        if (t == null)
        {
            Debug.LogWarning("[CoinForge] GoogleMobileAds plugin not loaded yet; rerun CoinForge/Apply AdMob Test App ID after import.");
            return;
        }
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        var inst = t.GetMethod("LoadInstance", flags)?.Invoke(null, null) as ScriptableObject;
        if (inst == null) { Debug.LogWarning("[CoinForge] Could not load GoogleMobileAdsSettings."); return; }
        var so = new SerializedObject(inst);
        var android = so.FindProperty("adMobAndroidAppId");
        if (android != null) android.stringValue = appId;
        else Debug.LogWarning("[CoinForge] adMobAndroidAppId field not found - set App ID in Assets > Google Mobile Ads > Settings.");
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(inst);
        AssetDatabase.SaveAssets();
    }

    // External Dependency Manager -> Android Resolver (sync). Reflection: EDM may not be loaded yet.
    [MenuItem("CoinForge/Resolve Android Dependencies")]
    public static void ResolveAndroidDeps()
    {
        var t = System.Type.GetType("GooglePlayServices.PlayServicesResolver, Google.JarResolver");
        if (t == null)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                if ((t = asm.GetType("GooglePlayServices.PlayServicesResolver")) != null) break;
        }
        if (t == null) { Debug.LogWarning("[CoinForge] EDM PlayServicesResolver not found."); return; }
        var m = t.GetMethod("ResolveSync", new[] { typeof(bool) });
        var ok = m?.Invoke(null, new object[] { true });
        Debug.Log("[CoinForge] Android Resolver result: " + ok);
    }

    // CLI: Unity -batchmode -quit -executeMethod CoinForgeSetup.BuildDebugApk
    public static void BuildDebugApk() => Build("Builds/CoinForge-debug.apk", BuildOptions.Development, AndroidBuildType.Debug);

    // IC-04: release-configuration APK (debug-signed) to verify dev-only code is stripped.
    // CLI: Unity -batchmode -quit -executeMethod CoinForgeSetup.BuildReleaseApk
    public static void BuildReleaseApk() => Build("Builds/CoinForge-release.apk", BuildOptions.None, AndroidBuildType.Release);

    // IC-06: Play Store release. AAB, IL2CPP arm64, R8, production AdMob IDs, release keystore.
    // Signing comes from environment variables only; passwords are never written to the project:
    //   CF_KEYSTORE_PATH, CF_KEY_ALIAS, CF_KEYSTORE_PASS, CF_KEY_PASS
    // CLI: Unity -batchmode -quit -executeMethod CoinForgeSetup.BuildReleaseAab
    public static void BuildReleaseAab()
    {
        var config = EnsureAdConfig();
        var problems = config.Production.ReleaseProblems();
        if (problems.Count > 0)
            Fail("Production AdMob IDs in " + AdConfigPath + " are not ready:\n  " + string.Join("\n  ", problems));
        string keystore = Env("CF_KEYSTORE_PATH"), alias = Env("CF_KEY_ALIAS");
        string storePass = Env("CF_KEYSTORE_PASS"), keyPass = Env("CF_KEY_PASS");
        if (keystore == null || alias == null || storePass == null || keyPass == null)
            Fail("Set CF_KEYSTORE_PATH, CF_KEY_ALIAS, CF_KEYSTORE_PASS and CF_KEY_PASS for the release keystore.");
        if (!File.Exists(keystore)) Fail("Keystore not found: " + keystore);

        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = Path.GetFullPath(keystore);
        PlayerSettings.Android.keyaliasName = alias;
        PlayerSettings.Android.keystorePass = storePass;
        PlayerSettings.Android.keyaliasPass = keyPass;
        try
        {
            Build($"Builds/CoinForge-{VersionName}-{VersionCode}.aab", BuildOptions.None, AndroidBuildType.Release,
                  appBundle: true, appId: config.Production.AppId);
        }
        finally
        {
            // Leave no signing data in ProjectSettings.asset (passwords are never serialized; path/alias would be).
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.keystoreName = "";
            PlayerSettings.Android.keyaliasName = "";
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasPass = "";
            AssetDatabase.SaveAssets();
            ScrubGradlePasswords();
        }
    }

    // Unity writes the signing passwords in plain text into the generated Gradle project (Library/, gitignored).
    // Blank them after the build so they don't stay on disk.
    static void ScrubGradlePasswords()
    {
        const string gradle = "Library/Bee/Android/Prj/IL2CPP/Gradle/launcher/build.gradle";
        if (!File.Exists(gradle)) return;
        var text = File.ReadAllText(gradle);
        var scrubbed = System.Text.RegularExpressions.Regex.Replace(
            text, @"(?m)^(\s*(?:storePassword|keyPassword))\s+.*$", "$1 ''");
        if (scrubbed != text) File.WriteAllText(gradle, scrubbed);
    }

    // IC-06 dry run: the release AAB pipeline (release config, R8, AAB) with Google test IDs and the debug keystore,
    // to test minify and the bundle on a device before the production IDs/keystore exist. Not for upload.
    // CLI: Unity -batchmode -quit -executeMethod CoinForgeSetup.BuildReleaseAabDryRun
    public static void BuildReleaseAabDryRun()
    {
        var config = EnsureAdConfig();
        string[] saved = { config.appId, config.rewardedId, config.interstitialId, config.bannerId };
        SetProductionIds(AdIds.Test.AppId, AdIds.Test.Rewarded, AdIds.Test.Interstitial, AdIds.Test.Banner);
        try
        {
            Build("Builds/CoinForge-dryrun.aab", BuildOptions.None, AndroidBuildType.Release,
                  appBundle: true, appId: AdIds.Test.AppId);
        }
        finally
        {
            SetProductionIds(saved[0], saved[1], saved[2], saved[3]);
        }
    }

    // IC-06 Play Store screenshots: release configuration (no "Development Build" mark), test IDs, no banner,
    // preset saves via -storeshot N (Assets/Scripts/StoreShots.cs). Not for upload.
    // CLI: Unity -batchmode -quit -executeMethod CoinForgeSetup.BuildStoreShotsApk
    public static void BuildStoreShotsApk()
    {
        var config = EnsureAdConfig();
        string[] saved = { config.appId, config.rewardedId, config.interstitialId, config.bannerId };
        SetProductionIds(AdIds.Test.AppId, AdIds.Test.Rewarded, AdIds.Test.Interstitial, AdIds.Test.Banner);
        try
        {
            Build("Builds/CoinForge-storeshots.apk", BuildOptions.None, AndroidBuildType.Release,
                  appId: AdIds.Test.AppId, defines: new[] { "CF_STORE_SHOTS" });
        }
        finally
        {
            SetProductionIds(saved[0], saved[1], saved[2], saved[3]);
        }
    }

    static void SetProductionIds(string app, string rewarded, string interstitial, string banner)
    {
        var config = EnsureAdConfig();   // load by path: a build unloads earlier references
        config.appId = app; config.rewardedId = rewarded; config.interstitialId = interstitial; config.bannerId = banner;
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
    }

    static string Env(string name)
    {
        var v = System.Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(v) ? null : v;
    }

    static void Fail(string message)
    {
        Debug.LogError("[CoinForge] " + message);
        throw new BuildFailedException(message);
    }

    static void Build(string path, BuildOptions options, AndroidBuildType type,
                      bool appBundle = false, string appId = AdMobTestAppId, string[] defines = null)
    {
        // IC-04: don't regenerate Main.unity on every build (it churned fileIDs); create it only if missing.
        ApplyPlayerSettings();
        if (!File.Exists(ScenePath)) CreateMainScene();
        EnsureAdConfig();
        ApplyAdMobAppId(appId);
        EditorUserBuildSettings.androidBuildType = type;
        EditorUserBuildSettings.buildAppBundle = appBundle;
        ResolveAndroidDeps();
        Directory.CreateDirectory("Builds");
        bool ok;
        try
        {
            var rep = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = BuildTarget.Android,
                options = options,
                extraScriptingDefines = defines
            });
            Debug.Log($"[CoinForge] Build {path}: {rep.summary.result}");
            ok = rep.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
        }
        finally
        {
            // The committed plugin settings always hold the test App ID (restored even when the build fails).
            ApplyAdMobAppId(AdMobTestAppId);
            EditorUserBuildSettings.buildAppBundle = false;
        }
        if (!ok) throw new BuildFailedException("Build failed: " + path);   // callers' finally blocks still run
    }
}
#endif
