# IC-04b · Time Warp trigger

## Rule

`SecondsToNextPurchase()` (cheapest building, boosted income) is replaced by `SecondsToNextGoal()`:

- target = first-unit cost of the lowest-index building with `Owned == 0`;
- if every building is owned, target = `CostOf` of the last (most expensive) building;
- `0` if `Coins >= target`; `MaxValue` if base income is 0; otherwise `(target − Coins) / BaseIncomePerSec`.

Time Warp is shown when `RewardedReady && TimeWarpReady && BaseIncomePerSec > 0 && SecondsToNextGoal() > 180` (and the reward is above 0). The button still shows `+X coins`.

| File | Change |
|---|---|
| `Core/GameState.cs` | `NextGoalCost`, `SecondsToNextGoal()` |
| `Core/TimeWarpRule.cs` (new) | `ShouldShow(rewardedReady, timeWarpReady, state, hours)` |
| `IdleEngine.cs` | `SecondsToNextPurchase()` removed; `SecondsToNextGoal()`, `ShouldOfferTimeWarp()` added |
| `CoinForgeUI.cs` | `CanTimeWarp()` calls the new rule; layout unchanged |
| `Debug/CoinForgeDebugPanel.cs` | Fix: "Reset save" lost the wipe because the process was killed before Android wrote PlayerPrefs; it now waits for the write |

## Tests — 56/56 passed

[`../IC-04/editmode-results.xml`](../IC-04/editmode-results.xml). New fixture `TimeWarpTriggerTests` (10 tests):

| WO | Test |
|---|---|
| (a) income 0 → hidden | `NoIncome_Hidden` |
| (b) new tier > 3 min → shown | `NewTierMoreThanThreeMinutesAway_Shown`, `CheapOwnedBuildingAffordable_StillShownForNewTier`, `NewTierWithinThreeMinutes_Hidden` |
| (c) all owned → most expensive | `AllOwned_UsesMostExpensiveBuilding` |
| (d) cooldown → hidden | `CooldownActive_Hidden`, `CooldownFromAdPacing_HidesForTenMinutes` |
| extra | `GoalAffordable_ReturnsZero`, `GoalIsLowestUnownedIndex_NotCheapest`, `BoostDoesNotChangeTheGoalTime` |

## Redmi Note 7, fresh save

Save wiped with the debug panel (all PlayerPrefs keys gone), app launched at 13:26:08, tutorial played (finished ≈ t+85 s), then scripted play: 3 coin taps per cycle and a buy attempt on Coin Press, Mini Forge and Smelter. The Time Warp button was sampled every cycle.

| Time from launch | State | Why |
|---|---|---|
| **t+110 s (1 min 50 s)** | **First shown**, "+1.44K coins" | 1 Coin Press (0.1/s), 13 coins → Mini Forge 870 s away |
| t+252 s (4 min 12 s) | Hidden | 5 Coin Press (0.5/s), 10 coins → Mini Forge exactly 180 s away |
| t+360 s (6 min) | Hidden | 10 Coin Press (1/s) → Mini Forge 82 s away |

[Screenshots: first shown · hidden at 180 s · end](timewarp-fresh-save-110s-252s-360s.png)

The first appearance is about 25 s after the tutorial, once consent has run and the rewarded ad has loaded (ads stay off during the tutorial).

## Notes for the PM

- Early on the reward is large relative to the goal: at 1:50 Time Warp pays 1,440 coins toward a 100-coin Mini Forge. Worth a look in IC-05 balancing.
- The phone was at 10 % battery and 46 °C; this test does not depend on performance.
