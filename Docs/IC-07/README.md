# IC-07 · How to Play and tutorial

## Redmi Note 7 screenshots

All six captures are 1080×2340 (19.5:9).

| Screen | Screenshot |
| --- | --- |
| Step 1 · Tap the coin | [Step 1](step-1.png) |
| Step 2 · Buy a Coin Press | [Step 2](step-2.png) |
| Step 3 · Read income | [Step 3](step-3.png) |
| Step 4 · See milestone progress | [Step 4](step-4.png) |
| Step 5 · Learn about the optional Boost | [Step 5](step-5.png) |
| How to Play · Native test banner visible | [How to Play](how-to-play.png) |

The preceding IC-02b polish has separate [main screen](../IC-02b/main.png) and [Welcome Back](../IC-02b/offline.png) captures.

## Device checks

- Physical taps completed the new-save walkthrough. After five taps, the coin remained highlighted until the player could afford Coin Press. The first purchase advanced the tutorial.
- Skip worked on all five steps. Force-stop and relaunch preserved completion after both Got it and Skip. A save with existing buildings skipped the first-run tutorial.
- The tutorial blocked input outside the active controls and prevented ad actions. During a fresh walkthrough the ad manager stayed disabled. Replay hid the loaded 167px native banner and restored it on exit.
- How to Play opened, scrolled, and closed. A test with fractional Inspector values confirmed that offline duration, offline efficiency, Boost duration, Boost cap, and Time Warp duration updated in the copy.
- Replay resets `ic_tutorial_done`. Players who already own a Coin Press can continue past its lesson with Next.
- Tutorial and Help layouts were checked at 16:9, 19.5:9, and 20:9 on the Redmi. The 16:9 and 20:9 checks used temporary display overrides. The Help button uses its full 48dp hit area.

## Files

- Added `Assets/Scripts/CoinForgeUI.Tutorial.cs`, `Assets/Scripts/CoinForgeUI.Help.cs`, and `Assets/Scripts/CoinForgeTutorialHand.cs` and their Unity metadata.
- Updated `Assets/Scripts/CoinForgeUI.cs`, `Assets/Scripts/CoinForgeUI.View.cs`, and `Assets/Scripts/CoinForgeUI.Tap.cs` for UI integration.
- Added the screenshot sets under `Docs/IC-02b/` and `Docs/IC-07/`.
- The gameplay, save, offline earnings, and ad manager scripts match the IC-02 baseline.
