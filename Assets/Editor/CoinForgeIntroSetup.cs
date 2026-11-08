#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CoinForgeIntroSetup
{
    const string PrefabPath = "Assets/Prefabs/CoinForgeIntro.prefab";

    [MenuItem("CoinForge/Install Splash Intro")]
    public static void Install()
    {
        ImportSprite("Assets/Design/G_Intro/logo_coinforge.png", 2048);
        ImportSprite("Assets/Design/G_Intro/hammer.png", 512);
        var root = new GameObject("Coin Forge Intro", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CoinForgeIntro));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 30000;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        var serialized = new SerializedObject(root.GetComponent<CoinForgeIntro>());
        Bind(serialized, "background", "Assets/Design/D_UI/bg_main.png");
        Bind(serialized, "coin", "Assets/Design/B_Currency/coin_main.png");
        Bind(serialized, "hammer", "Assets/Design/G_Intro/hammer.png");
        Bind(serialized, "logo", "Assets/Design/G_Intro/logo_coinforge.png");
        Bind(serialized, "bar", "Assets/Design/D_UI/btn_gold.png");
        BindFrames(serialized, "coinBurst", "vfx_coin_burst_", 8);
        BindFrames(serialized, "sparkle", "vfx_sparkle_", 6);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        AddToScene(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void AddToScene(Scene scene)
    {
        if (scene.GetRootGameObjects().Any(root => root.GetComponent<CoinForgeIntro>() != null)) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new FileNotFoundException("Missing Coin Forge intro prefab", PrefabPath);
        PrefabUtility.InstantiatePrefab(prefab, scene);
    }

    static void Bind(SerializedObject owner, string field, string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new FileNotFoundException("Intro sprite missing", path);
        owner.FindProperty(field).objectReferenceValue = sprite;
    }

    static void BindFrames(SerializedObject owner, string field, string prefix, int count)
    {
        var frames = owner.FindProperty(field);
        frames.arraySize = count;
        for (int i = 0; i < count; i++)
            frames.GetArrayElementAtIndex(i).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Design/E_VFX/" + prefix + (i + 1).ToString("00") + ".png");
    }

    static void ImportSprite(string path, int maxSize)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = maxSize;
        var android = importer.GetPlatformTextureSettings("Android");
        android.overridden = true;
        android.maxTextureSize = maxSize;
        importer.SetPlatformTextureSettings(android);
        importer.SaveAndReimport();
    }
}
#endif
