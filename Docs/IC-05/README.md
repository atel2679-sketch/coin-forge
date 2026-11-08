# IC-05 · Balance pass

`UpgradeSystem.DefaultDefs()` now uses the PM table (base cost and base production changed; rates unchanged). Cost growth, milestones (×2 per 25), tap, offline and prestige formulas are unchanged.

| # | Name | Base cost | Base /s | r |
|---|---|---|---|---|
| 1 | Coin Press | 15 | 0.5 | 1.15 |
| 2 | Mini Forge | 120 | 3 | 1.15 |
| 3 | Smelter | 1.2e3 | 20 | 1.14 |
| 4 | Foundry | 1.3e4 | 110 | 1.13 |
| 5 | Mint | 1.5e5 | 600 | 1.13 |
| 6 | Vault | 1.8e6 | 3.3e3 | 1.12 |
| 7 | Bank | 2.5e7 | 1.8e4 | 1.12 |
| 8 | Exchange | 4e8 | 1e5 | 1.11 |
| 9 | Treasury | 6e9 | 6e5 | 1.10 |
| 10 | Galaxy Mint | 9e10 | 3.6e6 | 1.10 |

## Tests — 56/56 passed

Only `TimeWarpTriggerTests` used old numbers (Mini Forge 100, Coin Press 0.1/s, Galaxy Mint 7.5e10). It now reads them from `DefaultDefs()`, so future balance changes don't break it. Results: [`../IC-04/editmode-results.xml`](../IC-04/editmode-results.xml).

## Redmi Note 7, fresh save

adb `input tap` manages only ~1.3 taps/s on this phone and raw touch events need root, so a **dev-only playtest bot** (`Debug/CoinForgePlaytestBot.cs`, compiled out of release) plays inside the real game:

- taps through `IdleEngine.Tap()`: 3/s for the first minute, 2/s until 4:00, 1/s after;
- "saver" buying: the next new tier as soon as it is affordable; otherwise the best-payback owned building only if it pays for itself before the new tier is reached;
- logs each unlock and the first frame the real Time Warp button is active.

Save wiped (0 PlayerPrefs keys), app launched at 07:59:36, tutorial played over adb (first Coin Press in the tutorial), bot started at 0:27. Times below are from launch. [Log](playtest-log.txt) · [state after 10 min](after-10-min.png)

| Event | Phone | PM target | Simulation, same profile |
|---|---|---|---|
| T2 Mini Forge | **0:59** | ±0:40 | 0:38 from first tap (+ tutorial) |
| T3 Smelter | **3:07** | ±3:00 ✅ | 2:42 + tutorial |
| T4 Foundry | **6:49** | 8–10 min ❌ early | 6:19 + tutorial |
| Time Warp first shown | **0:59** | — | — |

From the bot's start, the phone's unlock times (+0:32, +2:40, +6:22) match the simulation within 6 s.

- **T2:** the extra ~20 s is the adb-driven tutorial (slow taps). A player who taps ~3/s reaches T2 around 0:40.
- **Time Warp:** first shown at the T2 unlock (income 3.5/s, Smelter 343 s away). Before that it was eligible right after the tutorial (1 Coin Press, Mini Forge > 180 s away), but ads only start after the tutorial and consent (0:26), and by the time the rewarded ad loaded the tapping had brought the Mini Forge within 3 minutes.

## T4 timing vs. tap rate (simulation, saver policy, from first tap)

| Taps/s | T2 | T3 | T4 | T5 |
|---|---|---|---|---|
| 0.5 | 1:58 | 4:22 | 8:07 | 14:52 |
| 1 | 1:22 | 3:39 | 7:18 | 13:53 |
| 2 | 0:52 | 2:57 | 6:26 | 12:44 |
| 3 | 0:38 | 2:31 | 5:50 | 11:50 |
| bot profile (3 → 2 → 1) | 0:38 | 2:42 | 6:19 | 12:55 |

T2 and T3 land on target with an active start; T4 reaches 8–10 min only if the player drops to ~0.5 taps/s or less after the first minutes.

If T4 should hit 8–10 min with the bot's profile, only Foundry's **cost** matters (its production starts after the unlock, so it doesn't move the unlock time). Simulated, same profile, from first tap; T2/T3 are unaffected:

| Foundry base cost | T4 |
|---|---|
| 1.3e4 (current) | 6:19 |
| 1.95e4 (×1.5) | 7:23 |
| 2.6e4 (×2) | 8:09 |
| 3.25e4 (×2.5) | 9:00 |

Not changed here; a PM call for the next pass.

## IC-05b · Foundry cost 1.3e4 → 2.6e4

PM decision. Other values unchanged. EditMode tests 56/56. Simulation (same saver policy, table read from `UpgradeSystem.cs`, from first tap):

| Tap profile | T2 | T3 | T4 | T5 |
|---|---|---|---|---|
| Bot profile (3/s → 2/s → 1/s) | 0:38 | 2:42 | **8:09** | 15:03 |
| Constant 0.5/s | 1:58 | 4:22 | 9:59 | 17:02 |
| Constant 1/s | 1:22 | 3:39 | 9:07 | 16:01 |
| Constant 2/s | 0:52 | 2:57 | 8:11 | 14:45 |
| Constant 3/s | 0:38 | 2:31 | 7:29 | 13:46 |

T4 with the bot profile is inside the 7:30–10:00 window, so no device re-test was run. Only a player tapping a constant 3/s for the whole session lands 1 s under 7:30.
