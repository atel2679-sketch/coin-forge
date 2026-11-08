# IC-06 · Release build

**Result:** `Builds/CoinForge-1.0.0-1.aab` is **34,798,049 bytes (34.8 MB)**. The download for the Redmi Note 7 (arm64, xxhdpi) is **30,991,043 bytes** (bundletool `get-size`).

- **Build:** IL2CPP, arm64-v8a only, versionCode 1, versionName 1.0.0, minSdk 25, targetSdk 36. R8 minify is on.
- **Signing:** signed with the upload key `coinforge` (RSA 2048, SHA256withRSA, valid until 2054). Certificate SHA-256 fingerprint: `84:AE:38:38:6F:F4:03:49:78:1B:53:73:67:4B:9D:18:8C:62:C0:0B:78:4D:61:91:64:09:AB:6F:79:88:C7:E7`.
- **Tests:** EditMode 63/63 (56 old plus 7 new `AdIdsTests`). Results: [editmode-results.xml](editmode-results.xml).

## 1. Production IDs in one place

All AdMob IDs live in `Assets/Resources/AdConfig.asset` (class `AdConfig`): the App ID, the 3 ad units and the test devices.

| Build | IDs used |
|---|---|
| Editor and Development Build | Google test IDs, always (`AdConfig.Active` → `AdIds.Test`, compile-time) |
| Release | Production IDs from `AdConfig.asset` |

- **Release guard:** `BuildReleaseAab` refuses to build if any production ID is empty, malformed, a Google test ID, from another publisher, or a duplicate (`AdIds.ReleaseProblems`, unit-tested).
- **App ID in the manifest:** the build writes the production App ID into the plugin settings only for the length of the build. The committed settings asset always keeps the test App ID.

## 2. Test device

`MobileAds.SetRequestConfiguration(TestDeviceIds)` runs before `Initialize`, in every build. The list (`AdConfig.testDeviceIds`) holds the Redmi c890abc: `3EEB92147C2A16BE07C8D9854A3AB576`. With the production units, logcat shows `This request is sent from a test device.` and every ad carries the **Test Ad** label.

## 3. Release build

| | |
|---|---|
| **Command** | `Tools\build-release.ps1`. It asks for the keystore password with hidden input, puts it in environment variables for the Unity process only, then clears them. |
| **Unity entry point** | `CoinForgeSetup.BuildReleaseAab`. It reads `CF_KEYSTORE_PATH`, `CF_KEY_ALIAS`, `CF_KEYSTORE_PASS` and `CF_KEY_PASS`. |
| **Keystore** | `F:\Keystores\coinforge-release.keystore`, alias `coinforge`, PKCS12, RSA 2048, 10000 days. Created with `Tools\make-release-keystore.ps1`. **Not in the repo.** |
| **R8 keep rules** | `Assets/Plugins/Android/proguard-user.txt`: GMA and UMP SDKs, the Unity plugin bridges, and the Unity player. |
| **After the build** | Keystore path and alias are cleared from `ProjectSettings`. The passwords are blanked in Unity's generated `Library/.../launcher/build.gradle`, where Unity writes them in plain text. |

**R8 finding:** the first minified build failed with 54 `Missing class com.google.android.libraries.ads.mobile.sdk.*` errors. These come from the plugin's "next-gen" bridge (`com.google.unity.ads.nextgen`), which refers to the GMA Next-Gen SDK. This project doesn't include that SDK and never calls those classes, so a `-dontwarn` for the package fixes it.

## 4. No debug code in release

`python -I Tools/check_release_aab.py Builds/CoinForge-1.0.0-1.aab --expect-app-id ca-app-pub-2745406901501105~3201678057` scans the IL2CPP metadata and `libil2cpp.so`. Result: **PASS**.

- **Absent:**
  - debug panel: `CoinForgeDebugPanel`, `HoldToOpen`
  - perf probe and playtest bot: `CoinForgePerfProbe`, `CoinForgePlaytestBot`
  - dev hooks: `DebugAddCoins`, `DebugSkip`, `DebugSaveBlocked`, `DebugResetConsent`, `DebugEeaKey`, `dbg_ump_eea`
  - QA log tags and panel text: `IC04_PERF`, `IC05_PLAYTEST`, `timewarp_first_shown`, `IC-04 DEBUG`, `Playtest bot`
  - screenshot build code: `StoreShots`, `-storeshot`
