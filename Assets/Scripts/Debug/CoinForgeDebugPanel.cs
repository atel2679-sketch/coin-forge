#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// IC-04 developer cheat panel. The whole file is compiled out of release builds.
/// Open it by holding the "COIN FORGE" HUD title for 3 seconds.
/// It is IMGUI on its own GameObject, so it adds nothing to Codex's Canvas and costs nothing while closed
/// (the component is disabled, so OnGUI never runs and never allocates).
/// </summary>
public sealed class CoinForgeDebugPanel : MonoBehaviour
{
    const string TitleText = "COIN FORGE";
    const float HoldSeconds = 3f;
    // Layout in 1080-wide reference pixels; scaled to the screen. Rows are fixed so adb taps are predictable.
    public const float RefWidth = 1080, Top = 300, RowHeight = 130, RowGap = 16, Left = 90, ButtonWidth = 900;

    public static CoinForgeDebugPanel I { get; private set; }

    static readonly (string label, System.Action action)[] Actions =
    {
        ("+1e9 coins", () => IdleEngine.I.DebugAddCoins(1e9)),
        ("+1e12 coins", () => IdleEngine.I.DebugAddCoins(1e12)),
        ("Skip time +1 hour (offline)", () => FindFirstObjectByType<OfflineEarnings>().DebugSkip(3600)),
        ("Reset save (closes app)", ResetSave),
        ("Consent: toggle EEA test geography", ToggleEea),
        ("Consent: reset (applies on restart)", AdManager.DebugResetConsent),
        (PerfLabel, () => CoinForgePerfProbe.Begin(60)),
        ("Close", null),
    };

    const string PerfLabel = "Perf probe 60 s (closes panel)";
    GUIStyle button, label;
    string status = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        var go = new GameObject("IC-04 Debug Panel (dev only)");
        DontDestroyOnLoad(go);
        I = go.AddComponent<CoinForgeDebugPanel>();
        I.enabled = false;                     // closed: no OnGUI calls at all
        I.StartCoroutine(I.AttachToTitle());
    }

    IEnumerator AttachToTitle()
    {
        // CoinForgeUI builds its view in Start; wait for the title label to exist.
        for (int attempt = 0; attempt < 600; attempt++)
        {
            foreach (var text in FindObjectsByType<Text>(FindObjectsSortMode.None))
            {
                if (text.text != TitleText) continue;
                text.raycastTarget = true;
                text.gameObject.AddComponent<HoldToOpen>();
                Debug.Log("[Debug] Panel armed on HUD title (hold 3 s)");
                yield break;
            }
            yield return null;
        }
        Debug.LogWarning("[Debug] HUD title not found; panel unavailable");
    }

    public void Open() { status = EeaState(); enabled = true; }

    static string EeaState() => "Consent EEA test: " + (PlayerPrefs.GetInt(AdManager.DebugEeaKey, 0) == 1 ? "ON" : "OFF");

    static void ToggleEea()
    {
        PlayerPrefs.SetInt(AdManager.DebugEeaKey, PlayerPrefs.GetInt(AdManager.DebugEeaKey, 0) == 1 ? 0 : 1);
        PlayerPrefs.Save();
        Debug.Log("[Debug] " + EeaState() + " (restart the app)");
    }
    public void Close() => enabled = false;

    void OnGUI()
    {
        float s = Screen.width / RefWidth;
        if (button == null)
        {
            button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(44 * s), wordWrap = true };
            label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(36 * s), wordWrap = true };
            label.normal.textColor = Color.white;
        }
        float panelHeight = Top + Actions.Length * (RowHeight + RowGap) + 180;
        GUI.color = new Color(0, 0, 0, 0.85f);
        GUI.DrawTexture(new Rect(40 * s, (Top - 200) * s, 1000 * s, panelHeight * s), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(Left * s, (Top - 180) * s, ButtonWidth * s, 160 * s),
            "IC-04 DEBUG (dev build only)\n" + status, label);

        for (int i = 0; i < Actions.Length; i++)
        {
            var rect = new Rect(Left * s, (Top + i * (RowHeight + RowGap)) * s, ButtonWidth * s, RowHeight * s);
            if (!GUI.Button(rect, Actions[i].label, button)) continue;
            if (Actions[i].action == null) { Close(); continue; }
            Actions[i].action();
            status = "Done: " + Actions[i].label + " / " + EeaState();
            Debug.Log("[Debug] " + Actions[i].label);
            if (Actions[i].label == PerfLabel) Close();
        }
    }

    static void ResetSave()
    {
        IdleEngine.DebugSaveBlocked = true;    // nothing may write the old state back
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[Debug] Save wiped; closing app");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_ANDROID
        // Kill the process so no OnApplicationQuit/Pause handler can re-save.
        using var process = new AndroidJavaClass("android.os.Process");
        process.CallStatic("killProcess", process.CallStatic<int>("myPid"));
#else
        Application.Quit();
#endif
    }

    /// <summary>Long-press detector placed on the HUD title.</summary>
    sealed class HoldToOpen : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        float pressedAt = -1;

        void Awake() => enabled = false;       // Update only runs while a finger is down

        public void OnPointerDown(PointerEventData e) { pressedAt = Time.unscaledTime; enabled = true; }
        public void OnPointerUp(PointerEventData e) => Cancel();
        public void OnPointerExit(PointerEventData e) => Cancel();

        void Cancel() { pressedAt = -1; enabled = false; }

        void Update()
        {
            if (pressedAt < 0 || Time.unscaledTime - pressedAt < HoldSeconds) return;
            Cancel();
            I.Open();
        }
    }
}
#endif
