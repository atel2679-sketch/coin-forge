# IC-04 · Testing, performance, consent

Unity 6000.3.27f1 · Redmi Note 7 (Android 10, Adreno 512, Vulkan) · starts from `0e76107` (IC-07b).

| Commit | Section |
|---|---|
| `c09a356` | IC-04 debug |
| `127146d` | IC-04 tests |
| `8f25743` | IC-04 consent |
| `caee780` | IC-04 perf |

## A. Debug panel (dev builds only)

Hold the **COIN FORGE** HUD title for 3 s. The panel offers +1e9 coins, +1e12 coins, skip offline time +1 h, reset save (wipes PlayerPrefs and closes the app), consent EEA test on/off, consent reset, and a 60 s perf probe.

- `Assets/Scripts/Debug/` and every debug hook in core scripts sit inside `#if DEVELOPMENT_BUILD || UNITY_EDITOR`.
- The panel is IMGUI on its own GameObject. It is disabled while closed, so it adds no per-frame UI cost and does not touch the Codex Canvas. While open it disables the EventSystem so taps do not reach the game.
- Release check: see [Release build](#release-build).

## B. EditMode tests — 46/46 passed

Results: [`editmode-results.xml`](editmode-results.xml) (Unity Test Framework 1.6.0, batchmode `-runTests -testPlatform EditMode`).

| WO item | Fixture | Tests |
|---|---|---|
| B1 Rewarded | `RewardedTests` | closed without earned → 0; earned before close → 1; earned after close → 1; duplicates never 2; callbacks without show ignored; one grant per show |
| B2 Boost | `BoostAndTimeWarpTests` | 5 ads → 8 h cap; +2 h per ad; cap measured from now; boost doubles income |
| B3 Time Warp | `BoostAndTimeWarpTests` | reward = BaseIncome × 4 h, also while boosted; 10 min cooldown rejects second show (599 s no, 600 s yes); interstitial grace/cooldown/after-rewarded |
| B4 Offline | `OfflineTests` | clock back → 0; rollback-then-forward measures from high-water mark; 8 h cap; 50 % efficiency; x2 only once; x2 after collect → 0; < 60 s → 0; stamps round-trip in id-ID / de-DE |
| B5 Buy Max | `EconomyTests` | 2000 random cases match the geometric sum and never go negative; exactly-enough coins buys all k for k = 1…100; cost = Base × r^n |
| B6 Prestige | `EconomyTests` | 1e9 → 1 gem, 4e9 → 2 gems; coins and buildings reset; gems, PrestigeMult, lifetime and boost kept |
| B7 Save | `EconomyTests` | null / empty / corrupt / wrong-type JSON → fresh game, no exception; round-trip with `saveVersion` 2; IC-01 saves load; negative values sanitized |

### Core script changes

Game rules moved into a pure `CoinForge.Core` assembly so the tests can reach them. `IdleEngine`, `OfflineEarnings` and `AdManager` keep their IC-01 public API, so `CoinForgeUI` needed no changes for B.

| File | Change | Reason |
|---|---|---|
| `Core/GameClock.cs` (new) | `IClock`, `SystemClock`, `GameClock.Current` | Injectable time for tests |
| `Core/GameState.cs` (new) | Coins, buy, boost, time warp, prestige, save JSON | Testable model; `FromJson` never throws |
| `Core/RewardFlow.cs` (new) | Earned/closed state machine | IC-01 race fix, now tested |
| `Core/AdPacing.cs` (new) | Interstitial + time warp cooldowns | Tested pacing |
| `Core/Offline.cs` (new) | `OfflineCalc`, `OfflineClaim` | Tested offline math and doubler |
| `Core/UpgradeSystem.cs` | `MaxAffordable` corrects the log estimate both ways | Could under-buy by one at exact boundaries |
| `IdleEngine.cs` | Wraps `GameState`; load via `FromJson` | **Bug:** corrupt save threw in `Awake` (crash on every launch) |
| `OfflineEarnings.cs` | Uses `OfflineCalc`; invariant-culture stamps | **Bug:** stamps written with the device culture, read as invariant → no offline earnings on id-ID/de-DE phones |
| `AdManager.cs` | Uses `RewardFlow` / `AdPacing` | Same behaviour, tested logic |

## C. Consent (UMP)

Start order: `ConsentInformation.Update` → `LoadAndShowConsentFormIfRequired` → `MobileAds.Initialize` only when `CanRequestAds()`. Consent stored by an earlier session starts ads immediately while consent refreshes. Everything is asynchronous, so the game and tutorial keep running. Failure or offline: the game runs; consent is retried every 60 s while online and on every resume.

Verified on the Redmi (EEA debug geography + test device hash `3EEB92147C2A16BE07C8D9854A3AB576`):

```text
10:23:19 [Consent] Debug geography: EEA
10:24:01 [Consent] status=Obtained canRequestAds=True privacyOptions=Required
10:24:01 [Ads] MobileAds.Initialize (consent allows ads)
10:24:05 [Ads] Banner height px: 167
10:24:56 [Consent] Privacy options closed; canRequestAds=True
10:25:11 [Consent] Reset
10:25:34 [Consent] status=NotRequired canRequestAds=True privacyOptions=NotRequired   (EEA off, after reset)
```

No ad request was made before consent: the banner and rewarded ads stayed on "Loading…" while the form was open.

- [Consent form (EEA)](consent-form-eea.png)
- ["Privacy settings" in How to Play](help-privacy-button.png) — existing secondary style, shown only when `PrivacyOptionsRequirementStatus == Required`
- [Privacy options form](privacy-options-form.png)

Fixed during verification: SDK callbacks were marshalled with `MobileAdsEventExecutor`, which only exists after `MobileAds.Initialize`, so consent callbacks never ran. They now go through Unity's main-thread `SynchronizationContext`.

## D. Performance

Measured with the dev-only perf probe (`IC04_PERF` in logcat): 60 s, dev build, 1080×2340, banner live, `targetFrameRate` 60, vSync 1.

### Root cause

`Update.ScriptRunBehaviourUpdate` spiked to ~54 ms at the UI's 10 Hz heartbeat (spikes 0.1–0.2 s apart), while Canvas, layout and text stayed under 3 ms. `CoinForgeUI` reads `AdManager.BannerHeightPx` and `RewardedReady` on every refresh, and the tutorial reads the banner height every frame. Both were JNI calls into the ads plugin; `GetHeightInPixels` waits on the Android UI thread, which is busy with the banner WebView. Fix: the banner height is read once per load and `CanShowAd()` is cached for 30 s (taps still check fresh). No Codex UI code changed.

### Before / after

| Scenario | Build | Phone | avg FPS | P50 | P95 | P99 | frames > 20 ms | GC collections |
|---|---|---|---|---|---|---|---|---|
| Idle, main screen | before (`c09a356`) | — | 41.78 | 16.67 ms | **66.68 ms** | 83.36 ms | 544 | 0 |
| Idle, main screen | after (`caee780`) | 15 %, 41 °C | **59.87** | 16.67 ms | **16.67 ms** | 16.68 ms | 7 | **0** |
| 74 taps in 60 s | after (`caee780`) | 15 %, 43 °C | **58.55** | 16.67 ms | **16.68 ms** | 33.34 ms | 79 | **0** |

All targets met: avg ≥ 55 FPS, P95 ≤ 25 ms, 0 GC collections in 60 s, `targetFrameRate` 60. Banner live in every run.

Earlier "after" runs taken while the phone sat at 0–2 % battery and 46 °C gave 58.48 FPS idle but P95 33.34 ms while tapping. The same build measured on the cooler phone meets P95, so that miss was thermal throttling. One run that started right after the debug panel was open recorded 1 GC collection; the garbage comes from the dev-only IMGUI panel, and clean runs record 0.

### Remaining headroom (no action needed)

Tap frames spend most of their time rendering the overlay UI (`Canvas.RenderOverlays` ~3.5 ms average, ~10 ms P95) rather than in scripts (`Update` 0.5 ms). Turning off best-fit on the floating "+value" labels made no difference, so it was reverted. If weaker phones miss the target later, these are the options:

| Option | Expected effect | Impact |
|---|---|---|
| Lighter tap VFX (smaller burst/sparkle frames or fewer layers) | Less overdraw on tap frames | Visual change; IC-02/IC-03 owners |
| Graphics API: GLES3 before Vulkan for Android | Adreno 5xx Vulkan drivers are often slower for UI; needs an A/B run | Project setting; affects every device |
| Canvas Screen Space Overlay → Screen Space Camera + URP render scale 0.85 | ~28 % fewer pixels (overlay UI ignores render scale today) | UI slightly softer; canvas change in Codex territory |
| TextMeshPro migration | Lowers text CPU | Large UI rewrite; little effect on rendering |

## Release build

`CoinForgeSetup.BuildReleaseApk` (release configuration, debug-signed). IL2CPP metadata and `libil2cpp.so` were scanned for debug type, method and string names:

| Symbol | Release APK | Dev APK |
|---|---|---|
| `CoinForgeDebugPanel`, `CoinForgePerfProbe`, `HoldToOpen` | absent | present |
| `DebugAddCoins`, `DebugSkip`, `DebugSaveBlocked`, `DebugResetConsent`, `DebugConsentSettings` | absent | present |
| `dbg_ump_eea`, `IC04_PERF`, `IC-04 DEBUG`, test device hash | absent | present |
| `GameState`, `RewardFlow`, `StartAfterConsent`, `Privacy settings` | present | present |

## Findings for the PM

1. **Time Warp almost never appears, and pays nothing when it does.** The rule is "next purchase more than 180 s away", and `SecondsToNextPurchase` uses the *cheapest* building. Income comes from the top tiers while the Coin Press stays cheap, so after the first purchase the cheapest item is always seconds away. Simulated: early game 172.5 s; after buying max with +1e9 or +1e12 coins, 0–19 s. In practice Time Warp only shows at zero income (fresh save or right after Prestige), where its reward is +0 coins (confirmed on the Redmi). Suggest IC-05 redefine "next item" (e.g. next *unowned* or *most expensive affordable-soon* building).
2. **Test device hash** for UMP and ads on the Redmi: `3EEB92147C2A16BE07C8D9854A3AB576`.

## E. Manual tests on the Redmi (debug panel + adb)

Exact values come from the save read with `adb shell run-as` (the dev build is debuggable).

| Test | Result | Evidence |
|---|---|---|
| Boost stack 8 h | ✅ 5 Boost ads in a row capped at 8 h, not 10 h: after 1h 35m away the HUD showed 6h 23m | [screenshot](e-boost-cap-offline.png) |
| Offline (same screenshot) | ✅ 5,700 s × 1.1/s base × 50 % = +3.15K; the active boost was not applied | [screenshot](e-boost-cap-offline.png) |
| Buy Max | ✅ Max prices matched the geometric sum for Coin Press (k = 29, 26.3K), Mini Forge (k = 26, 24.6K), Smelter (k = 11, 25.3K). Buying Coin Press: 11 → 40 owned, spent 26,321.1154838639 = formula to 11 decimals, 1,488 coins left | [screenshot](e-buy-max.png) |
| Prestige #1 | ✅ lifetime 1.00003e9 → "0 → 1 Gems"; coins 0, all buildings 0, boost kept | [dialog](e-prestige-dialog.png), [result](e-prestige-1.png) |
| Prestige #2 | ✅ lifetime 4.0000286e9 → "1 → 2 Gems"; save `gems 2, coins 0, owned all 0`, `boostEnd` unchanged | [dialog](e-prestige-2-dialog.png), [result](e-prestige-2.png) |
| Time Warp + cooldown | ✅ ad watched at zero income (reward +0, see Findings); 45 s and 6m45s later Boost was ready but Time Warp hidden; 10m43s later it was back | [before](e-timewarp-before.png), [45 s](e-timewarp-cooldown.png), [6m45s vs 10m43s](e-timewarp-cooldown-6m-vs-10m.png) |
| Interstitial pacing | ✅ Prestige 163 s into the session: no interstitial (3 min grace). Stats at 212 s: shown. Prestige #2 within 5 min of it: none | logcat `AdActivity` times |
| Save `saveVersion` | ✅ `"saveVersion":2` in the device save | `run-as` dump |
| Debug panel | ✅ opens on a 3 s hold; +1e9, consent toggle/reset and perf probe used above | — |
