using System;
using UnityEngine;

/// <summary>Presentation-only gates. Game simulation and SDK initialization keep running.</summary>
public static class CoinForgePresentation
{
    public static event Action Changed;
    static bool introActive, introFading, startupPending, tutorialActive;
    public static bool IntroActive { get => introActive; set => Set(ref introActive, value); }
    public static bool IntroFading { get => introFading; set => Set(ref introFading, value); }
    public static bool StartupPending { get => startupPending; set => Set(ref startupPending, value); }
    public static bool TutorialActive { get => tutorialActive; set => Set(ref tutorialActive, value); }
    public static bool AdsBlocked => introActive || startupPending || tutorialActive;

    static void Set(ref bool field, bool value)
    {
        if (field == value) return;
        field = value;
        Changed?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        introActive = introFading = startupPending = tutorialActive = false;
        Changed = null;
    }
}
