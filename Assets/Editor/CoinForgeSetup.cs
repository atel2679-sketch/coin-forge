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
        PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)23;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto; // highest installed SDK
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.runInBackground = false;
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

    // Writes the test App ID into the plugin settings asset. Reflection so this compiles without the plugin.
    [MenuItem("CoinForge/Apply AdMob Test App ID")]
    public static void ApplyAdMobAppId()
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
        if (android != null) android.stringValue = AdMobTestAppId;
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
    public static void BuildDebugApk()
    {
        RunAll();
        ResolveAndroidDeps();
        Directory.CreateDirectory("Builds");
        var rep = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/CoinForge-debug.apk",
            target = BuildTarget.Android,
            options = BuildOptions.Development
        });
        if (rep.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
#endif
