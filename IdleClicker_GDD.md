# Idle Clicker (codename: COIN FORGE) — GDD & Technical Plan
Engine: **Unity 2022.3 LTS / C#** · Target: Android (AdMob) · Work order prefix: **IC**

## 0. Tim & Peran
| Peran | Pemilik | Tanggung jawab |
|---|---|---|
| PM | Claude Chat | GDD, balancing, work order IC-xx, review |
| Lead Engineer | Claude Code | Sistem inti, save, ads, build |
| Lead UI Design | Codex | Layout UI, prefab, safe area, banner padding |
| Design Asset | ChatGPT | Ikon gedung, koin, background, VFX sprite |

---

## 1. Core Mathematical Concept

### 1.1 Loop 3 langkah
**TAP / PRODUCE → ACCUMULATE → REINVEST** (beli gedung → produksi otomatis naik → beli lebih banyak).

### 1.2 Formula
| Item | Formula |
|---|---|
| Biaya gedung ke-n | `Cost(n) = Base × r^n` (r = 1.07–1.15) |
| Biaya beli k sekaligus | `Base × r^n × (r^k − 1)/(r − 1)` |
| Max terjangkau | `k = floor(log_r(Coins×(r−1)/(Base×r^n) + 1))` |
| Produksi gedung | `Owned × BaseProd × 2^(floor(Owned/25))` (milestone ×2 tiap 25) |
| Total /detik | `Σ Prod × GlobalMult × AdBoost(2 jika aktif) × PrestigeMult` |
| Tap | `TapBase × (1 + 0.05 × Total/detik detik)` → tap tetap relevan |
| Prestige | `Gems = floor(sqrt(LifetimeEarnings / 1e9))`, tiap gem +2% produksi |

### 1.3 Tabel gedung (10 tier awal)
| # | Nama | Base Cost | Base /s | r |
|---|---|---|---|---|
| 1 | Coin Press | 15 | 0.1 | 1.15 |
| 2 | Mini Forge | 100 | 1 | 1.15 |
| 3 | Smelter | 1.1K | 8 | 1.14 |
| 4 | Foundry | 12K | 47 | 1.13 |
| 5 | Mint | 130K | 260 | 1.13 |
| 6 | Vault | 1.4M | 1.4K | 1.12 |
| 7 | Bank | 20M | 7.8K | 1.12 |
| 8 | Exchange | 330M | 44K | 1.11 |
| 9 | Treasury | 5.1B | 260K | 1.10 |
| 10 | Galaxy Mint | 75B | 1.6M | 1.10 |

**Desain "wall":** biaya tumbuh eksponensial (r^n) sedangkan produksi linear + milestone diskrit → tiap ~25 unit pemain kena perlambatan. Tepat di titik "tinggal 2–4 jam lagi" → tombol **Time Warp 4 jam** muncul (dorongan iklan paling kuat). Target sesi 1: wall pertama di menit 8–12.

### 1.4 Big Number
`double` (maks ~1.8e308) cukup untuk 10 tier + prestige; format: K, M, B, T, Qa, Qi, Sx, Sp, Oc, No, Dc lalu `aa, ab, …`. Tidak pakai BigInteger (berat GC).

---

## 2. AdMob Incentive Architecture

### 2.1 Rewarded (pendapatan utama)
| Reward | Efek | Trigger UI | Cooldown |
|---|---|---|---|
| **2× Boost** | ×2 produksi 2 jam (bisa stack sampai 8 jam) | Tombol permanen di HUD | — |
| **Time Warp** | Langsung +4 jam produksi | Muncul saat item berikut >3 menit menunggu | 10 menit |
| **Offline Doubler** | ×2 hasil offline | Popup "Welcome back" | per sesi |

Aturan: reward hanya di `OnUserEarnedReward`; preload ulang setelah ditutup; tombol disabled bila iklan belum siap (jangan janji palsu).

### 2.2 Interstitial (pengisi)
- Trigger: setelah prestige selesai, buka menu Milestone/Stats.
- **Cooldown ≥ 5 menit** global + tidak tampil 60 dtk setelah rewarded + tidak tampil di 3 menit pertama sesi.

### 2.3 Adaptive Banner
- Anchored adaptive, **BAWAH**, di bawah panel upgrade.
- Panel upgrade diberi `bottom inset = tinggi banner + 16dp` → tombol beli tidak pernah bersebelahan dengan banner. Codex wajib terapkan.

---

## 3. Software Systems Blueprint
```
GameEvents (static C# events, decoupled bus)
   ▲ publish/subscribe
IdleEngine ──> UpgradeSystem      (beli, cost, produksi total)
   │  └──> SaveSystem (PlayerPrefs JSON)
OfflineEarnings (timestamp UTC, anti-rollback, cap 8 jam)
AdManager (Rewarded/Interstitial/Banner) ──> GameEvents.RewardGranted
UI (Codex) hanya mendengar event, tidak menyentuh data langsung
```
Alur reward: UI → `AdManager.ShowRewarded(type)` → `OnUserEarnedReward` → `GameEvents.RaiseReward(type)` → `IdleEngine` terapkan efek.

---

## 4. Work Orders
| ID | Pemilik | Isi |
|---|---|---|
| IC-01 | Claude Code | Setup proyek Unity, import Google Mobile Ads plugin, masukkan 6 script, test ID |
| IC-02 | Codex | HUD (koin, /s, tap area, tombol 2×), panel upgrade scroll, popup offline, safe area + banner inset |
| IC-03 | ChatGPT | 10 ikon gedung 256px, koin, background, tombol, VFX tap (PNG transparan, gaya sama) |
| IC-04 | Claude Code | Prestige + milestone menu + interstitial hook |
| IC-05 | Claude Chat | Review balancing (simulasi 1 jam), cek kebijakan AdMob |
| IC-06 | Claude Code | Build AAB, ganti ke ID iklan produksi, rilis internal test |

**Checklist kebijakan AdMob:** pakai test ID saat dev; reward harus dijelaskan sebelum tonton; tidak ada interstitial saat startup/keluar; UMP consent form (GDPR) wajib sebelum load iklan.

## 5. Scripts (folder `Assets/Scripts/`)
`BigNum.cs`, `GameEvents.cs`, `UpgradeSystem.cs`, `IdleEngine.cs`, `OfflineEarnings.cs`, `AdManager.cs`
