# Changelog — Coin Forge

## v1.0.0 (versionCode 1) — 2026-11-08 — Initial release (Internal testing)
Engine: Unity 6000.3.27f1 · Google Mobile Ads 11.6.0 · Min API 25 · Target API 36 · IL2CPP arm64
Package: com.publicvoid.coinforge · Commit: b0880cf

### Gameplay
- Tap-to-earn core loop; tap value scales with 5% of income/sec.
- 10 buildings (Coin Press → Galaxy Mint); cost = Base × r^n (r 1.10–1.15).
- Milestones: ×2 output every 25 units of a building.
- Buy x1 / x10 / Max with exact geometric cost.
- Prestige: Gems = floor(sqrt(lifetime / 1e9)); +2% income per Gem; Boost timer kept on reset.
- Offline earnings: 50% efficiency, 8-hour cap, clock-rollback protection.
- Big-number formatting (K, M, B, T … Dc, then aa, ab …).
- Balance pass IC-05/05b: T2 ≈ 0:40, T3 ≈ 3:00, T4 ≈ 8–9 min for an active player.

### Monetization (AdMob)
- Rewarded: 2x Boost 2h (stacks to 8h), Time Warp +4h income (10-min cooldown,
  shown when next new building is > 3 min away), Offline earnings ×2.
- Rewards granted only after completion; race between reward/close callbacks handled (exactly once).
- Interstitial: after Prestige / opening Stats; 3-min session grace, 5-min cooldown,
  60 s after any rewarded; never during tutorial.
- Adaptive banner at bottom with 16dp safety gap above it.
- UMP consent (GDPR) before ad init; Privacy settings button when required.
- Production ad IDs in AdConfig; development builds use Google test IDs automatically.

### UI / UX
- Final art set (IC-03): 10 buildings, coins, gems, feature icons, 9-slice panels and buttons, VFX.
- English UI, safe-area support (notch/punch-hole), 16:9 to 20:9 layouts.
- 5-step first-run tutorial (skippable) + How to Play screen with live values from game settings.
- Welcome Back popup, Stats and Prestige dialogs.

### Technical
- Event-driven architecture (GameEvents bus); save via PlayerPrefs JSON with saveVersion 2
  and corrupted-save fallback.
- Performance: 59.9 FPS avg, P95 16.7 ms, 0 GC collections (Redmi Note 7).
- 63 EditMode tests (rewards, boost cap, Time Warp, offline, Buy Max, prestige, save, ad IDs).
- R8/minify enabled with keep rules for Google Mobile Ads + UMP.
- Debug panel, playtest bot and QA logs excluded from release builds (verified by scan script).

### Known limitations
- Consent form shows a default icon until the app is linked to its Play listing in AdMob.
- English only.
