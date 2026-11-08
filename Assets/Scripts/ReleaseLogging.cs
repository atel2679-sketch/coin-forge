#if !DEVELOPMENT_BUILD && !UNITY_EDITOR
using UnityEngine;

/// <summary>
/// IC-06: release builds drop Debug.Log info lines ([Ads], [Consent], ...) from logcat; warnings, errors and
/// exceptions are still written so crashes stay diagnosable. (QA logs such as IC04_PERF / IC05_PLAYTEST are not
/// compiled into release at all.)
/// </summary>
static class ReleaseLogging
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Apply() => Debug.unityLogger.filterLogType = LogType.Warning;
}
#endif
