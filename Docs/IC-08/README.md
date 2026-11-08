# IC-08 — Coin Forge splash intro

Unity 6000.3.27f1; intended for v1.0.1 / code 2 with IC-06b.

## Implementation

Main contains the new CoinForgeIntro prefab: a Screen Space Overlay Canvas, order 30000, reference 1080×1920, match 0.5. No separate scene or new package.

The unscaled coroutine follows the supplied full timeline, including the additional 0.3-second fade: about 2.3 seconds total. Repeat launches use 0.9 seconds of content plus a 0.3-second fade. Tap after 0.5 seconds skips to fade. Neither path waits for SDK/network loading; the animation has a four-second guard. Android/native Unity startup before the first rendered scene frame is outside that animation timer.

The engine continues running. Consent information updates in the background; its native form waits for the intro to disappear. UI startup then waits for consent completion, presents pending Welcome Back, and starts FTUE after collection. Banner/rewarded/interstitial presentation is blocked during the intro and tutorial. SDK initialization remains enabled. Banner height still comes from AdManager at runtime.

The intro uses the supplied logo/hammer copied from F_Store into the requested G_Intro paths, plus the existing background, coin, eight coin-burst frames, six sparkle frames and sliced gold button artwork. Texture uploads occur before animation; safe-area checks are throttled; per-frame animation uses existing objects and structs. No Instantiate during animation.

## Files

New:
- Assets/Scripts/CoinForgeIntro.cs and CoinForgePresentation.cs
- Assets/Editor/CoinForgeIntroSetup.cs
- Assets/Prefabs/CoinForgeIntro.prefab
- Assets/Design/G_Intro/logo_coinforge.png and hammer.png, with import metadata
- This evidence directory

Updated:
- Main.unity: intro prefab instance
- CoinForgeSetup.cs: preserve the intro when rebuilding Main
- CoinForgeUI.cs: queue startup screens, gate input, defer hidden UI refresh
- CoinForgeUI.Tutorial.cs: presentation gate instead of disabling AdManager
- CoinForgeTutorialHand.cs: require CanvasRenderer
- AdManager.cs: presentation-only hooks for consent/banner/fullscreen-ad gating

AdManager was changed during IC-08 before the subsequent PM instruction restricting core changes. Claude Code/PM should review those hooks before the release build. IdleEngine, OfflineEarnings, balancing and reward math are unchanged. Future core hooks must be requested from Claude Code rather than edited by UI work.

## Redmi Note 7 checks

QA updated the existing separate com.publicvoid.coinforge.ic02qa package using test ads. The main package/save was untouched. First-run state was exercised by resetting QA preferences, rather than installing a new package. Temporary seeding/instrumentation/build scripts were removed from Assets and are not committed.

| Check | Evidence/result |
| --- | --- |
| Fresh QA state → full intro → tutorial step 1 | intro-full.mp4, first-launch.png, full.log; 2.343 s |
| Actual close for over two minutes → short intro → Welcome Back | intro-return.mp4, welcome-back.png, return.log; 1.217 s intro; popup reports 2m 35s |
| Physical tap-to-skip | skip.log; 1.625 s total, skipped=True |
| Forced EEA native consent | consent.png, consent.log; intro ends first, then consent; after dismissal tutorial step 1 |
| Ads blocked during intro/tutorial | state logs show blocked=True; no banner in animation frames |
| Ads resume in same session | welcome-banner.png and main-banner.png; banner measured 167 px, Boost ready |
| 16:9 / 19.5:9 / 20:9 | logo-16x9.png / logo.png / logo-20x9.png; safe-area composition remains visible |
| Hammer strike / logo | hammer-strike.png / logo.png; exact frames extracted from adb screenrecord |

Consent release may log a few milliseconds before the final intro log because the Canvas is already transparent when the completion event fires. The intro is not held for consent network latency.

## Performance / release handoff

Final development APK, without screenrecord: 56.47 FPS average, p95 33.34 ms, maximum 33.35 ms, zero GC collections during the full intro. Whole-player allocation instrumentation measured p95 308 B/frame and maximum 3038 B/frame, including the QA observer. The recorded full run measured 54.80 FPS with one collection; the real-return short run measured 54.06 FPS with no collections.

**Stable 60 FPS is not verified.** Release-build profiling remains a release QA item; these measurements must not be reported as a 60 FPS pass. No further profiling build was started after the request to release Unity for Claude Code.

Local QA used the IC-06b v1.0.1/code 2 settings. No signed release AAB was generated or published by IC-08. Existing IC-06b changes remain outside this commit. Unity is closed for Claude Code's final build.
