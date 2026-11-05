# IC-02 · Coin Forge

## Unity integration

- `Assets/Scenes/Main.unity` uses `Assets/Prefabs/CoinForgeUI.prefab` with the 1080×1920 Canvas Scaler and a 0.5 match.
- `CoinForgeUI.cs` handles events, actions, prices, dialogs, and stats. `CoinForgeUI.View.cs` builds the view and applies safe area and banner insets. `CoinForgeUI.Tap.cs` handles the scale punch, 8-frame coin burst, 6-frame sparkle, and 20-label pool.
- `Assets/Editor/CoinForgeSetup.cs` installs the same prefab when it recreates Main.
- The upgrade panel and popup shield share a lower edge. The layout reads the runtime banner height and adds 16dp. Before that height is available, it scales the 167px reference from a 1080px screen.
- Panel borders are 32px on each side. Button borders are 48px left and right and 40px top and bottom. Both use `Image.Type.Sliced`.
- The English UI uses Unity’s built-in font in bold. Minimum touch and text sizes follow Android density and scaled density.

## IC-03 asset slots

Image object names match the PNG filenames. The import limit is the maximum Android texture dimension.

| PNG slot | Source size | Import limit |
| --- | --- | --- |
| `bld_01_coin_press.png`, `bld_02_mini_forge.png`, `bld_03_smelter.png`, `bld_04_foundry.png`, `bld_05_mint.png`, `bld_06_vault.png`, `bld_07_bank.png`, `bld_08_exchange.png`, `bld_09_treasury.png`, `bld_10_galaxy_mint.png`, `bld_locked.png` | 512×512 | 256 |
| `coin_icon.png`, `gem_icon.png` | 128×128 | 128 |
| `coin_main.png` | 1024×1024 | 512 |
| `ic_ad_badge.png`, `ic_boost2x.png`, `ic_milestone.png`, `ic_offline.png`, `ic_prestige.png`, `ic_timewarp.png` | 256×256 | 256 |
| `bg_main.png` | 1080×2340 | 2048 |
| `panel_frame.png` | 512×512 | 512 |
| `btn_gold.png`, `btn_gray.png`, `btn_green.png` | 512×160 | 512 |
| `vfx_coin_burst_01.png` through `vfx_coin_burst_08.png` | 256×256 per frame | 256 |
| `vfx_sparkle_01.png` through `vfx_sparkle_06.png` | 256×256 per frame | 256 |

Store assets imported with IC-03 include `app_icon_512.png` (512×512) and `feature_graphic.png` (1024×500).

## Verification

The Android development APK built in Unity 6000.3.27f1. Play Mode passed 18 checks. The Redmi also passed checks for x1, x10, and Max prices and availability, purchase, Stats, and Prestige. Collect dismissed Welcome Back and credited coins. The device showed Time Warp and its reward when rewarded ads were ready and the purchase ETA exceeded 180 seconds. A separate fixture with an affordable purchase kept Time Warp hidden.

| Redmi Note 7 screenshot | Resolution | Display mode |
| --- | --- | --- |
| [16:9](16x9.png) | 1080×1920 | Temporary display override |
| [19.5:9](19.5x9.png) | 1080×2340 | Physical display with notch |
| [20:9](20x9.png) | 1080×2400 | Temporary display override |
| [Welcome Back and native test banner](welcome-banner.png) | 1080×2340 | Native AdMob banner beneath the popup |

Unity reported a 79px top safe-area inset on the physical display. The native banner measured 167px. At 440dpi the layout reserves 44px, or 16dp, above it. Editor QA also exercised a simulated punch-hole inset.

The initial native-resolution development build averaged 35.73 FPS idle and 33.34 FPS during fast taps. The rendering pass reduced fullscreen layers, simplified the scene camera, and isolated the HUD and tap animation from the main Canvas. The final English development build measured 47.69 FPS idle and 49.08 FPS during 96 taps in eight seconds at 1080×2340 with the banner active. P95 frame time was 50.01 ms in both samples. P95 GC allocation was 218 B per frame, and neither sample recorded a collection. The target of a sustained 60 FPS remains unmet on this Redmi. Lower-resolution diagnostic runs stayed around 50 FPS, so the project uses native resolution.

## Data notes

- IC-01 has no stored play time. The UI records foreground time from IC-02 onward in a separate PlayerPrefs key.
- The UI reads the banner getter on the coin heartbeat and when the Canvas dimensions change.
- The displayed offline duration comes from `OfflineEarned` and follows the IC-01 cap.
- Pricing, rewards, purchases, and Prestige continue to use the IC-01 APIs. The six core scripts match baseline `cc3eb74`.
