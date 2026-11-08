# Coin Forge · Play Console Data safety — DRAFT (IC-06)

Status: draft for PM review. Not submitted.
Source: Google's disclosure for the Google Mobile Ads SDK 25.5.0, which this build uses ([developers.google.com/admob/android/privacy/play-data-disclosure](https://developers.google.com/admob/android/privacy/play-data-disclosure)), plus what the game's own code does.

## What the game itself does

- The game has no accounts, no backend and no analytics SDK of its own.
- The save game (coins, buildings, gems, boost time, offline stamp, tutorial flag) is stored only on the device, in PlayerPrefs. It never leaves the phone, so Play does not count it as "collected".
- All collected data comes from the **Google Mobile Ads SDK** (AdMob, for banner, interstitial and rewarded ads) and the **User Messaging Platform SDK** (the consent form).

## Overview answers

| Question | Draft answer |
|---|---|
| Does your app collect or share any of the required user data types? | **Yes** |
| Is all of the user data collected by your app encrypted in transit? | **Yes** (the SDK sends everything over TLS) |
| Do you provide a way for users to request that their data is deleted? | **No**. There is no account and the app stores nothing on a server. Users can reset or delete their advertising ID in Android settings, and EEA/UK users can withdraw consent with **Privacy settings** in How to Play. *(PM: confirm, or add a contact email for requests.)* |

## Data types

All four are collected **and** shared by the Google Mobile Ads SDK. They are processed in the normal way, not ephemerally.

| Play Console category → type | What the SDK collects | Collected | Shared | Purposes | Required or optional |
|---|---|---|---|---|---|
| **Location → Approximate location** | Device IP address, which may be used to estimate general location | Yes | Yes | Advertising or marketing, Analytics, Fraud prevention, security, and compliance | Required |
| **App activity → App interactions** | App launches, taps, video (ad) views | Yes | Yes | Advertising or marketing, Analytics, Fraud prevention, security, and compliance | Required |
| **App info and performance → Diagnostics** | App and SDK performance: launch time, hang rate, energy use | Yes | Yes | Analytics, Fraud prevention, security, and compliance | Required |
| **Device or other IDs → Device or other IDs** | Android advertising ID, app set ID | Yes | Yes | Advertising or marketing, Analytics, Fraud prevention, security, and compliance | Required *(the user can reset or delete the ad ID in Android settings)* |

Google gives one combined purpose ("advertising, analytics, and fraud prevention") for all four types. The per-row split above is our reading of it; the PM may tick all three purposes on every row to match Google's wording exactly.

**Not collected:** personal info (name, email, user IDs), financial info, health, messages, photos or videos, audio, files, calendar, contacts, web browsing, precise location, crash logs (the game has no crash reporter; Diagnostics covers the SDK's performance data).

## Other Play Console declarations this build needs

- **Advertising ID declaration** (Policy → App content): **Yes**, the app uses the advertising ID. `play-services-ads` adds the `com.google.android.gms.permission.AD_ID` permission. Purposes: Advertising or marketing, Analytics, Fraud prevention.
- **Contains ads:** **Yes**.
- **Privacy policy URL:** required for apps with ads and the advertising ID. It must name AdMob/Google as a third party and link [Google's partner-sites policy](https://policies.google.com/technologies/partner-sites). *(PM: still needed.)*
- **Target audience:** the ad requests are not configured for children (no `TagForChildDirectedTreatment`, no Families-certified mediation). The audience should be **13+ / not primarily children**. If under-13s are a target, the ad setup must change before release. *(PM decision.)*
- **AdMob:** before release, publish a GDPR message (and a US-states message if wanted) under Privacy & messaging for this App ID. Without one, UMP shows no form, even in the EEA.