- **Present:** `GameState`, `RewardFlow`, `StartAfterConsent`, `AdConfig`, `ReleaseLogging`, `Privacy settings`.
- **Manifest:** exactly the production App ID. **ABI:** arm64-v8a only.
- **Logging:** release builds also drop `Debug.Log` info lines from logcat (`ReleaseLogging`). Warnings, errors and crashes are still logged.

## 5. Device test (Redmi Note 7, Android 10)

The AAB was installed with bundletool (`build-apks --connected-device`, then `install-apks`). The local APK set is signed with the debug key; Play signs the real one.

The consent form is only shown in the EEA. A registered test device can force the EEA region in any build with `am start ... -e unity "-ump-eea"`. UMP ignores this setting on devices that aren't registered as test devices.

| Check | Dry run (test IDs, R8) | Release AAB (production IDs) |
|---|---|---|
| Installs and starts, not debuggable, 1.0.0 (1) | ✅ | ✅ |
| Consent form (EEA test region) | ✅ "Publisher Test Ads" form ([screenshot](dryrun-consent-eea.png)) | ✅ "CoinForge" form ([screenshot](release-consent-eea.png)). The first load timed out ("Web view timed out"); the 60 s retry loaded it. |
| Consent → ads | ✅ banner and rewarded ad | ✅ "Do not consent" still serves ads ([screenshot](release-production-units-test-ads.png)) |
| Ads labelled **Test Ad** on the test device | ✅ | ✅ with the production units |
| Rewarded ad → reward granted, next ad reloads | ✅ Boost 1h 59m ([ad](dryrun-rewarded-test-ad.png), [after](dryrun-after-reward.png)) | ✅ Boost 1h 59m |
| Privacy settings button (EEA) | — | ✅ ([screenshot](release-privacy-settings.png)) |
| Crash, ANR or `E/Unity` | none | none |

## 6. Play Store screenshots

[store-screenshots/](store-screenshots/) holds 4 shots at 1080×2340 with no banner.

| File | Shows |
|---|---|
| `01-early-game.png` | Early game: first 3 buildings, Boost and Time Warp offers |
| `02-boost-timewarp.png` | Mid game, Boost active, Time Warp +1.03B |
| `03-late-game.png` | Late game: 86.1B coins, 12 gems, Boost 5h 39m |
| `04-offline-earnings.png` | "Welcome back" offline earnings with the ×2 ad offer |

They come from `CoinForgeSetup.BuildStoreShotsApk`:
- **Build type:** release configuration, so there's no "Development Build" watermark.
- **Ads:** test IDs. The banner and interstitials are never loaded.
- **Preset saves:** loaded with `-e unity '-storeshot N'`.
- **Not in the release AAB:** the code only compiles with the `CF_STORE_SHOTS` define.

With `adb shell`, the extra needs inner quotes (`"am start ... -e unity '-storeshot 1'"`). Without them the number gets lost.

## 7. Data safety

Draft: [data-safety-draft.md](data-safety-draft.md).

## Incident: keystore password shown in the terminal

The first version of `make-release-keystore.ps1` ended with `keytool -list ... | Select-String`. Because of the pipe, keytool's password prompt echoed the typed password in plain text. The password showed up in the terminal and in the Claude session transcript.

**Fix:**
- The keystore password was changed right away with `keytool -storepasswd`, before anything was signed. The AAB is signed with the new password.
- The pipe was removed from the script.
- Nothing was committed or written to a file.

## Open points for the PM

1. **AdMob app icon:** the consent form shows the default Unity icon. AdMob takes the icon from the linked store listing, so link the app in AdMob after publishing.
2. **Consent load timeout:** the first load of the production form timed out once on Wi-Fi. The retry (60 s, and on resume) recovered. No change made.
3. **Privacy policy URL and target audience:** still needed for the Play Console (see the Data safety draft).
4. **Keystore backup:** `F:\Keystores\coinforge-release.keystore` and its password must be backed up outside this PC. Turn on Play App Signing when uploading; this key then acts as the upload key.
